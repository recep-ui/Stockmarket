using System.Globalization;
using BistQuant.Application.Common.Exceptions;
using BistQuant.Application.Common.Interfaces;
using BistQuant.Domain.Entities;
using BistQuant.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BistQuant.Infrastructure.Providers.MarketData;

public record CsvImportResult(
    int TotalProcessed,
    int SuccessfulImports,
    int SkippedDuplicates,
    List<string> ValidationErrors
);

public interface ICsvMarketDataService
{
    Task<CsvImportResult> ImportCsvAsync(Stream stream, Timeframe timeframe = Timeframe.Daily, CancellationToken cancellationToken = default);
}

public class CsvMarketDataService : ICsvMarketDataService
{
    private readonly IApplicationDbContext _context;
    private readonly ILogger<CsvMarketDataService> _logger;

    public CsvMarketDataService(IApplicationDbContext context, ILogger<CsvMarketDataService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<CsvImportResult> ImportCsvAsync(Stream stream, Timeframe timeframe = Timeframe.Daily, CancellationToken cancellationToken = default)
    {
        using var reader = new StreamReader(stream);
        var errors = new List<string>();
        var totalRows = 0;
        var skippedDuplicates = 0;
        var newBars = new List<PriceBar>();

        // Preload symbols lookup
        var symbols = await _context.Symbols.ToDictionaryAsync(s => s.Ticker.ToUpper(), s => s, cancellationToken);
        
        // Read header
        var header = await reader.ReadLineAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(header))
        {
            throw new ValidationException(new Dictionary<string, string[]> { { "Csv", new[] { "CSV file is empty." } } });
        }

        var headerCols = header.Split(',', ';').Select(h => h.Trim().ToLowerInvariant()).ToArray();
        var symbolIdx = Array.FindIndex(headerCols, c => c.Contains("symbol") || c.Contains("ticker"));
        var dateIdx = Array.FindIndex(headerCols, c => c.Contains("date") || c.Contains("timestamp") || c.Contains("time"));
        var openIdx = Array.FindIndex(headerCols, c => c == "open");
        var highIdx = Array.FindIndex(headerCols, c => c == "high");
        var lowIdx = Array.FindIndex(headerCols, c => c == "low");
        var closeIdx = Array.FindIndex(headerCols, c => c == "close");
        var volumeIdx = Array.FindIndex(headerCols, c => c == "volume" || c == "vol");

        if (symbolIdx == -1 || dateIdx == -1 || openIdx == -1 || highIdx == -1 || lowIdx == -1 || closeIdx == -1)
        {
            throw new ValidationException(new Dictionary<string, string[]>
            {
                { "Csv", new[] { "CSV must contain columns: Symbol, Date, Open, High, Low, Close, Volume" } }
            });
        }

        while (await reader.ReadLineAsync(cancellationToken) is { } line)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;

            totalRows++;
            var cols = line.Split(',', ';').Select(c => c.Trim()).ToArray();
            if (cols.Length <= Math.Max(closeIdx, Math.Max(highIdx, lowIdx)))
            {
                errors.Add($"Row {totalRows}: Insufficient columns.");
                continue;
            }

            var ticker = cols[symbolIdx].ToUpperInvariant();
            if (!symbols.TryGetValue(ticker, out var symbolEntity))
            {
                errors.Add($"Row {totalRows}: Symbol '{ticker}' is not defined in system.");
                continue;
            }

            if (!DateTime.TryParse(cols[dateIdx], CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal, out var timestamp))
            {
                errors.Add($"Row {totalRows}: Invalid date format '{cols[dateIdx]}'.");
                continue;
            }

            if (!decimal.TryParse(cols[openIdx], NumberStyles.Any, CultureInfo.InvariantCulture, out var open) || open < 0)
            {
                errors.Add($"Row {totalRows}: Invalid Open price.");
                continue;
            }

            if (!decimal.TryParse(cols[highIdx], NumberStyles.Any, CultureInfo.InvariantCulture, out var high) || high < 0)
            {
                errors.Add($"Row {totalRows}: Invalid High price.");
                continue;
            }

            if (!decimal.TryParse(cols[lowIdx], NumberStyles.Any, CultureInfo.InvariantCulture, out var low) || low < 0)
            {
                errors.Add($"Row {totalRows}: Invalid Low price.");
                continue;
            }

            if (!decimal.TryParse(cols[closeIdx], NumberStyles.Any, CultureInfo.InvariantCulture, out var close) || close < 0)
            {
                errors.Add($"Row {totalRows}: Invalid Close price.");
                continue;
            }

            decimal volume = 0;
            if (volumeIdx != -1 && volumeIdx < cols.Length)
            {
                decimal.TryParse(cols[volumeIdx], NumberStyles.Any, CultureInfo.InvariantCulture, out volume);
            }

            // Validation Rule: High >= Low, High >= Open, High >= Close, Low <= Open, Low <= Close
            if (high < low)
            {
                errors.Add($"Row {totalRows}: High ({high}) cannot be less than Low ({low}).");
                continue;
            }

            // Check duplicate
            var isDuplicate = await _context.PriceBars.AnyAsync(
                p => p.SymbolId == symbolEntity.Id && p.Timeframe == timeframe && p.Timestamp == timestamp,
                cancellationToken);

            if (isDuplicate)
            {
                skippedDuplicates++;
                continue;
            }

            newBars.Add(new PriceBar
            {
                SymbolId = symbolEntity.Id,
                Timeframe = timeframe,
                Timestamp = timestamp,
                Open = open,
                High = high,
                Low = low,
                Close = close,
                Volume = Math.Max(0, volume)
            });
        }

        if (newBars.Any())
        {
            _context.PriceBars.AddRange(newBars);
            await _context.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Successfully imported {Count} price bars from CSV.", newBars.Count);
        }

        return new CsvImportResult(totalRows, newBars.Count, skippedDuplicates, errors);
    }
}
