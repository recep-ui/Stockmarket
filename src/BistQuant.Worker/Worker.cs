using BistQuant.Application.Services;
using BistQuant.Domain.Enums;

namespace BistQuant.Worker;

public class Worker : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<Worker> _logger;

    public Worker(IServiceProvider serviceProvider, ILogger<Worker> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("BIST Quant Scanner Background Worker started at: {Time}", DateTimeOffset.Now);

        // Initial scan on startup
        await RunScanAsync(Timeframe.Daily, stoppingToken);

        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(15));

        while (!stoppingToken.IsCancellationRequested && await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                _logger.LogInformation("Periodic scanner tick triggered at: {Time}", DateTimeOffset.Now);
                await RunScanAsync(Timeframe.Daily, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during background market scan.");
            }
        }
    }

    private async Task RunScanAsync(Timeframe timeframe, CancellationToken stoppingToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var scanner = scope.ServiceProvider.GetRequiredService<IMarketScannerService>();
        var results = await scanner.ScanUniverseAsync(timeframe, stoppingToken);
        _logger.LogInformation("Background scan completed: {Count} symbols evaluated.", results.Count);
    }
}
