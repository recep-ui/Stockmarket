using BistQuant.Application.Common.Interfaces;
using BistQuant.Application.Common.Models;
using BistQuant.Application.DTOs.Scanner;
using BistQuant.Domain.Entities;
using BistQuant.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BistQuant.Application.Services;

public interface IMarketScannerService
{
    Task<List<ScannerItemDto>> ScanUniverseAsync(Timeframe timeframe = Timeframe.Daily, CancellationToken cancellationToken = default);

    Task<List<ScannerItemDto>> ScanUniverseAsync(Timeframe timeframe, int? strategyId, CancellationToken cancellationToken = default);

    Task<PagedResult<ScannerItemDto>> GetScannerResultsAsync(ScannerFilterDto filter, CancellationToken cancellationToken = default);

    Task<MarketOverviewDto> GetMarketOverviewAsync(CancellationToken cancellationToken = default);
}

public class MarketScannerService : IMarketScannerService
{
    private readonly IApplicationDbContext _context;
    private readonly ISignalEngine _signalEngine;
    private readonly ITechnicalAnalysisService _technicalService;
    private readonly IAlertEngine _alertEngine;
    private readonly ICacheService _cacheService;
    private readonly ILogger<MarketScannerService> _logger;

    private const string CacheKeyPrefix = "scanner_results_";

    public MarketScannerService(
        IApplicationDbContext context,
        ISignalEngine signalEngine,
        ITechnicalAnalysisService technicalService,
        IAlertEngine alertEngine,
        ICacheService cacheService,
        ILogger<MarketScannerService> logger)
    {
        _context = context;
        _signalEngine = signalEngine;
        _technicalService = technicalService;
        _alertEngine = alertEngine;
        _cacheService = cacheService;
        _logger = logger;
    }

    public Task<List<ScannerItemDto>> ScanUniverseAsync(Timeframe timeframe = Timeframe.Daily, CancellationToken cancellationToken = default)
    {
        return ScanUniverseAsync(timeframe, null, cancellationToken);
    }

    public async Task<List<ScannerItemDto>> ScanUniverseAsync(Timeframe timeframe, int? strategyId, CancellationToken cancellationToken = default)
    {
        Strategy? strategy = null;
        if (strategyId.HasValue)
        {
            strategy = await _context.Strategies
                .Include(s => s.Rules)
                .FirstOrDefaultAsync(s => s.Id == strategyId.Value, cancellationToken);
        }

        var symbols = await _context.Symbols
            .AsNoTracking()
            .Where(s => s.IsActive)
            .OrderBy(s => s.Ticker)
            .ToListAsync(cancellationToken);

        var results = new List<ScannerItemDto>();

        foreach (var sym in symbols)
        {
            try
            {
                var signal = await _signalEngine.GenerateAndSaveSignalAsync(sym.Id, timeframe, strategy, cancellationToken);
                if (signal == null) continue;

                // Wire alerts pipeline: evaluate and dispatch notifications for generated signal
                try
                {
                    await _alertEngine.ProcessAlertsForSignalAsync(signal, cancellationToken);
                }
                catch (Exception alertEx)
                {
                    _logger.LogWarning(alertEx, "Alert processing failed for symbol {Ticker}", sym.Ticker);
                }

                var snapshot = await _technicalService.GetLatestSnapshotAsync(sym.Ticker, timeframe, cancellationToken);

                // Calculate daily price change
                var recentBars = await _context.PriceBars
                    .AsNoTracking()
                    .Where(p => p.SymbolId == sym.Id && p.Timeframe == timeframe)
                    .OrderByDescending(p => p.Timestamp)
                    .Take(2)
                    .ToListAsync(cancellationToken);

                decimal dailyChange = 0;
                if (recentBars.Count >= 2 && recentBars[1].Close > 0)
                {
                    dailyChange = Math.Round(((recentBars[0].Close - recentBars[1].Close) / recentBars[1].Close) * 100m, 2);
                }

                string trendDesc = snapshot?.SuperTrendDirection == 1 ? "Bullish" : "Bearish";
                if (signal.Score >= 80) trendDesc = "Strong Bullish";
                else if (signal.Score <= 30) trendDesc = "Strong Bearish";

                results.Add(new ScannerItemDto(
                    sym.Ticker,
                    sym.Name,
                    sym.Sector,
                    signal.Price,
                    dailyChange,
                    signal.Score,
                    signal.SignalType.ToString(),
                    signal.TrendScore,
                    signal.MomentumScore,
                    signal.VolumeScore,
                    signal.StructureScore,
                    snapshot?.RSI14,
                    snapshot?.MACD,
                    snapshot?.ADX14,
                    snapshot?.VolumeRatio,
                    trendDesc,
                    snapshot?.AverageVolume20 ?? 0,
                    signal.CreatedAt
                ));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed scanning symbol {Ticker}", sym.Ticker);
            }
        }

        // Cache full universe results
        await _cacheService.SetAsync($"{CacheKeyPrefix}{(byte)timeframe}", results, TimeSpan.FromMinutes(15), cancellationToken);

        _logger.LogInformation("Market scan completed for {Count} symbols across timeframe {Timeframe}.", results.Count, timeframe);
        return results;
    }

