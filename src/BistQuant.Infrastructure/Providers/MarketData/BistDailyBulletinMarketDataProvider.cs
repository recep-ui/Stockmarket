using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using BistQuant.Application.Common.Interfaces;
using BistQuant.Application.DTOs.MarketData;
using BistQuant.Application.Interfaces;
using BistQuant.Application.Services.MarketData;
using BistQuant.Domain.Entities;
using BistQuant.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace BistQuant.Infrastructure.Providers.MarketData;

public class BistDailyBulletinMarketDataProvider : IBistDailyBulletinMarketDataProvider
{
    private readonly IApplicationDbContext _context;
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<BistDailyBulletinMarketDataProvider> _logger;
    private readonly BistDailyBulletinParser _parser;
    private readonly ICorporateActionAdjustmentService _corporateActionService;
    private readonly IMarketSessionCalendar _sessionCalendar;
    private readonly string _storagePath;
    private readonly string _baseUrl;

    public MarketDataProviderCapabilities Capabilities => new(
        ProviderName: "Borsa İstanbul Pay Piyasası Günlük Bülten",
        SupportsRealtime: false,
        SupportsEndOfWeekOrDay: true,
        SupportedTimeframes: new[] { Timeframe.Daily },
        RequiresSessionClosure: true,
        Description: "Official Borsa İstanbul Daily Bulletin (BUL_<YYYYMMDD>.csv). Zero-cost official EOD feed."
    );

    public BistDailyBulletinMarketDataProvider(
        IApplicationDbContext context,
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<BistDailyBulletinMarketDataProvider> logger,
        BistDailyBulletinParser parser,
        ICorporateActionAdjustmentService corporateActionService,
        IMarketSessionCalendar sessionCalendar)
    {
        _context = context;
        _httpClient = httpClient;
        _configuration = configuration;
        _logger = logger;
        _parser = parser;
        _corporateActionService = corporateActionService;
        _sessionCalendar = sessionCalendar;

        _baseUrl = _configuration["BistBulletin:BaseUrl"] ?? "https://www.borsaistanbul.com/data/bulten";

        var configuredPath = _configuration["BistBulletin:StoragePath"] ?? "/app/data/marketdata";
        _storagePath = ResolveStoragePath(configuredPath);
    }

    private static string ResolveStoragePath(string configuredPath)
    {
        try
        {
            Directory.CreateDirectory(configuredPath);
            return configuredPath;
        }
        catch
        {
            var fallback = Path.Combine(AppContext.BaseDirectory, "marketdata");
            Directory.CreateDirectory(fallback);
            return fallback;
        }
    }

    public async Task<IEnumerable<SymbolDto>> GetSymbolsAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Symbols
            .AsNoTracking()
            .Where(s => s.IsActive)
            .OrderBy(s => s.Ticker)
            .Select(s => new SymbolDto(s.Id, s.Ticker, s.Name, s.Sector, s.Industry, s.IsActive))
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<PriceBarDto>> GetHistoricalBarsAsync(
        string symbol,
        DateTime start,
        DateTime end,
        Timeframe timeframe,
        CancellationToken cancellationToken = default)
    {
        // Daily bulletin provider only supports Daily timeframe; intraday requests return empty to prevent fabrication
        if (timeframe != Timeframe.Daily)
        {
            return Enumerable.Empty<PriceBarDto>();
        }

        var sym = await _context.Symbols
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Ticker == symbol.ToUpper(), cancellationToken);

        if (sym == null)
        {
            return Enumerable.Empty<PriceBarDto>();
        }

