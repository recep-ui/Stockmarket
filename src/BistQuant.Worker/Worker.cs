using System.Collections.Concurrent;
using BistQuant.Application.Common.Interfaces;
using BistQuant.Application.Services;
using BistQuant.Domain.Entities;
using BistQuant.Domain.Enums;
using BistQuant.Worker.Scheduling;

namespace BistQuant.Worker;

public class Worker : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IMarketScanScheduler _scheduler;
    private readonly ILogger<Worker> _logger;
    private readonly ConcurrentDictionary<Timeframe, DateTime> _lastCompletedMap = new();

    public Worker(
        IServiceProvider serviceProvider,
        IMarketScanScheduler scheduler,
        ILogger<Worker> logger)
    {
        _serviceProvider = serviceProvider;
        _scheduler = scheduler;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("BIST Quant Scanner Background Worker started at: {Time}", DateTimeOffset.Now);

        // Initial scan on startup for enabled timeframes
        if (_scheduler.IsTimeframeEnabled(Timeframe.Daily))
        {
            await TryRunScanAsync(Timeframe.Daily, stoppingToken);
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

        var heartbeat = new WorkerHeartbeat
        {
            WorkerInstance = Environment.MachineName,
            ScanType = "UniverseScan",
            Timeframe = timeframe,
            StartedAt = DateTime.UtcNow,
            Success = false
        };

        context.WorkerHeartbeats.Add(heartbeat);
        await context.SaveChangesAsync(stoppingToken);

        try
        {
            var results = await scanner.ScanUniverseAsync(timeframe, null, stoppingToken);
            heartbeat.CompletedAt = DateTime.UtcNow;
            heartbeat.Success = true;
            heartbeat.SymbolCount = results.Count;
            await context.SaveChangesAsync(stoppingToken);

            _lastCompletedMap[timeframe] = heartbeat.CompletedAt.Value;
            _logger.LogInformation("Background scan for {Timeframe} completed: {Count} symbols evaluated.", timeframe, results.Count);
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
