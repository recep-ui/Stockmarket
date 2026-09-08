using BistQuant.Application.Common.Interfaces;
using BistQuant.Domain.Enums;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace BistQuant.Application.Services.MarketData;

public class MarketScanScheduler : IMarketScanScheduler
{
    private readonly IConfiguration _configuration;
    private readonly IMarketSessionCalendar _sessionCalendar;
    private readonly ILogger<MarketScanScheduler> _logger;

    public MarketScanScheduler(
        IConfiguration configuration,
        IMarketSessionCalendar sessionCalendar,
        ILogger<MarketScanScheduler> logger)
    {
        _configuration = configuration;
        _sessionCalendar = sessionCalendar;
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

        // Invariant: Do NOT trigger weekend or market holiday scans.
        if (!_sessionCalendar.IsTradingDay(asOfUtc))
        {
            return dueList;
        }

        var localNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(asOfUtc, DateTimeKind.Utc), _sessionCalendar.MarketTimeZone);

        // 1. M15: runs only during trading session (10:15 to 18:10 local Istanbul time)
        // after a 15-minute candle closes (e.g. 10:15, 10:30, ..., 18:00)
        if (IsTimeframeEnabled(Timeframe.M15))
        {
            var m15Start = _sessionCalendar.SessionOpenTime.Add(TimeSpan.FromMinutes(15));
            var m15End = _sessionCalendar.SessionCloseTime.Add(TimeSpan.FromMinutes(10));

            if (localNow.TimeOfDay >= m15Start && localNow.TimeOfDay <= m15End)
            {
                var lastClosedM15Utc = _sessionCalendar.GetLastClosedCandleTimeUtc(Timeframe.M15, asOfUtc);
                if (lastClosedM15Utc.HasValue)
                {
                    if (!lastCompletedMap.TryGetValue(Timeframe.M15, out var lastM15) || lastM15 < lastClosedM15Utc.Value)
                    {
                        dueList.Add(Timeframe.M15);
                    }
                }
            }
        }

        // 2. H1: runs only during trading session (11:00 to 18:10 local Istanbul time)
        // after an hourly candle closes (e.g. 11:00, 12:00, ..., 18:00)
        if (IsTimeframeEnabled(Timeframe.H1))
        {
            var h1Start = _sessionCalendar.SessionOpenTime.Add(TimeSpan.FromHours(1));
            var h1End = _sessionCalendar.SessionCloseTime.Add(TimeSpan.FromMinutes(10));

            if (localNow.TimeOfDay >= h1Start && localNow.TimeOfDay <= h1End)
            {
                var lastClosedH1Utc = _sessionCalendar.GetLastClosedCandleTimeUtc(Timeframe.H1, asOfUtc);
                if (lastClosedH1Utc.HasValue)
                {
                    if (!lastCompletedMap.TryGetValue(Timeframe.H1, out var lastH1) || lastH1 < lastClosedH1Utc.Value)
                    {
                        dueList.Add(Timeframe.H1);
                    }
                }
            }
        }

        // 3. Daily: runs after BIST market close and finalization buffer (default 18:15 Europe/Istanbul)
        if (IsTimeframeEnabled(Timeframe.Daily))
        {
            var todayFinalizationUtc = _sessionCalendar.GetDailyFinalizationUtc(asOfUtc);
            if (asOfUtc >= todayFinalizationUtc)
            {
                if (!lastCompletedMap.TryGetValue(Timeframe.Daily, out var lastDaily) || lastDaily < todayFinalizationUtc)
                {
                    dueList.Add(Timeframe.Daily);
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
}