    public async Task<PagedResult<ScannerItemDto>> GetScannerResultsAsync(ScannerFilterDto filter, CancellationToken cancellationToken = default)
    {
        var cached = await _cacheService.GetAsync<List<ScannerItemDto>>($"{CacheKeyPrefix}{(byte)filter.Timeframe}", cancellationToken);
        if (cached == null || cached.Count == 0)
        {
            cached = await ScanUniverseAsync(filter.Timeframe, cancellationToken);
        }

        var query = cached.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(filter.Signal))
        {
            query = query.Where(i => string.Equals(i.Signal, filter.Signal, StringComparison.OrdinalIgnoreCase));
        }

        if (filter.MinScore.HasValue)
        {
            query = query.Where(i => i.Score >= filter.MinScore.Value);
        }

        if (!string.IsNullOrWhiteSpace(filter.Sector))
        {
            query = query.Where(i => string.Equals(i.Sector, filter.Sector, StringComparison.OrdinalIgnoreCase));
        }

        if (filter.MinRsi.HasValue)
        {
            query = query.Where(i => i.RSI.HasValue && i.RSI.Value >= filter.MinRsi.Value);
        }

        if (filter.MaxRsi.HasValue)
        {
            query = query.Where(i => i.RSI.HasValue && i.RSI.Value <= filter.MaxRsi.Value);
        }

        if (filter.MinVolumeRatio.HasValue)
        {
            query = query.Where(i => i.VolumeRatio.HasValue && i.VolumeRatio.Value >= filter.MinVolumeRatio.Value);
        }

        // Sorting
        bool isAsc = string.Equals(filter.SortDirection, "asc", StringComparison.OrdinalIgnoreCase);
        query = filter.SortBy?.ToLowerInvariant() switch
        {
            "volume" => isAsc ? query.OrderBy(i => i.Volume) : query.OrderByDescending(i => i.Volume),
            "pricechange" => isAsc ? query.OrderBy(i => i.DailyChangePercent) : query.OrderByDescending(i => i.DailyChangePercent),
            "signaltime" => isAsc ? query.OrderBy(i => i.SignalTime) : query.OrderByDescending(i => i.SignalTime),
            _ => isAsc ? query.OrderBy(i => i.Score) : query.OrderByDescending(i => i.Score)
        };

        var totalCount = query.Count();
        var pagedItems = query.Skip((filter.Page - 1) * filter.PageSize).Take(filter.PageSize).ToList();

        return new PagedResult<ScannerItemDto>(pagedItems, totalCount, filter.Page, filter.PageSize);
    }

    public async Task<MarketOverviewDto> GetMarketOverviewAsync(CancellationToken cancellationToken = default)
    {
        var items = await _cacheService.GetAsync<List<ScannerItemDto>>($"{CacheKeyPrefix}{(byte)Timeframe.Daily}", cancellationToken);
        if (items == null || items.Count == 0)
        {
            items = await ScanUniverseAsync(Timeframe.Daily, cancellationToken);
        }

        int strongBuy = items.Count(i => i.Signal == nameof(SignalType.StrongBuy));
        int buy = items.Count(i => i.Signal == nameof(SignalType.Buy));
        int candidate = items.Count(i => i.Signal == nameof(SignalType.BuyCandidate));
        int watch = items.Count(i => i.Signal == nameof(SignalType.Watch));
        int sell = items.Count(i => i.Signal == nameof(SignalType.Sell) || i.Signal == nameof(SignalType.StrongSell));

        decimal avgScore = items.Count > 0 ? Math.Round((decimal)items.Average(i => i.Score), 1) : 50.0m;

        var topSignals = items.OrderByDescending(i => i.Score).Take(5).ToList();
        var volumeSurges = items.OrderByDescending(i => i.VolumeRatio ?? 0).Take(5).ToList();
        var breakouts = items.Where(i => i.StructureScore >= 10).OrderByDescending(i => i.Score).Take(5).ToList();

        return new MarketOverviewDto(
            items.Count,
            strongBuy,
            buy,
            candidate,
            watch,
            sell,
            avgScore,
            topSignals,
            volumeSurges,
            breakouts
        );
    }
}
