using BistQuant.Application.Common.Interfaces;
using BistQuant.Application.DTOs.MarketData;
using BistQuant.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BistQuant.Application.Services.MarketData;

public class MarketDataGapDetector : IMarketDataGapDetector
{
    private readonly IApplicationDbContext _context;
    private readonly IMarketSessionCalendar _sessionCalendar;
    private readonly ILogger<MarketDataGapDetector> _logger;

    public MarketDataGapDetector(
        IApplicationDbContext context,
        IMarketSessionCalendar sessionCalendar,
        ILogger<MarketDataGapDetector> logger)
    {
        _context = context;
        _sessionCalendar = sessionCalendar;
        _logger = logger;
    }

    public async Task<SymbolGapsDto> DetectGapsForSymbolAsync(
        string ticker,
        DateOnly startDate,
        DateOnly endDate,
        CancellationToken cancellationToken = default)
    {
        var symbol = await _context.Symbols
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Ticker == ticker.ToUpperInvariant(), cancellationToken);

        if (symbol == null)
        {
            return new SymbolGapsDto(ticker, 0, new List<MarketDataGapDto>());
        }

        // Get expected trading sessions from calendar (strictly skipping weekends & holidays)
        var expectedSessions = new List<DateOnly>();
        for (var d = startDate; d <= endDate; d = d.AddDays(1))
        {
            if (_sessionCalendar.IsTradingDay(d))
            {
                expectedSessions.Add(d);
            }
        }

        // Get actual price bars for symbol in range
        var startDt = startDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var endDt = endDate.ToDateTime(new TimeOnly(23, 59, 59), DateTimeKind.Utc);

        var existingBarDates = await _context.PriceBars
            .AsNoTracking()
            .Where(p => p.SymbolId == symbol.Id && p.Timeframe == Timeframe.Daily && p.Timestamp >= startDt && p.Timestamp <= endDt)
            .Select(p => DateOnly.FromDateTime(p.Timestamp))
            .ToHashSetAsync(cancellationToken);

        var gaps = new List<MarketDataGapDto>();

        foreach (var sessionDate in expectedSessions)
        {
            if (existingBarDates.Contains(sessionDate))
            {
                continue;
            }

            // Detect gap reason
            var import = await _context.MarketDataImports
                .AsNoTracking()
                .Where(i => i.SessionDate == sessionDate && i.IsCurrent)
                .OrderByDescending(i => i.Id)
                .FirstOrDefaultAsync(cancellationToken);

            if (import == null)
            {
                gaps.Add(new MarketDataGapDto(sessionDate, MarketDataGapReason.BulletinMissing, "No bulletin imported for session."));
                continue;
            }

            if (import.Status != MarketDataImportStatus.Success)
            {
                gaps.Add(new MarketDataGapDto(sessionDate, MarketDataGapReason.ImportFailed, $"Bulletin import failed: {import.ErrorMessage}"));
                continue;
            }

            var stats = await _context.DailyInstrumentMarketStats
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.SymbolId == symbol.Id && s.SessionDate == sessionDate, cancellationToken);

