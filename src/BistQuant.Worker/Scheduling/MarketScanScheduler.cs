using BistQuant.Domain.Enums;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace BistQuant.Worker.Scheduling;

public class MarketScanScheduler : IMarketScanScheduler
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<MarketScanScheduler> _logger;

    public MarketScanScheduler(IConfiguration configuration, ILogger<MarketScanScheduler> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public bool IsTimeframeEnabled(Timeframe timeframe)
    {
        return timeframe switch
        {
            Timeframe.M15 => _configuration.GetValue<bool?>("ScannerSchedules:M15Enabled") ?? true,
            Timeframe.H1 => _configuration.GetValue<bool?>("ScannerSchedules:H1Enabled") ?? true,
            Timeframe.Daily => _configuration.GetValue<bool?>("ScannerSchedules:DailyEnabled") ?? true,
            _ => false
        };
    }

    public List<Timeframe> GetDueTimeframes(DateTime asOfUtc, IDictionary<Timeframe, DateTime> lastCompletedMap)
    {
        var dueList = new List<Timeframe>();

        // 1. M15: runs after a 15-minute candle closes (e.g. at 0, 15, 30, 45 minutes + 15s buffer)
        if (IsTimeframeEnabled(Timeframe.M15))
        {
            var lastClosedM15 = GetLastClosedCandleTime(asOfUtc, TimeSpan.FromMinutes(15));
            if (!lastCompletedMap.TryGetValue(Timeframe.M15, out var lastM15) || lastM15 < lastClosedM15)
            {
                dueList.Add(Timeframe.M15);
            }
        }

        // 2. H1: runs after an hourly candle closes (at minute 0 + 15s buffer)
        if (IsTimeframeEnabled(Timeframe.H1))
        {
            var lastClosedH1 = GetLastClosedCandleTime(asOfUtc, TimeSpan.FromHours(1));
            if (!lastCompletedMap.TryGetValue(Timeframe.H1, out var lastH1) || lastH1 < lastClosedH1)
            {
                dueList.Add(Timeframe.H1);
            }
        }

        // 3. Daily: runs after BIST market close (default 18:15 Europe/Istanbul -> 15:15 UTC)
        if (IsTimeframeEnabled(Timeframe.Daily))
        {
            var dailyTargetUtcTimeStr = _configuration["ScannerSchedules:DailyRunTimeUtc"] ?? "15:15:00";
            if (TimeSpan.TryParse(dailyTargetUtcTimeStr, out var dailyTargetTime))
            {
                var todayDailyRunUtc = asOfUtc.Date.Add(dailyTargetTime);
                if (asOfUtc >= todayDailyRunUtc)
                {
                    if (!lastCompletedMap.TryGetValue(Timeframe.Daily, out var lastDaily) || lastDaily < todayDailyRunUtc)
                    {
                        dueList.Add(Timeframe.Daily);
                    }
                }
            }
        }

        return dueList;
    }

    public TimeSpan GetNextCheckInterval()
    {
        // Poll every 15 seconds to detect closed-candle boundaries with low latency
        return TimeSpan.FromSeconds(15);
    }

    private static DateTime GetLastClosedCandleTime(DateTime utcNow, TimeSpan interval)
    {
        var totalTicks = utcNow.Ticks;
        var intervalTicks = interval.Ticks;
        var remainder = totalTicks % intervalTicks;
        return new DateTime(totalTicks - remainder, DateTimeKind.Utc);
    }
}