        return await _context.PriceBars
            .AsNoTracking()
            .Where(p => p.SymbolId == sym.Id && p.Timeframe == timeframe && p.Timestamp >= start && p.Timestamp <= end)
            .OrderBy(p => p.Timestamp)
            .Select(p => new PriceBarDto(
                sym.Ticker,
                p.Timeframe,
                p.Timestamp,
                p.Open,
                p.High,
                p.Low,
                p.Close,
                p.Volume,
                p.AdjustedClose))
            .ToListAsync(cancellationToken);
    }

    public async Task<PriceBarDto?> GetLatestBarAsync(
        string symbol,
        Timeframe timeframe,
        CancellationToken cancellationToken = default)
    {
        if (timeframe != Timeframe.Daily)
        {
            return null;
        }

        var sym = await _context.Symbols
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Ticker == symbol.ToUpper(), cancellationToken);

        if (sym == null)
        {
            return null;
        }

        var latest = await _context.PriceBars
            .AsNoTracking()
            .Where(p => p.SymbolId == sym.Id && p.Timeframe == timeframe)
            .OrderByDescending(p => p.Timestamp)
            .FirstOrDefaultAsync(cancellationToken);

        if (latest == null)
        {
            return null;
        }

        return new PriceBarDto(
            sym.Ticker,
            latest.Timeframe,
            latest.Timestamp,
            latest.Open,
            latest.High,
            latest.Low,
            latest.Close,
            latest.Volume,
            latest.AdjustedClose);
    }

    public async Task<MarketDataImport?> GetLatestImportAsync(CancellationToken cancellationToken = default)
    {
        return await _context.MarketDataImports
            .OrderByDescending(i => i.SessionDate)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<MarketDataImport>> GetRecentImportsAsync(int count = 30, CancellationToken cancellationToken = default)
    {
        return await _context.MarketDataImports
            .OrderByDescending(i => i.SessionDate)
            .Take(count)
            .ToListAsync(cancellationToken);
    }

    public async Task<MarketDataImport> ImportBulletinForDateAsync(DateOnly date, bool force = false, CancellationToken cancellationToken = default)
    {
        // Verify calendar: if date is a closed holiday or weekend, do not fetch
        if (!_sessionCalendar.IsTradingDay(date))
        {
            _logger.LogInformation("Date {Date} is not a trading session according to calendar. Skipping bulletin download.", date);
            var skippedImport = new MarketDataImport
            {
                Provider = Capabilities.ProviderName,
                SessionDate = date,
                SourceFileName = $"BUL_{date:yyyyMMdd}.csv",
                Status = MarketDataImportStatus.Skipped,
                ErrorMessage = "Non-trading day according to BIST market calendar"
            };
            return skippedImport;
        }

        var fileName = $"BUL_{date:yyyyMMdd}.csv";
        var zipFileName = $"BUL_{date:yyyyMMdd}.zip";
        var localCsvPath = Path.Combine(_storagePath, fileName);
        var localZipPath = Path.Combine(_storagePath, zipFileName);

        byte[]? rawBytes = null;
        string sourceUrl = $"{_baseUrl.TrimEnd('/')}/{fileName}";

        // Check local disk first if exists
        if (File.Exists(localCsvPath))
        {
            rawBytes = await File.ReadAllBytesAsync(localCsvPath, cancellationToken);
            sourceUrl = localCsvPath;
        }
        else if (File.Exists(localZipPath))
        {
            rawBytes = await File.ReadAllBytesAsync(localZipPath, cancellationToken);
            sourceUrl = localZipPath;
            fileName = zipFileName;
        }
        else
        {
            // Download from official Borsa Istanbul URL
            try
            {
                _logger.LogInformation("Attempting bulletin download from {Url}", sourceUrl);
                using var response = await _httpClient.GetAsync(sourceUrl, HttpCompletionOption.ResponseContentRead, cancellationToken);

                if (response.IsSuccessStatusCode)
                {
                    rawBytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
                }
                else
                {
                    // Attempt downloading .zip variant if .csv failed
                    var zipUrl = $"{_baseUrl.TrimEnd('/')}/{zipFileName}";
                    _logger.LogInformation("Attempting zip variant download from {Url}", zipUrl);
                    using var zipResponse = await _httpClient.GetAsync(zipUrl, HttpCompletionOption.ResponseContentRead, cancellationToken);
                    if (zipResponse.IsSuccessStatusCode)
                    {
                        rawBytes = await zipResponse.Content.ReadAsByteArrayAsync(cancellationToken);
                        fileName = zipFileName;
                        sourceUrl = zipUrl;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to download bulletin from web for date {Date}", date);
            }
        }

        if (rawBytes == null || rawBytes.Length == 0)
        {
            _logger.LogWarning("No bulletin data could be retrieved for date {Date}", date);
            var failedImport = new MarketDataImport
            {
                Provider = Capabilities.ProviderName,
                SessionDate = date,
                SourceFileName = fileName,
                SourceUrl = sourceUrl,
                Status = MarketDataImportStatus.Failed,
                ErrorMessage = $"Bulletin file not available for date {date}"
            };
            return failedImport;
        }

        using var memoryStream = new MemoryStream(rawBytes);
        return await ProcessBulletinDataAsync(date, fileName, sourceUrl, memoryStream, rawBytes, force, cancellationToken);
    }

    public async Task<MarketDataImport> ImportBulletinStreamAsync(
        DateOnly? dateHint,
        string fileName,
        Stream contentStream,
        CancellationToken cancellationToken = default)
    {
        using var memoryStream = new MemoryStream();
        await contentStream.CopyToAsync(memoryStream, cancellationToken);
        var rawBytes = memoryStream.ToArray();
        memoryStream.Position = 0;

        return await ProcessBulletinDataAsync(dateHint, fileName, "upload", memoryStream, rawBytes, force: true, cancellationToken);
    }

    private async Task<MarketDataImport> ProcessBulletinDataAsync(
        DateOnly? targetDate,
        string fileName,
        string sourceUrl,
        Stream stream,
        byte[] rawBytes,
        bool force,
        CancellationToken cancellationToken)
    {
        var sha256 = Convert.ToHexString(SHA256.HashData(rawBytes)).ToLowerInvariant();

        // If file is zip, extract the CSV
        Stream csvStream = stream;
        MemoryStream? decompressedStream = null;

        if (fileName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
        {
            decompressedStream = ExtractCsvFromZip(rawBytes);
            if (decompressedStream == null)
            {
                var errImport = new MarketDataImport
                {
                    Provider = Capabilities.ProviderName,
                    SessionDate = targetDate ?? DateOnly.FromDateTime(DateTime.UtcNow),
                    SourceFileName = fileName,
                    SourceUrl = sourceUrl,
                    Sha256 = sha256,
                    ContentLength = rawBytes.Length,
                    Status = MarketDataImportStatus.Failed,
                    ErrorMessage = "No valid BUL_*.csv found inside the uploaded zip archive."
                };
                _context.MarketDataImports.Add(errImport);
                await _context.SaveChangesAsync(cancellationToken);
                return errImport;
            }
            csvStream = decompressedStream;
        }

        // Parse CSV
        csvStream.Position = 0;
        var parseResult = _parser.Parse(csvStream);

        var sessionDate = targetDate ?? parseResult.SessionDate;
        if (sessionDate == null)
        {
            var errImport = new MarketDataImport
            {
                Provider = Capabilities.ProviderName,
                SessionDate = DateOnly.FromDateTime(DateTime.UtcNow),
                SourceFileName = fileName,
                SourceUrl = sourceUrl,
                Sha256 = sha256,
                ContentLength = rawBytes.Length,
                Status = MarketDataImportStatus.Failed,
                ErrorMessage = "Could not detect session date from bulletin."
            };
            _context.MarketDataImports.Add(errImport);
            await _context.SaveChangesAsync(cancellationToken);
            return errImport;
        }

        // Check for existing import (Idempotency)
        var existingImport = await _context.MarketDataImports
            .FirstOrDefaultAsync(i => i.SessionDate == sessionDate.Value, cancellationToken);

        if (existingImport != null && existingImport.Sha256 == sha256 && existingImport.Status == MarketDataImportStatus.Success && !force)
        {
            _logger.LogInformation(
                "Bulletin for {Date} already imported with matching SHA256 {Sha256}. Skipping re-processing (0 duplicate bars).",
                sessionDate.Value, sha256);
            return existingImport;
        }

        // Save raw file to local storage directory for auditing & replay
        try
        {
            var targetRawFile = Path.Combine(_storagePath, fileName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) ? fileName : $"BUL_{sessionDate.Value:yyyyMMdd}.csv");
            await File.WriteAllBytesAsync(targetRawFile, rawBytes, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not save bulletin file copy to storage path {Path}", _storagePath);
        }

        var import = existingImport ?? new MarketDataImport
        {
            Provider = Capabilities.ProviderName,
            SessionDate = sessionDate.Value,
        };

        import.SourceFileName = fileName;
        import.SourceUrl = sourceUrl;
        import.Sha256 = sha256;
        import.ContentLength = rawBytes.Length;
        import.DownloadedAt = DateTime.UtcNow;
        import.Status = MarketDataImportStatus.Processing;
        import.RowsRead = parseResult.TotalRowsRead;
        import.RowsAccepted = parseResult.AcceptedRows;
        import.RowsRejected = parseResult.RejectedRows;

        if (existingImport == null)
        {
            _context.MarketDataImports.Add(import);
        }

        await _context.SaveChangesAsync(cancellationToken);

        // Process records
        int barsInserted = 0;
        int barsUpdated = 0;

        var barTimestampUtc = sessionDate.Value.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);

        // Load existing symbols into memory cache
        var allSymbols = await _context.Symbols.ToListAsync(cancellationToken);
        var symbolMap = allSymbols.ToDictionary(s => s.Ticker, s => s, StringComparer.OrdinalIgnoreCase);

        // Load existing daily stats for this session
        var existingStats = await _context.DailyInstrumentMarketStats
            .Where(s => s.SessionDate == sessionDate.Value)
            .ToDictionaryAsync(s => s.SymbolId, cancellationToken);

        // Load existing price bars for this session
        var existingBars = await _context.PriceBars
            .Where(b => b.Timeframe == Timeframe.Daily && b.Timestamp == barTimestampUtc)
            .ToDictionaryAsync(b => b.SymbolId, cancellationToken);

        var newSymbols = new List<Symbol>();
        var newStats = new List<DailyInstrumentMarketStats>();
        var newBars = new List<PriceBar>();

        // Resolve default market ID (BIST)
        var defaultMarketId = await _context.Markets
            .Select(m => m.Id)
            .FirstOrDefaultAsync(cancellationToken);
        if (defaultMarketId == 0) defaultMarketId = 1;

        foreach (var record in parseResult.Records)
        {
            if (!symbolMap.TryGetValue(record.Ticker, out var symbol))
            {
                symbol = new Symbol
                {
                    MarketId = defaultMarketId,
                    Ticker = record.Ticker,
                    Name = string.IsNullOrWhiteSpace(record.InstrumentName) ? record.Ticker : record.InstrumentName,
                    Sector = record.MarketSegment ?? "BIST",
                    Industry = "Equity",
                    IsActive = !record.Suspended
                };
                newSymbols.Add(symbol);
                symbolMap[record.Ticker] = symbol;
            }
            else
            {
                // Update symbol info
                if (!string.IsNullOrWhiteSpace(record.InstrumentName) && symbol.Name != record.InstrumentName)
                {
                    symbol.Name = record.InstrumentName;
                }
                symbol.IsActive = !record.Suspended;
            }
        }

        if (newSymbols.Any())
        {
            _context.Symbols.AddRange(newSymbols);
            await _context.SaveChangesAsync(cancellationToken);
        }

        // Apply Corporate Action adjustments
        await _corporateActionService.ProcessCorporateActionsAsync(sessionDate.Value, parseResult.Records, cancellationToken);

        foreach (var record in parseResult.Records)
        {
            var symbol = symbolMap[record.Ticker];

            // 1. DailyInstrumentMarketStats
            if (!existingStats.TryGetValue(symbol.Id, out var stats))
            {
                stats = new DailyInstrumentMarketStats
                {
                    SymbolId = symbol.Id,
                    SessionDate = sessionDate.Value,
                    PreviousLastPrice = record.PreviousLastPrice,
                    ClosingSessionPrice = record.ClosingSessionPrice,
                    ChangePercent = record.ChangePercent,
                    Vwap = record.Vwap,
                    TotalTradedValue = record.TotalTradedValue,
                    TotalTradedVolume = record.TotalTradedVolume,
                    TotalNumberOfContracts = record.TotalNumberOfContracts,
                    Suspended = record.Suspended,
                    CorporateActionRaw = record.CorporateAction,
                    MarketSegment = record.MarketSegment,
                    TradingMethod = record.TradingMethod,
                    SourceImportId = import.Id
                };
                newStats.Add(stats);
            }
            else
            {
                stats.PreviousLastPrice = record.PreviousLastPrice;
                stats.ClosingSessionPrice = record.ClosingSessionPrice;
                stats.ChangePercent = record.ChangePercent;
                stats.Vwap = record.Vwap;
                stats.TotalTradedValue = record.TotalTradedValue;
                stats.TotalTradedVolume = record.TotalTradedVolume;
                stats.TotalNumberOfContracts = record.TotalNumberOfContracts;
                stats.Suspended = record.Suspended;
                stats.CorporateActionRaw = record.CorporateAction;
                stats.MarketSegment = record.MarketSegment;
                stats.TradingMethod = record.TradingMethod;
                stats.SourceImportId = import.Id;
            }

            // 2. PriceBars (Strict OHLC: only if valid trading occurred)
            if (record.HasValidOhlc)
            {
                if (!existingBars.TryGetValue(symbol.Id, out var bar))
                {
                    bar = new PriceBar
                    {
                        SymbolId = symbol.Id,
                        Timeframe = Timeframe.Daily,
                        Timestamp = barTimestampUtc,
                        Open = record.Open!.Value,
                        High = record.High!.Value,
                        Low = record.Low!.Value,
                        Close = record.Close!.Value,
                        Volume = record.TotalTradedVolume ?? 0,
                        AdjustedClose = record.Close!.Value
                    };
                    newBars.Add(bar);
                    barsInserted++;
                }
                else
                {
                    bar.Open = record.Open!.Value;
                    bar.High = record.High!.Value;
                    bar.Low = record.Low!.Value;
                    bar.Close = record.Close!.Value;
                    bar.Volume = record.TotalTradedVolume ?? 0;
                    bar.AdjustedClose = record.Close!.Value;
                    barsUpdated++;
                }
            }
        }

        if (newStats.Any())
        {
            _context.DailyInstrumentMarketStats.AddRange(newStats);
        }

        if (newBars.Any())
        {
            _context.PriceBars.AddRange(newBars);
        }

        import.Status = MarketDataImportStatus.Success;
        import.PriceBarsInserted = barsInserted;
        import.PriceBarsUpdated = barsUpdated;

        await _context.SaveChangesAsync(cancellationToken);

        decompressedStream?.Dispose();

        _logger.LogInformation(
            "Successfully imported BIST bulletin for {Date}. Accepted rows: {Accepted}, Bars inserted: {Inserted}, Bars updated: {Updated}",
            sessionDate.Value, parseResult.AcceptedRows, barsInserted, barsUpdated);

        return import;
    }

    private static MemoryStream? ExtractCsvFromZip(byte[] zipBytes)
    {
        using var zipStream = new MemoryStream(zipBytes);
        using var archive = new ZipArchive(zipStream, ZipArchiveMode.Read);

        // Security check: limit number of entries to prevent zip bombs
        if (archive.Entries.Count > 20)
        {
            return null;
        }

        foreach (var entry in archive.Entries)
        {
            // Security check: limit uncompressed entry size (50MB max)
            if (entry.Length > 50 * 1024 * 1024)
            {
                return null;
            }

            if (entry.FullName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
            {
                var ms = new MemoryStream();
                using var entryStream = entry.Open();
                entryStream.CopyTo(ms);
                ms.Position = 0;
                return ms;
            }
        }

        return null;
    }
}