            if (stats != null)
            {
                if (stats.Suspended)
                {
                    gaps.Add(new MarketDataGapDto(sessionDate, MarketDataGapReason.Suspended, "Instrument was suspended on trading session."));
                }
                else if (stats.TotalTradedVolume == 0 || stats.TotalTradedValue == 0)
                {
                    gaps.Add(new MarketDataGapDto(sessionDate, MarketDataGapReason.NoTrade, "Instrument had zero traded volume/value on session."));
                }
                else
                {
                    gaps.Add(new MarketDataGapDto(sessionDate, MarketDataGapReason.Unknown, "Bar missing despite valid bulletin row."));
                }
            }
            else
            {
                gaps.Add(new MarketDataGapDto(sessionDate, MarketDataGapReason.Unknown, "Symbol not listed in session bulletin."));
            }
        }

        return new SymbolGapsDto(ticker, gaps.Count, gaps);
    }

    public async Task<UniverseCoverageSummaryDto> GetUniverseCoverageAsync(
        DateOnly? startDate = null,
        DateOnly? endDate = null,
        CancellationToken cancellationToken = default)
    {
        var symbols = await _context.Symbols
            .AsNoTracking()
            .Where(s => s.IsActive)
            .OrderBy(s => s.Ticker)
            .ToListAsync(cancellationToken);

        var latestBulletin = await _context.MarketDataImports
            .AsNoTracking()
            .Where(i => i.Status == MarketDataImportStatus.Success && i.IsCurrent)
            .OrderByDescending(i => i.SessionDate)
            .Select(i => (DateOnly?)i.SessionDate)
            .FirstOrDefaultAsync(cancellationToken);

        var warnings = await _context.IndicatorContinuityWarnings
            .AsNoTracking()
            .Where(w => !w.IsAcknowledged)
            .Select(w => w.SymbolId)
            .ToHashSetAsync(cancellationToken);

        var symbolCoverages = new List<SymbolCoverageDto>();

        foreach (var sym in symbols)
        {
            var barsQuery = _context.PriceBars
                .AsNoTracking()
                .Where(p => p.SymbolId == sym.Id && p.Timeframe == Timeframe.Daily);

            var barCount = await barsQuery.CountAsync(cancellationToken);
            DateOnly? firstBar = null;
            DateOnly? lastBar = null;

            if (barCount > 0)
            {
                var minDate = await barsQuery.MinAsync(p => p.Timestamp, cancellationToken);
                var maxDate = await barsQuery.MaxAsync(p => p.Timestamp, cancellationToken);
                firstBar = DateOnly.FromDateTime(minDate);
                lastBar = DateOnly.FromDateTime(maxDate);
            }

            var effectiveStart = startDate ?? firstBar;
            var effectiveEnd = endDate ?? lastBar ?? latestBulletin;

            int expectedSessions = 0;
            if (effectiveStart.HasValue && effectiveEnd.HasValue && effectiveStart.Value <= effectiveEnd.Value)
            {
                for (var d = effectiveStart.Value; d <= effectiveEnd.Value; d = d.AddDays(1))
                {
                    if (_sessionCalendar.IsTradingDay(d))
                    {
                        expectedSessions++;
                    }
                }
            }

            int missingSessions = Math.Max(0, expectedSessions - barCount);
            decimal coveragePercent = expectedSessions > 0
                ? Math.Round(Math.Min(100m, (decimal)barCount / expectedSessions * 100m), 2)
                : (barCount > 0 ? 100m : 0m);

            bool ema200Ready = barCount >= 220; // Stabilized EMA200 threshold
            bool hasWarning = warnings.Contains(sym.Id);

            symbolCoverages.Add(new SymbolCoverageDto(
                sym.Ticker,
                sym.Name,
                firstBar,
                lastBar,
                barCount,
                expectedSessions,
                missingSessions,
                coveragePercent,
                ema200Ready,
                sym.LastSeenInBulletinDate ?? latestBulletin,
                hasWarning
            ));
        }

        int totalSymbols = symbolCoverages.Count;
        int with300 = symbolCoverages.Count(s => s.DailyBarCount >= 300);
        int with500 = symbolCoverages.Count(s => s.DailyBarCount >= 500);
        decimal avgCoverage = totalSymbols > 0 ? Math.Round(symbolCoverages.Average(s => s.CoveragePercent), 2) : 0m;
        int totalMissing = symbolCoverages.Sum(s => s.MissingSessions);
        int withWarnings = symbolCoverages.Count(s => s.HasCorporateActionWarning);

        return new UniverseCoverageSummaryDto(
            totalSymbols,
            with300,
            with500,
            avgCoverage,
            totalMissing,
            withWarnings,
            symbolCoverages
        );
    }
}
