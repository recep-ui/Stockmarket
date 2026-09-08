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
    private readonly IMarketSessionCalendar _sessionCalendar;
    private readonly IConfiguration _configuration;

    public MarketDataFreshnessHealthCheck(
        BistQuantDbContext context,
        IMarketDataFreshnessPolicy freshnessPolicy,
        IMarketSessionCalendar sessionCalendar,
        IConfiguration configuration)
    {
        _context = context;
        _freshnessPolicy = freshnessPolicy;
        _sessionCalendar = sessionCalendar;
        _configuration = configuration;
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

            var nowUtc = DateTime.UtcNow;
            var targetTimeframes = new[] { Timeframe.Daily, Timeframe.H1, Timeframe.M15 };
            var timeframeData = new Dictionary<string, object>();

            bool anyUnhealthy = false;
            bool anyDegraded = false;
            var statusSummaries = new List<string>();

            foreach (var tf in targetTimeframes)
            {
                bool isRequired = tf == Timeframe.Daily || (_configuration.GetValue<bool?>($"FreshnessThresholds:{tf}Required") ?? false);

                var expectedBarTime = _sessionCalendar.ExpectedLatestBarTimeUtc(tf, nowUtc);

                var latestBars = await _context.PriceBars
                    .AsNoTracking()
                    .Where(p => activeSymbols.Contains(p.SymbolId) && p.Timeframe == tf)
                    .GroupBy(p => p.SymbolId)
                    .Select(g => new
                    {
                        SymbolId = g.Key,
                        LatestTimestamp = g.Max(p => p.Timestamp)
                    })
                    .ToListAsync(cancellationToken);

                int totalActive = activeSymbols.Count;
                int withData = latestBars.Count;
                int missingCount = totalActive - withData;
                int staleCount = 0;
                DateTime? newestBar = latestBars.Count > 0 ? latestBars.Max(b => b.LatestTimestamp) : null;

                foreach (var b in latestBars)
                {
                    if (b.LatestTimestamp >= expectedBarTime)
                    {
                        continue;
                    }

                    var check = _freshnessPolicy.CheckFreshness(tf, b.LatestTimestamp, expectedBarTime);
                    if (!check.IsFresh)
                    {
                        staleCount++;
                    }
                }

                // Missing symbols without bars are also treated as stale
                staleCount += missingCount;
                int freshCount = totalActive - staleCount;
                double stalePercentage = totalActive > 0 ? Math.Round((double)staleCount / totalActive * 100.0, 1) : 0;

                string tfStatus = "Healthy";
                if (stalePercentage >= 50.0)
                {
                    tfStatus = "Unhealthy";
                    if (isRequired) anyUnhealthy = true;
                    else anyDegraded = true;
                }
                else if (stalePercentage > 15.0)
                {
                    tfStatus = "Degraded";
                    anyDegraded = true;
                }

                timeframeData[tf.ToString()] = new Dictionary<string, object>
                {
                    ["ActiveSymbols"] = totalActive,
                    ["SymbolsWithData"] = withData,
                    ["MissingSymbols"] = missingCount,
                    ["Fresh"] = freshCount,
                    ["Stale"] = staleCount,
                    ["StalePercentage"] = stalePercentage,
                    ["ExpectedBarTimestamp"] = expectedBarTime.ToString("yyyy-MM-dd HH:mm:ss"),
                    ["LatestBarTimestamp"] = newestBar?.ToString("yyyy-MM-dd HH:mm:ss") ?? "None",
                    ["Status"] = tfStatus
                };

                statusSummaries.Add($"{tf}: {freshCount}/{totalActive} fresh ({stalePercentage}% stale)");
            }

            var summaryText = string.Join("; ", statusSummaries);

            if (anyUnhealthy)
            {
                return HealthCheckResult.Unhealthy($"Market data freshness check failed: {summaryText}", data: timeframeData);
            }

            if (anyDegraded)
            {
                return HealthCheckResult.Degraded($"Market data freshness degraded: {summaryText}", data: timeframeData);
            }

            return HealthCheckResult.Healthy($"Market data is fresh across all timeframes: {summaryText}", data: timeframeData);
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
    private readonly IMarketSessionCalendar _sessionCalendar;
    private readonly IConfiguration _configuration;
    private static readonly DateTime AppStartTime = DateTime.UtcNow;

    public WorkerScanHealthCheck(
        BistQuantDbContext context,
        IMarketSessionCalendar sessionCalendar,
        IConfiguration configuration)
    {
        _context = context;
        _sessionCalendar = sessionCalendar;
        _configuration = configuration;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var nowUtc = DateTime.UtcNow;
            var targetTimeframes = new[] { Timeframe.Daily, Timeframe.H1, Timeframe.M15 };
            var timeframeReports = new Dictionary<string, object>();

            bool anyUnhealthy = false;
            bool anyDegraded = false;
            var statusNotes = new List<string>();

            foreach (var tf in targetTimeframes)
            {
                bool isEnabled = tf switch
                {
                    Timeframe.Daily => _configuration.GetValue<bool?>("ScannerSchedules:DailyEnabled") ?? true,
                    Timeframe.H1 => _configuration.GetValue<bool?>("ScannerSchedules:H1Enabled") ?? true,
                    Timeframe.M15 => _configuration.GetValue<bool?>("ScannerSchedules:M15Enabled") ?? true,
                    _ => false
                };

                if (!isEnabled)
                {
                    timeframeReports[tf.ToString()] = new Dictionary<string, object> { ["Status"] = "Disabled" };
                    continue;
                }

                var latestHb = await _context.WorkerHeartbeats
                    .AsNoTracking()
                    .Where(w => w.Timeframe == tf && w.Success && w.CompletedAt != null)
                    .OrderByDescending(w => w.CompletedAt)
                    .FirstOrDefaultAsync(cancellationToken);

                var expectedCandle = _sessionCalendar.GetLastClosedCandleTimeUtc(tf, nowUtc);

                if (latestHb == null || latestHb.CompletedAt == null)
                {
                    var uptime = nowUtc - AppStartTime;
                    var gracePeriod = TimeSpan.FromMinutes(20);

                    if (uptime > gracePeriod)
                    {
                        timeframeReports[tf.ToString()] = new Dictionary<string, object>
                        {
                            ["Status"] = "Unhealthy",
                            ["Reason"] = "No heartbeat recorded within startup grace period."
                        };
                        anyUnhealthy = true;
                        statusNotes.Add($"{tf}: No heartbeat");
                    }
                    else
                    {
                        timeframeReports[tf.ToString()] = new Dictionary<string, object>
                        {
                            ["Status"] = "Degraded",
                            ["Reason"] = $"Awaiting initial scan ({uptime.TotalMinutes:F0}m elapsed)."
                        };
                        anyDegraded = true;
                        statusNotes.Add($"{tf}: Awaiting initial scan");
                    }
                    continue;
                }

                // Check staleness relative to session calendar expectations
                var elapsed = nowUtc - latestHb.CompletedAt.Value;
                bool isStale = false;
                string staleReason = string.Empty;

                if (tf == Timeframe.Daily)
                {
                    // Daily scans run once per trading day after 18:15 Istanbul.
                    // Over weekends/holidays, the Friday scan is expected and valid.
                    var maxDailyTolerance = TimeSpan.FromDays(4); // covers up to 4-day weekend/holiday stretches
                    if (elapsed > maxDailyTolerance)
                    {
                        isStale = true;
                        staleReason = $"Daily scan completed {elapsed.TotalDays:F1} days ago (exceeds {maxDailyTolerance.TotalDays}d tolerance).";
                    }
                }
                else if (tf == Timeframe.H1)
                {
                    // H1 expected within 2 hours during market hours, or last session close over closed hours
                    bool isMarketOpen = _sessionCalendar.IsMarketOpen(nowUtc);
                    if (isMarketOpen && elapsed > TimeSpan.FromHours(2.5))
                    {
                        isStale = true;
                        staleReason = $"H1 scan is {elapsed.TotalMinutes:F0} minutes old during active session.";
                    }
                    else if (!isMarketOpen && elapsed > TimeSpan.FromDays(4))
                    {
                        isStale = true;
                        staleReason = $"H1 scan is {elapsed.TotalDays:F1} days old while market closed.";
                    }
                }
                else if (tf == Timeframe.M15)
                {
                    // M15 expected within 45 minutes during market hours, or last session close over closed hours
                    bool isMarketOpen = _sessionCalendar.IsMarketOpen(nowUtc);
                    if (isMarketOpen && elapsed > TimeSpan.FromMinutes(45))
                    {
                        isStale = true;
                        staleReason = $"M15 scan is {elapsed.TotalMinutes:F0} minutes old during active session.";
                    }
                    else if (!isMarketOpen && elapsed > TimeSpan.FromDays(4))
                    {
                        isStale = true;
                        staleReason = $"M15 scan is {elapsed.TotalDays:F1} days old while market closed.";
                    }
                }

                if (isStale)
                {
                    timeframeReports[tf.ToString()] = new Dictionary<string, object>
                    {
                        ["Status"] = "Degraded",
                        ["CompletedAt"] = latestHb.CompletedAt.Value.ToString("yyyy-MM-dd HH:mm:ss"),
                        ["ExpectedCandleClose"] = expectedCandle?.ToString("yyyy-MM-dd HH:mm:ss") ?? "None",
                        ["DataTimestamp"] = latestHb.DataTimestamp?.ToString("yyyy-MM-dd HH:mm:ss") ?? "None",
                        ["SymbolCount"] = latestHb.SymbolCount,
                        ["Reason"] = staleReason
                    };
                    anyDegraded = true;
                    statusNotes.Add($"{tf}: Stale ({staleReason})");
                }
                else
                {
                    timeframeReports[tf.ToString()] = new Dictionary<string, object>
                    {
                        ["Status"] = "Healthy",
                        ["CompletedAt"] = latestHb.CompletedAt.Value.ToString("yyyy-MM-dd HH:mm:ss"),
                        ["ExpectedCandleClose"] = expectedCandle?.ToString("yyyy-MM-dd HH:mm:ss") ?? "None",
                        ["DataTimestamp"] = latestHb.DataTimestamp?.ToString("yyyy-MM-dd HH:mm:ss") ?? "None",
                        ["SymbolCount"] = latestHb.SymbolCount
                    };
                    statusNotes.Add($"{tf}: Healthy");
                }
            }

            var noteSummary = string.Join("; ", statusNotes);

            if (anyUnhealthy)
            {
                return HealthCheckResult.Unhealthy($"Worker scan health check failed: {noteSummary}", data: timeframeReports);
            }

            if (anyDegraded)
            {
                return HealthCheckResult.Degraded($"Worker scan health is degraded: {noteSummary}", data: timeframeReports);
            }

            return HealthCheckResult.Healthy($"Worker scan healthy across timeframes: {noteSummary}", data: timeframeReports);
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Worker scan health check failed.", ex);
        }
    }
}
