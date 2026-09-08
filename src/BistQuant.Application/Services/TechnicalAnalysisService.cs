using BistQuant.Application.Common.Interfaces;
using BistQuant.Application.Services.Indicators;
using BistQuant.Domain.Entities;
using BistQuant.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BistQuant.Application.Services;

public interface ITechnicalAnalysisService
{
    Task<IndicatorSnapshot?> CalculateAndSaveSnapshotAsync(
        int symbolId,
        Timeframe timeframe,
        CancellationToken cancellationToken = default);

    Task<IndicatorSnapshot?> GetLatestSnapshotAsync(
        string symbol,
        Timeframe timeframe,
        CancellationToken cancellationToken = default);
}

public class TechnicalAnalysisService : ITechnicalAnalysisService
{
    private readonly IApplicationDbContext _context;
    private readonly ILogger<TechnicalAnalysisService> _logger;

    public TechnicalAnalysisService(IApplicationDbContext context, ILogger<TechnicalAnalysisService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<IndicatorSnapshot?> CalculateAndSaveSnapshotAsync(
        int symbolId,
        Timeframe timeframe,
        CancellationToken cancellationToken = default)
    {
        var bars = await _context.PriceBars
            .AsNoTracking()
            .Where(p => p.SymbolId == symbolId && p.Timeframe == timeframe)
            .OrderBy(p => p.Timestamp)
            .ToListAsync(cancellationToken);

        if (bars.Count < 20)
        {
            _logger.LogWarning("Insufficient price bars ({Count}) for Symbol ID {SymbolId} to compute indicators.", bars.Count, symbolId);
            return null;
        }

        var snapshots = IndicatorCalculators.CalculateSnapshots(bars, symbolId, timeframe);
        var snapshot = snapshots[^1];
        var latestBar = bars[^1];

        // Check if existing snapshot for this timestamp exists
        var existing = await _context.IndicatorSnapshots
            .FirstOrDefaultAsync(s => s.SymbolId == symbolId && s.Timeframe == timeframe && s.Timestamp == latestBar.Timestamp, cancellationToken);

        if (existing != null)
        {
            _context.IndicatorSnapshots.Remove(existing);
        }

        _context.IndicatorSnapshots.Add(snapshot);
        await _context.SaveChangesAsync(cancellationToken);

        return snapshot;
    }

    public async Task<IndicatorSnapshot?> GetLatestSnapshotAsync(
        string symbol,
        Timeframe timeframe,
        CancellationToken cancellationToken = default)
    {
        var sym = await _context.Symbols
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Ticker == symbol.ToUpper(), cancellationToken);

        if (sym == null) return null;

        var snapshot = await _context.IndicatorSnapshots
            .AsNoTracking()
            .Where(s => s.SymbolId == sym.Id && s.Timeframe == timeframe)
            .OrderByDescending(s => s.Timestamp)
            .FirstOrDefaultAsync(cancellationToken);

        // If no snapshot exists yet, compute and persist it on-demand
        if (snapshot == null)
        {
            snapshot = await CalculateAndSaveSnapshotAsync(sym.Id, timeframe, cancellationToken);
        }

        return snapshot;
    }
}
