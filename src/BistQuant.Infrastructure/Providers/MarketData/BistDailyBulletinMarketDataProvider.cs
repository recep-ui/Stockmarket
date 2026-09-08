using System.Diagnostics;
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
    private readonly IMarketSessionDateResolver _sessionDateResolver;
    private readonly string _storagePath;
    private readonly string _verifiedEndpointTemplate;
    private readonly bool _autoDownloadEnabled;

    public MarketDataProviderCapabilities Capabilities => new(
        ProviderName: "Borsa İstanbul Pay Piyasası Günlük Bülten",
        SupportsRealtime: false,
        SupportsEndOfWeekOrDay: true,
        SupportedTimeframes: new[] { Timeframe.Daily },
        RequiresSessionClosure: true,
        Description: "Official Borsa İstanbul Daily Bulletin (thb<YYYYMMDD>1.zip). Zero-cost official EOD feed."
    );

    public BistDailyBulletinMarketDataProvider(
        IApplicationDbContext context,
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<BistDailyBulletinMarketDataProvider> logger,
        BistDailyBulletinParser parser,
        ICorporateActionAdjustmentService corporateActionService,
        IMarketSessionCalendar sessionCalendar,
        IMarketSessionDateResolver sessionDateResolver)
    {
        _context = context;
        _httpClient = httpClient;
        _configuration = configuration;
        _logger = logger;
        _parser = parser;
        _corporateActionService = corporateActionService;
        _sessionCalendar = sessionCalendar;
        _sessionDateResolver = sessionDateResolver;

        _verifiedEndpointTemplate = _configuration["BistBulletin:VerifiedDownloadEndpoint"]
            ?? "https://www.borsaistanbul.com/data/thb/{YYYY}/{MM}/thb{YYYY}{MM}{DD}1.zip";
        _autoDownloadEnabled = _configuration.GetValue<bool?>("BistBulletin:AutomaticDownloadEnabled") ?? true;

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
            .Where(i => i.Provider == Capabilities.ProviderName)
            .OrderByDescending(i => i.SessionDate)
            .ThenByDescending(i => i.RevisionNumber)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<MarketDataImport>> GetRecentImportsAsync(int count = 30, CancellationToken cancellationToken = default)
    {
        return await _context.MarketDataImports
            .Where(i => i.Provider == Capabilities.ProviderName)
            .OrderByDescending(i => i.SessionDate)
            .ThenByDescending(i => i.RevisionNumber)
            .Take(count)
            .ToListAsync(cancellationToken);
    }

    public async Task<BulletinDownloadResult> DownloadBulletinForDateAsync(DateOnly date, CancellationToken cancellationToken = default)
    {
        if (!_sessionCalendar.IsTradingDay(date))
        {
            _logger.LogInformation("Date {Date} is not a trading session according to calendar. Skipping bulletin download.", date);
            return BulletinDownloadResult.Failed(date, 0, "Non-trading day according to BIST market calendar");
        }

        if (!_autoDownloadEnabled || string.IsNullOrWhiteSpace(_verifiedEndpointTemplate))
        {
            _logger.LogWarning("Automatic bulletin download is disabled or not configured.");
            var attempt = new BulletinFetchAttempt
            {
                SessionDate = date,
                SourceUrl = "disabled",
                AttemptedAtUtc = DateTime.UtcNow,
                Status = BulletinDownloadStatus.AutomaticDownloadUnavailable,
                ErrorMessage = "Automatic download is disabled or unconfigured."
            };
            _context.BulletinFetchAttempts.Add(attempt);
            await _context.SaveChangesAsync(cancellationToken);
            return BulletinDownloadResult.AutomaticDownloadUnavailable(date, "Automatic bulletin download is not configured or disabled. Use manual upload or configure BistBulletin:VerifiedDownloadEndpoint.");
        }

        var turkeyTz = _sessionCalendar.MarketTimeZone;
        var localNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, turkeyTz);
        var todayTurkey = DateOnly.FromDateTime(localNow);

        if (date > todayTurkey)
        {
            return BulletinDownloadResult.Failed(date, 0, $"Requested date {date:yyyy-MM-dd} is in the future.");
        }

        if (date == todayTurkey)
        {
            var pubTimeUtc = _sessionCalendar.GetBulletinPublicationTimeUtc(date);
            if (DateTime.UtcNow < pubTimeUtc)
            {
                _logger.LogInformation("Bulletin publication window for {Date} not reached yet (Scheduled: {PubTime:HH:mm} UTC).", date, pubTimeUtc);
                return BulletinDownloadResult.NotPublishedYet(date, 0, $"Bulletin publication time ({pubTimeUtc:HH:mm} UTC) not reached yet.");
            }
        }

        // Check persistent safe backoff
        var previousImport = await _context.MarketDataImports
            .Where(i => i.Provider == Capabilities.ProviderName && i.SessionDate == date)
            .OrderByDescending(i => i.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (previousImport?.NextAttemptAt.HasValue == true && DateTime.UtcNow < previousImport.NextAttemptAt.Value)
        {
            _logger.LogInformation("Safe backoff active until {NextAttempt} for session {Date}. Skipping network call.", previousImport.NextAttemptAt.Value, date);
            return BulletinDownloadResult.NotPublishedYet(date, previousImport.LastHttpStatus ?? 404, $"Safe backoff active until {previousImport.NextAttemptAt:yyyy-MM-dd HH:mm:ss} UTC (attempt count: {previousImport.AttemptCount}).");
        }

        var url = _verifiedEndpointTemplate
            .Replace("{YYYY}", date.ToString("yyyy"))
            .Replace("{MM}", date.ToString("MM"))
            .Replace("{DD}", date.ToString("dd"));

        var sw = Stopwatch.StartNew();
        var fetchAttempt = new BulletinFetchAttempt
        {
            SessionDate = date,
            SourceUrl = url,
            AttemptedAtUtc = DateTime.UtcNow
        };

        HttpResponseMessage response;
        try
        {
            _logger.LogInformation("Attempting bulletin download from verified endpoint: {Url}", url);
            response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseContentRead, cancellationToken);
        }
        catch (Exception ex)
        {
            sw.Stop();
            fetchAttempt.ElapsedMs = sw.ElapsedMilliseconds;
            fetchAttempt.Status = BulletinDownloadStatus.ProviderUnavailable;
            fetchAttempt.ErrorMessage = $"Network error: {ex.Message}";
            _context.BulletinFetchAttempts.Add(fetchAttempt);

            await UpdateBackoffStateAsync(date, previousImport, fetchAttempt.Status, 0, ex.Message, cancellationToken);
            return BulletinDownloadResult.ProviderUnavailable(date, 0, ex.Message);
        }

        sw.Stop();
        fetchAttempt.ElapsedMs = sw.ElapsedMilliseconds;
        fetchAttempt.HttpStatus = (int)response.StatusCode;

        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            fetchAttempt.Status = BulletinDownloadStatus.NotPublishedYet;
            fetchAttempt.ErrorMessage = "HTTP 404 - Bulletin not published yet.";
            _context.BulletinFetchAttempts.Add(fetchAttempt);

            await UpdateBackoffStateAsync(date, previousImport, fetchAttempt.Status, 404, fetchAttempt.ErrorMessage, cancellationToken);
            return BulletinDownloadResult.NotPublishedYet(date, 404, $"HTTP 404 - Bulletin for session {date:yyyy-MM-dd} is not published yet.");
        }

        if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
        {
            fetchAttempt.Status = BulletinDownloadStatus.RateLimited;
            fetchAttempt.ErrorMessage = "HTTP 429 - Rate limited by BIST provider.";
            _context.BulletinFetchAttempts.Add(fetchAttempt);

            await UpdateBackoffStateAsync(date, previousImport, fetchAttempt.Status, 429, fetchAttempt.ErrorMessage, cancellationToken);
            return BulletinDownloadResult.RateLimited(date, 429, "HTTP 429 - Rate limited by provider.");
        }

        if ((int)response.StatusCode >= 500)
        {
            fetchAttempt.Status = BulletinDownloadStatus.ProviderUnavailable;
            fetchAttempt.ErrorMessage = $"HTTP {(int)response.StatusCode} - Provider server error.";
            _context.BulletinFetchAttempts.Add(fetchAttempt);

            await UpdateBackoffStateAsync(date, previousImport, fetchAttempt.Status, (int)response.StatusCode, fetchAttempt.ErrorMessage, cancellationToken);
            return BulletinDownloadResult.ProviderUnavailable(date, (int)response.StatusCode, $"Provider returned HTTP {(int)response.StatusCode}.");
        }

        if (!response.IsSuccessStatusCode)
        {
            fetchAttempt.Status = BulletinDownloadStatus.Failed;
            fetchAttempt.ErrorMessage = $"HTTP {(int)response.StatusCode} - Unexpected status code.";
            _context.BulletinFetchAttempts.Add(fetchAttempt);

            await UpdateBackoffStateAsync(date, previousImport, fetchAttempt.Status, (int)response.StatusCode, fetchAttempt.ErrorMessage, cancellationToken);
            return BulletinDownloadResult.Failed(date, (int)response.StatusCode, $"Unexpected HTTP {(int)response.StatusCode}.");
        }

        var rawBytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
        fetchAttempt.ContentLength = rawBytes.Length;

        if (rawBytes.Length == 0)
        {
            fetchAttempt.Status = BulletinDownloadStatus.InvalidSourceContent;
            fetchAttempt.ErrorMessage = "Received empty response from server.";
            _context.BulletinFetchAttempts.Add(fetchAttempt);
            await UpdateBackoffStateAsync(date, previousImport, fetchAttempt.Status, 200, fetchAttempt.ErrorMessage, cancellationToken);
            return BulletinDownloadResult.InvalidSourceContent(date, 200, "Empty payload received.");
        }

        // Validate content: not HTML
        var preview = Encoding.UTF8.GetString(rawBytes.Take(Math.Min(rawBytes.Length, 256)).ToArray()).TrimStart();
        if (preview.StartsWith("<!DOCTYPE", StringComparison.OrdinalIgnoreCase) ||
            preview.StartsWith("<html", StringComparison.OrdinalIgnoreCase) ||
            preview.StartsWith("<?xml", StringComparison.OrdinalIgnoreCase))
        {
            fetchAttempt.Status = BulletinDownloadStatus.InvalidSourceContent;
            fetchAttempt.ErrorMessage = "Received HTML/XML content instead of bulletin ZIP/CSV.";
            _context.BulletinFetchAttempts.Add(fetchAttempt);
            await UpdateBackoffStateAsync(date, previousImport, fetchAttempt.Status, 200, fetchAttempt.ErrorMessage, cancellationToken);
            return BulletinDownloadResult.InvalidSourceContent(date, 200, "Received HTML/XML response page instead of valid bulletin data.");
        }

        byte[]? extractedCsvBytes = null;
        bool isZip = url.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) ||
                     (rawBytes.Length > 4 && rawBytes[0] == 0x50 && rawBytes[1] == 0x4B);

        if (isZip)
        {
            using var csvMs = ExtractCsvFromZip(rawBytes);
            if (csvMs == null)
            {
                fetchAttempt.Status = BulletinDownloadStatus.InvalidSourceContent;
                fetchAttempt.ErrorMessage = "Could not extract valid CSV from ZIP archive.";
                _context.BulletinFetchAttempts.Add(fetchAttempt);
                await UpdateBackoffStateAsync(date, previousImport, fetchAttempt.Status, 200, fetchAttempt.ErrorMessage, cancellationToken);
                return BulletinDownloadResult.InvalidSourceContent(date, 200, "ZIP archive did not contain a valid CSV bulletin file.");
            }
            extractedCsvBytes = csvMs.ToArray();
        }
        else
        {
            extractedCsvBytes = rawBytes;
        }

        var sha256 = Convert.ToHexString(SHA256.HashData(rawBytes)).ToLowerInvariant();
        fetchAttempt.Sha256 = sha256;
        fetchAttempt.Status = BulletinDownloadStatus.Success;
        _context.BulletinFetchAttempts.Add(fetchAttempt);
        await _context.SaveChangesAsync(cancellationToken);

        var fileName = Path.GetFileName(url);
        return BulletinDownloadResult.SuccessResult(date, rawBytes, sha256, fileName, url, extractedCsvBytes);
    }

    private async Task UpdateBackoffStateAsync(
        DateOnly date,
        MarketDataImport? existingImport,
        BulletinDownloadStatus status,
        int httpStatus,
        string errorMessage,
        CancellationToken cancellationToken)
    {
        var targetImport = existingImport ?? new MarketDataImport
        {
            Provider = Capabilities.ProviderName,
            SessionDate = date,
            SourceFileName = $"thb{date:yyyyMMdd}1.zip",
            SourceUrl = _verifiedEndpointTemplate
                .Replace("{YYYY}", date.ToString("yyyy"))
                .Replace("{MM}", date.ToString("MM"))
                .Replace("{DD}", date.ToString("dd")),
            Status = MarketDataImportStatus.Failed
        };

        targetImport.LastHttpStatus = httpStatus;
        targetImport.DownloadStatus = status;
        targetImport.ErrorMessage = errorMessage;
        targetImport.LastAttemptAt = DateTime.UtcNow;
        targetImport.AttemptCount++;

        var backoffMinutes = Math.Min(30, 5 * targetImport.AttemptCount);
        targetImport.NextAttemptAt = DateTime.UtcNow.AddMinutes(backoffMinutes);

        if (existingImport == null)
        {
            _context.MarketDataImports.Add(targetImport);
        }

        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<MarketDataImport> ImportBulletinForDateAsync(DateOnly date, bool force = false, CancellationToken cancellationToken = default)
    {
        if (!_sessionCalendar.IsTradingDay(date))
        {
            _logger.LogInformation("Date {Date} is not a trading session according to calendar. Skipping bulletin download.", date);
            return new MarketDataImport
            {
                Provider = Capabilities.ProviderName,
                SessionDate = date,
                SourceFileName = $"thb{date:yyyyMMdd}1.zip",
                Status = MarketDataImportStatus.Skipped,
                ErrorMessage = "Non-trading day according to BIST market calendar"
            };
        }

        // Check local disk first
        var candidateFiles = new[]
        {
            Path.Combine(_storagePath, $"thb{date:yyyyMMdd}1.zip"),
            Path.Combine(_storagePath, $"thb{date:yyyyMMdd}1.csv"),
            Path.Combine(_storagePath, $"BUL_{date:yyyyMMdd}.zip"),
            Path.Combine(_storagePath, $"BUL_{date:yyyyMMdd}.csv")
        };

        foreach (var localPath in candidateFiles)
        {
            if (File.Exists(localPath))
            {
                _logger.LogInformation("Found local bulletin file at {Path}. Processing...", localPath);
                var localBytes = await File.ReadAllBytesAsync(localPath, cancellationToken);
                return await ProcessBulletinDataAsync(date, Path.GetFileName(localPath), localPath, new MemoryStream(localBytes), localBytes, force, cancellationToken);
            }
        }

        // Download via official endpoint
        var downloadResult = await DownloadBulletinForDateAsync(date, cancellationToken);
        if (downloadResult.Status != BulletinDownloadStatus.Success)
        {
            var existing = await _context.MarketDataImports
                .Where(i => i.Provider == Capabilities.ProviderName && i.SessionDate == date)
                .OrderByDescending(i => i.Id)
                .FirstOrDefaultAsync(cancellationToken);

            if (existing != null)
            {
                return existing;
            }

            var failedImport = new MarketDataImport
            {
                Provider = Capabilities.ProviderName,
                SessionDate = date,
                SourceFileName = $"thb{date:yyyyMMdd}1.zip",
                SourceUrl = _verifiedEndpointTemplate
                    .Replace("{YYYY}", date.ToString("yyyy"))
                    .Replace("{MM}", date.ToString("MM"))
                    .Replace("{DD}", date.ToString("dd")),
                Status = MarketDataImportStatus.Failed,
                DownloadStatus = downloadResult.Status,
                LastHttpStatus = downloadResult.HttpStatus,
                ErrorMessage = downloadResult.ErrorMessage
            };
            return failedImport;
        }

        // Save local copy
        try
        {
            var savePath = Path.Combine(_storagePath, downloadResult.FileName!);
            await File.WriteAllBytesAsync(savePath, downloadResult.RawBytes!, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not save bulletin file copy to storage path {Path}", _storagePath);
        }

        return await ProcessBulletinDataAsync(
            date,
            downloadResult.FileName!,
            downloadResult.SourceUrl!,
            new MemoryStream(downloadResult.RawBytes!),
            downloadResult.RawBytes!,
            force,
            cancellationToken);
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

        Stream csvStream = stream;
        MemoryStream? decompressedStream = null;

        if (fileName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) ||
            (rawBytes.Length > 4 && rawBytes[0] == 0x50 && rawBytes[1] == 0x4B))
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
                    ErrorMessage = "No valid CSV found inside zip archive."
                };
                _context.MarketDataImports.Add(errImport);
                await _context.SaveChangesAsync(cancellationToken);
                return errImport;
            }
            csvStream = decompressedStream;
        }

        csvStream.Position = 0;
        var parseResult = _parser.Parse(csvStream, targetDate);

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

        if (parseResult.Errors.Any(e => e.Contains("date mismatch", StringComparison.OrdinalIgnoreCase)))
        {
            _logger.LogError("Bulletin date mismatch for target date {TargetDate}: {Errors}",
                sessionDate.Value, string.Join("; ", parseResult.Errors));

            var dateMismatchImport = new MarketDataImport
            {
                Provider = Capabilities.ProviderName,
                SessionDate = sessionDate.Value,
                SourceFileName = fileName,
                SourceUrl = sourceUrl,
                Sha256 = sha256,
                ContentLength = rawBytes.Length,
                Status = MarketDataImportStatus.DateMismatch,
                ErrorMessage = string.Join("; ", parseResult.Errors)
            };
            _context.MarketDataImports.Add(dateMismatchImport);
            await _context.SaveChangesAsync(cancellationToken);
            return dateMismatchImport;
        }

        if (parseResult.AcceptedRows == 0 && parseResult.Errors.Count > 0)
        {
            var failedSchemaImport = new MarketDataImport
            {
                Provider = Capabilities.ProviderName,
                SessionDate = sessionDate.Value,
                SourceFileName = fileName,
                SourceUrl = sourceUrl,
                Sha256 = sha256,
                ContentLength = rawBytes.Length,
                Status = MarketDataImportStatus.Failed,
                ErrorMessage = $"Parsing failed: {string.Join("; ", parseResult.Errors.Take(3))}"
            };
            _context.MarketDataImports.Add(failedSchemaImport);
            await _context.SaveChangesAsync(cancellationToken);
            return failedSchemaImport;
        }

        // True Revision Audit
        var existingImports = await _context.MarketDataImports
            .Where(i => i.Provider == Capabilities.ProviderName && i.SessionDate == sessionDate.Value)
            .OrderBy(i => i.RevisionNumber)
            .ToListAsync(cancellationToken);

        var currentImport = existingImports.FirstOrDefault(i => i.IsCurrent);

        if (currentImport != null && currentImport.Sha256 == sha256 && currentImport.Status == MarketDataImportStatus.Success && !force)
        {
            _logger.LogInformation(
                "Bulletin for {Date} already imported with matching SHA256 {Sha256} (Rev #{Rev}). Skipping re-processing (0 duplicate bars).",
                sessionDate.Value, sha256, currentImport.RevisionNumber);
            return currentImport;
        }

        var exactShaMatch = existingImports.FirstOrDefault(i => i.Sha256 == sha256);
        if (exactShaMatch != null && exactShaMatch.Status == MarketDataImportStatus.Success && !force)
        {
            _logger.LogInformation(
                "Bulletin for {Date} already contains revision with SHA256 {Sha256} (Rev #{Rev}). Skipping re-processing.",
                sessionDate.Value, sha256, exactShaMatch.RevisionNumber);
            return exactShaMatch;
        }

        MarketDataImport import;
        if (currentImport != null && currentImport.Sha256 != sha256)
        {
            _logger.LogInformation(
                "Bulletin REVISION detected for session {Date}. Old SHA: {OldSha}, New SHA: {NewSha}. Creating Revision #{Rev}.",
                sessionDate.Value, currentImport.Sha256, sha256, currentImport.RevisionNumber + 1);

            currentImport.IsCurrent = false;

            int nextRev = existingImports.Max(i => i.RevisionNumber) + 1;
            import = new MarketDataImport
            {
                Provider = Capabilities.ProviderName,
                SessionDate = sessionDate.Value,
                SourceFileName = fileName,
                SourceUrl = sourceUrl,
                Sha256 = sha256,
                ContentLength = rawBytes.Length,
                DownloadedAt = DateTime.UtcNow,
                RevisionNumber = nextRev,
                IsRevision = true,
                SupersedesImportId = currentImport.Id,
                IsCurrent = true,
                Status = MarketDataImportStatus.Processing,
                RowsRead = parseResult.TotalRowsRead,
                RowsAccepted = parseResult.AcceptedRows,
                RowsRejected = parseResult.RejectedRows
            };
            _context.MarketDataImports.Add(import);
        }
        else if (currentImport != null && force)
        {
            import = currentImport;
            import.Status = MarketDataImportStatus.Processing;
            import.DownloadedAt = DateTime.UtcNow;
            import.RowsRead = parseResult.TotalRowsRead;
            import.RowsAccepted = parseResult.AcceptedRows;
            import.RowsRejected = parseResult.RejectedRows;
        }
        else
        {
            import = new MarketDataImport
            {
                Provider = Capabilities.ProviderName,
                SessionDate = sessionDate.Value,
                SourceFileName = fileName,
                SourceUrl = sourceUrl,
                Sha256 = sha256,
                ContentLength = rawBytes.Length,
                DownloadedAt = DateTime.UtcNow,
                RevisionNumber = 1,
                IsRevision = false,
                IsCurrent = true,
                Status = MarketDataImportStatus.Processing,
                RowsRead = parseResult.TotalRowsRead,
                RowsAccepted = parseResult.AcceptedRows,
                RowsRejected = parseResult.RejectedRows
            };
            _context.MarketDataImports.Add(import);
        }

        await _context.SaveChangesAsync(cancellationToken);

        // Transactional Database Import
        var dbContext = _context as DbContext;
        var executionStrategy = dbContext?.Database.CreateExecutionStrategy();

        int barsInserted = 0;
        int barsUpdated = 0;

        async Task ExecuteImportTransactionAsync()
        {
            Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction? transaction = null;
            if (dbContext != null && dbContext.Database.IsRelational())
            {
                transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
            }

            try
            {
                var barTimestampUtc = _sessionDateResolver.ToDailyBarTimestampUtc(sessionDate.Value);

                var allSymbols = await _context.Symbols.ToListAsync(cancellationToken);
                var symbolMap = allSymbols.ToDictionary(s => s.Ticker, s => s, StringComparer.OrdinalIgnoreCase);

                var existingStats = await _context.DailyInstrumentMarketStats
                    .Where(s => s.SessionDate == sessionDate.Value)
                    .ToDictionaryAsync(s => s.SymbolId, cancellationToken);

                var existingBars = await _context.PriceBars
                    .Where(b => b.Timeframe == Timeframe.Daily && b.Timestamp == barTimestampUtc)
                    .ToDictionaryAsync(b => b.SymbolId, cancellationToken);

                var newSymbols = new List<Symbol>();
                var newStats = new List<DailyInstrumentMarketStats>();
                var newBars = new List<PriceBar>();

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
                            IsActive = true,
                            LastSeenInBulletinDate = sessionDate.Value
                        };
                        newSymbols.Add(symbol);
                        symbolMap[record.Ticker] = symbol;
                    }
                    else
                    {
                        if (!string.IsNullOrWhiteSpace(record.InstrumentName) && symbol.Name != record.InstrumentName)
                        {
                            symbol.Name = record.InstrumentName;
                        }
                        symbol.LastSeenInBulletinDate = sessionDate.Value;
                    }
                }

                if (newSymbols.Count > 0)
                {
                    _context.Symbols.AddRange(newSymbols);
                    await _context.SaveChangesAsync(cancellationToken);
                }

                await _corporateActionService.ProcessCorporateActionsAsync(sessionDate.Value, parseResult.Records, cancellationToken);

                foreach (var record in parseResult.Records)
                {
                    var symbol = symbolMap[record.Ticker];

                    // 1. Stats
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

                    // 2. Strict OHLC PriceBar
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

                if (newStats.Count > 0)
                {
                    _context.DailyInstrumentMarketStats.AddRange(newStats);
                }

                if (newBars.Count > 0)
                {
                    _context.PriceBars.AddRange(newBars);
                }

                import.Status = MarketDataImportStatus.Success;
                import.PriceBarsInserted = barsInserted;
                import.PriceBarsUpdated = barsUpdated;

                await _context.SaveChangesAsync(cancellationToken);

                if (transaction != null)
                {
                    await transaction.CommitAsync(cancellationToken);
                }
            }
            catch (Exception ex)
            {
                if (transaction != null)
                {
                    await transaction.RollbackAsync(cancellationToken);
                }

                import.Status = MarketDataImportStatus.Failed;
                import.ErrorMessage = $"Database transaction aborted and rolled back (0 partial bars): {ex.Message}";
                await _context.SaveChangesAsync(CancellationToken.None);
                throw;
            }
            finally
            {
                if (transaction != null)
                {
                    await transaction.DisposeAsync();
                }
            }
        }

        if (executionStrategy != null)
        {
            await executionStrategy.ExecuteAsync(ExecuteImportTransactionAsync);
        }
        else
        {
            await ExecuteImportTransactionAsync();
        }

        decompressedStream?.Dispose();

        _logger.LogInformation(
            "Successfully imported BIST bulletin for {Date}. Revision #{Rev}, Accepted rows: {Accepted}, Bars inserted: {Inserted}, Bars updated: {Updated}",
            sessionDate.Value, import.RevisionNumber, parseResult.AcceptedRows, barsInserted, barsUpdated);

        return import;
    }

    private static MemoryStream? ExtractCsvFromZip(byte[] zipBytes)
    {
        using var zipStream = new MemoryStream(zipBytes);
        using var archive = new ZipArchive(zipStream, ZipArchiveMode.Read);

        if (archive.Entries.Count > 20)
        {
            return null;
        }

        foreach (var entry in archive.Entries)
        {
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
