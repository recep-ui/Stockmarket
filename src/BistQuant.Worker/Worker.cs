using System.Collections.Concurrent;
using BistQuant.Application.Common.Interfaces;
using BistQuant.Application.Services;
using BistQuant.Domain.Entities;
using BistQuant.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace BistQuant.Worker;

public class Worker : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IMarketScanScheduler _scheduler;
    private readonly IMarketSessionCalendar _sessionCalendar;
    private readonly ILogger<Worker> _logger;
    private readonly ConcurrentDictionary<Timeframe, DateTime> _lastCompletedMap = new();

    public Worker(
        IServiceProvider serviceProvider,
        IMarketScanScheduler scheduler,
        IMarketSessionCalendar sessionCalendar,
        ILogger<Worker> logger)
    {
        _serviceProvider = serviceProvider;
        _scheduler = scheduler;
        _sessionCalendar = sessionCalendar;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("BIST Quant Scanner Background Worker started at: {Time}", DateTimeOffset.Now);

        // Pre-populate last completed map from database to prevent duplicate startup runs
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
            var recentHeartbeats = await context.WorkerHeartbeats
                .AsNoTracking()
                .Where(w => w.Success && w.CompletedAt.HasValue)
                .GroupBy(w => w.Timeframe)
                .Select(g => new { Timeframe = g.Key, LastCompleted = g.Max(w => w.CompletedAt!.Value) })
                .ToListAsync(stoppingToken);

            foreach (var hb in recentHeartbeats)
            {
                _lastCompletedMap[hb.Timeframe] = hb.LastCompleted;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not load prior worker heartbeats during initialization.");
        }

        // Calendar-aware startup check: run only if a completed candle exists that has not yet been scanned
        var startupDue = _scheduler.GetDueTimeframes(DateTime.UtcNow, _lastCompletedMap);
        foreach (var tf in startupDue)
        {
            _logger.LogInformation("Startup scan cycle due for timeframe {Timeframe}.", tf);
            await TryRunScanAsync(tf, stoppingToken);
        }

        using var timer = new PeriodicTimer(_scheduler.GetNextCheckInterval());

        while (!stoppingToken.IsCancellationRequested && await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                var nowUtc = DateTime.UtcNow;
                var dueTimeframes = _scheduler.GetDueTimeframes(nowUtc, _lastCompletedMap);

                foreach (var tf in dueTimeframes)
                {
                    _logger.LogInformation("Triggering scheduled market scan for timeframe {Timeframe} at {Time}", tf, DateTimeOffset.Now);
                    await TryRunScanAsync(tf, stoppingToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during scheduled background market scan check.");
            }
        }
    }

    private async Task TryRunScanAsync(Timeframe timeframe, CancellationToken stoppingToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        var scanner = scope.ServiceProvider.GetRequiredService<IMarketScannerService>();

        var expectedCandleClose = _sessionCalendar.GetLastClosedCandleTimeUtc(timeframe, DateTime.UtcNow);

        var heartbeat = new WorkerHeartbeat
        {
            WorkerInstance = Environment.MachineName,
            ScanType = "UniverseScan",
            Timeframe = timeframe,
            StartedAt = DateTime.UtcNow,
            ExpectedCandleClose = expectedCandleClose,
            Success = false
        };

        context.WorkerHeartbeats.Add(heartbeat);
        await context.SaveChangesAsync(stoppingToken);

        try
        {
            var results = await scanner.ScanUniverseAsync(timeframe, null, stoppingToken);
            
            // Retrieve latest data bar timestamp to distinguish successful worker execution from stale data scan
            var latestBar = await context.PriceBars
                .AsNoTracking()
                .Where(p => p.Timeframe == timeframe)
                .OrderByDescending(p => p.Timestamp)
                .FirstOrDefaultAsync(stoppingToken);

            heartbeat.CompletedAt = DateTime.UtcNow;
            heartbeat.DataTimestamp = latestBar?.Timestamp;
            heartbeat.Success = true;
            heartbeat.SymbolCount = results.Count;
            await context.SaveChangesAsync(stoppingToken);

            _lastCompletedMap[timeframe] = heartbeat.CompletedAt.Value;
            _logger.LogInformation("Background scan for {Timeframe} completed: {Count} symbols evaluated (latest data: {DataTs}).",
                timeframe, results.Count, latestBar?.Timestamp.ToString("yyyy-MM-dd HH:mm:ss") ?? "None");
        }
        catch (Exception ex)
        {
            heartbeat.CompletedAt = DateTime.UtcNow;
            heartbeat.Success = false;
            heartbeat.ErrorMessage = ex.Message;
            await context.SaveChangesAsync(stoppingToken);
            _logger.LogError(ex, "Failed background market scan for timeframe {Timeframe}.", timeframe);
        }
    }
}
