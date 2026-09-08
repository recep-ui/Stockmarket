using BistQuant.Application.Common.Interfaces;
using BistQuant.Domain.Enums;
using BistQuant.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
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
    private readonly IMarketDataFreshnessPolicy _freshnessPolicy;

    public MarketDataFreshnessHealthCheck(BistQuantDbContext context, IMarketDataFreshnessPolicy freshnessPolicy)
    {
        _context = context;
        _freshnessPolicy = freshnessPolicy;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var activeSymbols = await _context.Symbols
                .AsNoTracking()
                .Where(s => s.IsActive)
                .Select(s => s.Id)
                .ToListAsync(cancellationToken);

            if (activeSymbols.Count == 0)
            {
                return HealthCheckResult.Degraded("No active symbols configured in the database.");
            }

            var latestBars = await _context.PriceBars
                .AsNoTracking()
                .Where(p => activeSymbols.Contains(p.SymbolId) && p.Timeframe == Timeframe.Daily)
                .GroupBy(p => p.SymbolId)
                .Select(g => new
                {
                    SymbolId = g.Key,
                    LatestTimestamp = g.Max(p => p.Timestamp)
                })
                .ToListAsync(cancellationToken);

            int totalActive = activeSymbols.Count;
            int withData = latestBars.Count;
            int staleCount = 0;
            DateTime? newestBar = latestBars.Count > 0 ? latestBars.Max(b => b.LatestTimestamp) : null;

            foreach (var b in latestBars)
            {
                var check = _freshnessPolicy.CheckFreshness(Timeframe.Daily, b.LatestTimestamp);
                if (!check.IsFresh)
                {
                    staleCount++;
                }
            }

            // Symbols with zero price bars are also counted as stale
            staleCount += (totalActive - withData);
            double stalePercentage = totalActive > 0 ? Math.Round((double)staleCount / totalActive * 100.0, 1) : 0;

            var data = new Dictionary<string, object>
            {
                ["ActiveSymbols"] = totalActive,
                ["SymbolsWithData"] = withData,
                ["StaleSymbols"] = staleCount,
                ["StalePercentage"] = stalePercentage,
                ["LatestBarDate"] = newestBar?.ToString("yyyy-MM-dd") ?? "None"
            };

            if (stalePercentage >= 50.0)
            {
                return HealthCheckResult.Unhealthy($"Market data is severely stale ({stalePercentage}% of symbols stale: {staleCount}/{totalActive}).", data: data);
            }

            if (stalePercentage > 15.0)
            {
                return HealthCheckResult.Degraded($"Market data has degraded freshness ({stalePercentage}% stale: {staleCount}/{totalActive}).", data: data);
            }

            return HealthCheckResult.Healthy($"Market data is fresh across active symbols ({totalActive - staleCount}/{totalActive} symbols fresh).", data: data);
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
    private readonly IConfiguration _configuration;

    public RedisHealthCheck(IServiceProvider serviceProvider, IConfiguration configuration)
    {
        _redis = serviceProvider.GetService<StackExchange.Redis.IConnectionMultiplexer>();
        _configuration = configuration;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        bool isRedisRequired = _configuration.GetValue<bool>("Redis:Required");

        if (_redis == null)
        {
            if (isRedisRequired)
            {
                return HealthCheckResult.Unhealthy("Redis is required in this environment but is not configured or failed to connect.");
            }
            return HealthCheckResult.Healthy("In-Memory cache active; Redis not required.");
        }

        try
        {
            if (!_redis.IsConnected)
            {
                if (isRedisRequired)
                {
                    return HealthCheckResult.Unhealthy("Redis is required but is currently disconnected.");
                }
                return HealthCheckResult.Degraded("Redis is disconnected, operating on in-memory fallback.");
            }

            var server = _redis.GetServer(_redis.GetEndPoints()[0]);
            var latency = await server.PingAsync();
            return HealthCheckResult.Healthy($"Redis is responsive (latency: {latency.TotalMilliseconds:F1}ms).");
        }
        catch (Exception ex)
        {
            if (isRedisRequired)
            {
                return HealthCheckResult.Unhealthy("Redis is required but health check failed.", ex);
            }
            return HealthCheckResult.Degraded("Redis check failed, running with in-memory fallback.", ex);
        }
    }
}

public class WorkerScanHealthCheck : IHealthCheck
{
    private readonly BistQuantDbContext _context;
    private static readonly DateTime AppStartTime = DateTime.UtcNow;

    public WorkerScanHealthCheck(BistQuantDbContext context)
    {
        _context = context;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var latestHeartbeat = await _context.WorkerHeartbeats
                .AsNoTracking()
                .OrderByDescending(w => w.CompletedAt ?? w.StartedAt)
                .FirstOrDefaultAsync(cancellationToken);

            if (latestHeartbeat == null || latestHeartbeat.CompletedAt == null)
            {
                var uptime = DateTime.UtcNow - AppStartTime;
                var gracePeriod = TimeSpan.FromMinutes(15);
                if (uptime > gracePeriod)
                {
                    return HealthCheckResult.Unhealthy($"No worker scan cycle has completed within startup grace period ({uptime.TotalMinutes:F1} minutes since startup).");
                }

                return HealthCheckResult.Degraded($"Worker starting up; awaiting initial scan cycle ({uptime.TotalMinutes:F1}m elapsed of {gracePeriod.TotalMinutes:F0}m grace period).");
            }

            if (!latestHeartbeat.Success)
            {
                return HealthCheckResult.Degraded($"Last worker scan failed for timeframe {latestHeartbeat.Timeframe}: {latestHeartbeat.ErrorMessage}");
            }

            var elapsed = DateTime.UtcNow - latestHeartbeat.CompletedAt.Value;
            if (elapsed > TimeSpan.FromDays(4)) // Calendar aware tolerance for Daily
            {
                return HealthCheckResult.Degraded($"Worker scan is stale. Last completed: {latestHeartbeat.CompletedAt.Value:yyyy-MM-dd HH:mm} ({elapsed.TotalHours:F1} hours ago).");
            }

            var data = new Dictionary<string, object>
            {
                ["WorkerInstance"] = latestHeartbeat.WorkerInstance,
                ["ScanType"] = latestHeartbeat.ScanType,
                ["Timeframe"] = latestHeartbeat.Timeframe.ToString(),
                ["CompletedAt"] = latestHeartbeat.CompletedAt.Value,
                ["SymbolCount"] = latestHeartbeat.SymbolCount
            };

            return HealthCheckResult.Healthy($"Worker scan healthy. Last completed at {latestHeartbeat.CompletedAt.Value:yyyy-MM-dd HH:mm} ({latestHeartbeat.SymbolCount} symbols).", data);
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Worker scan health check failed.", ex);
        }
    }
}
