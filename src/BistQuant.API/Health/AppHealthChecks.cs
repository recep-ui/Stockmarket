using BistQuant.Domain.Enums;
using BistQuant.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace BistQuant.API.Health;

public class DatabaseHealthCheck : IHealthCheck
{
    private readonly BistQuantDbContext _context;

    public DatabaseHealthCheck(BistQuantDbContext context)
    {
        _context = context;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var canConnect = await _context.Database.CanConnectAsync(cancellationToken);
            return canConnect
                ? HealthCheckResult.Healthy("Database is online and responsive.")
                : HealthCheckResult.Unhealthy("Database cannot connect.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Database connection threw an exception.", ex);
        }
    }
}

public class MarketDataFreshnessHealthCheck : IHealthCheck
{
    private readonly BistQuantDbContext _context;

    public MarketDataFreshnessHealthCheck(BistQuantDbContext context)
    {
        _context = context;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var latestBar = await _context.PriceBars
                .AsNoTracking()
                .OrderByDescending(p => p.Timestamp)
                .FirstOrDefaultAsync(cancellationToken);

            if (latestBar == null)
            {
                return HealthCheckResult.Degraded("No price bars available in database.");
            }

            // Fresh if data is within last 14 days (accounting for weekends and market holidays)
            var age = DateTime.UtcNow - latestBar.Timestamp;
            if (age.TotalDays > 14)
            {
                return HealthCheckResult.Degraded($"Market data is stale. Latest bar date: {latestBar.Timestamp:yyyy-MM-dd}.");
            }

            return HealthCheckResult.Healthy($"Market data is fresh. Latest bar date: {latestBar.Timestamp:yyyy-MM-dd}.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Market data freshness check failed.", ex);
        }
    }
}

public class RedisHealthCheck : IHealthCheck
{
    private readonly StackExchange.Redis.IConnectionMultiplexer? _redis;

    public RedisHealthCheck(IServiceProvider serviceProvider)
    {
        _redis = serviceProvider.GetService<StackExchange.Redis.IConnectionMultiplexer>();
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        if (_redis == null)
        {
            return HealthCheckResult.Healthy("In-Memory cache active; Redis not configured.");
        }

        try
        {
            if (!_redis.IsConnected)
            {
                return HealthCheckResult.Degraded("Redis is disconnected.");
            }

            var server = _redis.GetServer(_redis.GetEndPoints()[0]);
            var latency = await server.PingAsync();
            return HealthCheckResult.Healthy($"Redis is responsive (latency: {latency.TotalMilliseconds:F1}ms).");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Degraded("Redis check failed, running with in-memory fallback.", ex);
        }
    }
}

public class WorkerScanHealthCheck : IHealthCheck
{
    private readonly BistQuantDbContext _context;

    public WorkerScanHealthCheck(BistQuantDbContext context)
    {
        _context = context;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var latestSignal = await _context.Signals
                .AsNoTracking()
                .OrderByDescending(s => s.CreatedAt)
                .FirstOrDefaultAsync(cancellationToken);

            if (latestSignal == null)
            {
                return HealthCheckResult.Healthy("Worker initialized; awaiting initial scan cycle.");
            }

            var elapsed = DateTime.UtcNow - latestSignal.CreatedAt;
            if (elapsed.TotalDays > 4) // Calendar aware: 4 days accounts for weekends
            {
                return HealthCheckResult.Degraded($"Worker scan is stale (last completed: {latestSignal.CreatedAt:yyyy-MM-dd HH:mm}).");
            }

            return HealthCheckResult.Healthy($"Worker last completed scan at {latestSignal.CreatedAt:yyyy-MM-dd HH:mm}.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Degraded("Worker scan health check failed.", ex);
        }
    }
}
