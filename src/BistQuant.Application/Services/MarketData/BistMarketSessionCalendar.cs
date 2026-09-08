using BistQuant.Application.Common.Interfaces;
using BistQuant.Domain.Enums;
using Microsoft.Extensions.Configuration;

namespace BistQuant.Application.Services.MarketData;

public class BistMarketSessionCalendar : IMarketSessionCalendar
{
    private readonly IHolidayCalendar _holidayCalendar;
    private readonly IConfiguration? _configuration;
    private readonly TimeZoneInfo _istanbulTz;
    private readonly TimeSpan _openTime;
    private readonly TimeSpan _closeTime;
    private readonly TimeSpan _dailyFinalizationTime;

    public TimeZoneInfo MarketTimeZone => _istanbulTz;
    public TimeSpan SessionOpenTime => _openTime;
    public TimeSpan SessionCloseTime => _closeTime;
    public TimeSpan DailyFinalizationTime => _dailyFinalizationTime;

    public BistMarketSessionCalendar(IConfiguration? configuration = null, IHolidayCalendar? holidayCalendar = null)
    {
        _configuration = configuration;
        _holidayCalendar = holidayCalendar ?? new ConfigurableHolidayCalendar(configuration);

        var tzId = configuration?["BistSession:TimeZone"] ?? "Europe/Istanbul";
        _istanbulTz = ResolveTimeZone(tzId);

        var openStr = configuration?["BistSession:OpenTime"] ?? "10:00:00";
        var closeStr = configuration?["BistSession:CloseTime"] ?? "18:00:00";
        var dailyFinStr = configuration?["BistSession:DailyFinalizationTime"] ?? "18:15:00";

        _openTime = TimeSpan.TryParse(openStr, out var ot) ? ot : new TimeSpan(10, 0, 0);
        _closeTime = TimeSpan.TryParse(closeStr, out var ct) ? ct : new TimeSpan(18, 0, 0);
        _dailyFinalizationTime = TimeSpan.TryParse(dailyFinStr, out var dt) ? dt : new TimeSpan(18, 15, 0);
    }

    private static TimeZoneInfo ResolveTimeZone(string timeZoneId)
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        }
        catch
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById("Turkey Standard Time");
            }
            catch
            {
                return TimeZoneInfo.CreateCustomTimeZone("Europe/Istanbul", TimeSpan.FromHours(3), "Istanbul Time", "Istanbul Time");
            }
        }
    }

    private DateTime ToLocal(DateTime utcTime)
    {
        var utc = DateTime.SpecifyKind(utcTime, DateTimeKind.Utc);
        return TimeZoneInfo.ConvertTimeFromUtc(utc, _istanbulTz);
    }

    private DateTime ToUtc(DateTime localTime)
    {
        var unspecified = DateTime.SpecifyKind(localTime, DateTimeKind.Unspecified);
        return TimeZoneInfo.ConvertTimeToUtc(unspecified, _istanbulTz);
    }

    public MarketSessionInfo GetSessionInfo(DateOnly date)
    {
        var isTrading = IsTradingDay(date);
        var isHalf = IsHalfDay(date);
        var open = isTrading ? GetMarketOpenTime(date) : TimeSpan.Zero;
        var close = isTrading ? GetMarketCloseTime(date) : TimeSpan.Zero;
        var ov = _holidayCalendar.GetOverride(date);
        var desc = ov?.Reason ?? (isTrading ? (isHalf ? "Half-Day Trading Session" : "Regular Trading Session") : "Weekend / Closed");
        return new MarketSessionInfo(date, isTrading, isHalf, open, close, desc);
    }

    public bool IsTradingDay(DateOnly date)
    {
        if (date.DayOfWeek == DayOfWeek.Saturday || date.DayOfWeek == DayOfWeek.Sunday)
        {
            return false;
        }

        return !_holidayCalendar.IsHoliday(date);
    }

    public bool IsTradingDay(DateTime utcTime)
    {
        var local = ToLocal(utcTime);
        var date = DateOnly.FromDateTime(local);
        return IsTradingDay(date);
    }

    public bool IsHalfDay(DateOnly date)
    {
        return IsTradingDay(date) && _holidayCalendar.IsHalfDay(date);
    }

    public TimeSpan GetMarketOpenTime(DateOnly date)
    {
        var ov = _holidayCalendar.GetOverride(date);
        return ov?.OpenTime ?? _openTime;
    }

    public TimeSpan GetMarketCloseTime(DateOnly date)
    {
        var ov = _holidayCalendar.GetOverride(date);
        return ov?.CloseTime ?? _closeTime;
    }

    public bool IsMarketOpen(DateTime utcTime)
    {
        if (!IsTradingDay(utcTime)) return false;

        var local = ToLocal(utcTime);
        var date = DateOnly.FromDateTime(local);
        var openTime = GetMarketOpenTime(date);
        var closeTime = GetMarketCloseTime(date);
        var timeOfDay = local.TimeOfDay;
        return timeOfDay >= openTime && timeOfDay < closeTime;
    }

    public DateTime GetSessionOpenUtc(DateTime dateUtc)
    {
        var local = ToLocal(dateUtc);
        var date = DateOnly.FromDateTime(local);
        var localOpen = local.Date.Add(GetMarketOpenTime(date));
        return ToUtc(localOpen);
    }

    public DateTime GetSessionOpenUtc(DateOnly date)
    {
        var openTime = GetMarketOpenTime(date);
        var localOpen = date.ToDateTime(TimeOnly.FromTimeSpan(openTime));
        return ToUtc(localOpen);
    }

    public DateTime GetSessionCloseUtc(DateTime dateUtc)
    {
        var local = ToLocal(dateUtc);
        var date = DateOnly.FromDateTime(local);
        var localClose = local.Date.Add(GetMarketCloseTime(date));
        return ToUtc(localClose);
    }

    public DateTime GetSessionCloseUtc(DateOnly date)
    {
        var localClose = date.ToDateTime(TimeOnly.FromTimeSpan(GetMarketCloseTime(date)));
        return ToUtc(localClose);
    }

    public DateTime GetDailyFinalizationUtc(DateTime dateUtc)
    {
        var local = ToLocal(dateUtc);
        var date = DateOnly.FromDateTime(local);
        var closeTime = GetMarketCloseTime(date);
        // On half-days, finalization is 15 minutes after 13:00 close (13:15)
        var finTime = IsHalfDay(date) ? closeTime.Add(TimeSpan.FromMinutes(15)) : _dailyFinalizationTime;
        var localFin = local.Date.Add(finTime);
        return ToUtc(localFin);
    }

    public DateTime GetBulletinPublicationTimeUtc(DateOnly date)
    {
        var closeTime = GetMarketCloseTime(date);
        // Official bulletin publication window opens 25 minutes after close (18:25 on full days, 13:25 on half days)
        var pubTime = closeTime.Add(TimeSpan.FromMinutes(25));
        var localPub = date.ToDateTime(TimeOnly.FromTimeSpan(pubTime));
        return ToUtc(localPub);
    }

    public DateTime GetBulletinCutoffTimeUtc(DateOnly date)
    {
        var isHalf = IsHalfDay(date);
        var cutoffStr = isHalf
            ? (_configuration?["BistBulletin:HalfDayCutoff"] ?? "16:00:00")
            : (_configuration?["BistBulletin:FullDayCutoff"] ?? "21:00:00");
        var cutoffTime = TimeSpan.TryParse(cutoffStr, out var ct)
            ? ct
            : (isHalf ? new TimeSpan(16, 0, 0) : new TimeSpan(21, 0, 0));
        var localCutoff = date.ToDateTime(TimeOnly.FromTimeSpan(cutoffTime));
        return ToUtc(localCutoff);
    }

    public DateOnly GetNextTradingDay(DateOnly date)
    {
        var candidate = date.AddDays(1);
        while (!IsTradingDay(candidate))
        {
            candidate = candidate.AddDays(1);
        }
        return candidate;
    }

    public DateOnly GetPreviousTradingDay(DateOnly date)
    {
        var candidate = date.AddDays(-1);
        while (!IsTradingDay(candidate))
        {
            candidate = candidate.AddDays(-1);
        }
        return candidate;
    }

    public bool IsClosedCandleAvailable(Timeframe timeframe, DateTime utcTime)
    {
        return GetLastClosedCandleTimeUtc(timeframe, utcTime) != null;
    }

    public DateTime? GetLastClosedCandleTimeUtc(Timeframe timeframe, DateTime utcTime)
    {
        var local = ToLocal(utcTime);
        var date = DateOnly.FromDateTime(local);

        switch (timeframe)
        {
            case Timeframe.Daily:
            {
                var isTrading = IsTradingDay(date);
                var isHalf = isTrading && IsHalfDay(date);
                var closeTime = isTrading ? GetMarketCloseTime(date) : _closeTime;
                var finTime = isHalf ? closeTime.Add(TimeSpan.FromMinutes(15)) : _dailyFinalizationTime;

                if (isTrading && local.TimeOfDay >= finTime)
                {
                    return ToUtc(local.Date);
                }

                var prevTradingDay = GetPreviousTradingDay(date);
                return ToUtc(prevTradingDay.ToDateTime(TimeOnly.MinValue));
            }

            case Timeframe.H1:
            {
                var isTrading = IsTradingDay(date);
                var openTime = isTrading ? GetMarketOpenTime(date) : _openTime;
                var closeTime = isTrading ? GetMarketCloseTime(date) : _closeTime;

                if (isTrading)
                {
                    if (local.TimeOfDay >= openTime.Add(TimeSpan.FromHours(1)))
                    {
                        var cappedTime = local.TimeOfDay >= closeTime ? closeTime : local.TimeOfDay;
                        var completedHours = (int)cappedTime.TotalHours;
                        var closedCandleTime = local.Date.AddHours(completedHours);
                        var candleOpenLocal = closedCandleTime.AddHours(-1);
                        return ToUtc(candleOpenLocal);
                    }
                }

                var prevDay = isTrading ? GetPreviousTradingDay(date) : GetPreviousTradingDay(date);
                var prevClose = GetMarketCloseTime(prevDay);
                return ToUtc(prevDay.ToDateTime(TimeOnly.FromTimeSpan(prevClose)).AddHours(-1));
            }

            case Timeframe.M15:
            {
                var isTrading = IsTradingDay(date);
                var openTime = isTrading ? GetMarketOpenTime(date) : _openTime;
                var closeTime = isTrading ? GetMarketCloseTime(date) : _closeTime;

                if (isTrading)
                {
                    if (local.TimeOfDay >= openTime.Add(TimeSpan.FromMinutes(15)))
                    {
                        var cappedTime = local.TimeOfDay >= closeTime ? closeTime : local.TimeOfDay;
                        var totalMinutes = (int)cappedTime.TotalMinutes;
                        var completed15Block = (totalMinutes / 15) * 15;
                        var closedCandleTime = local.Date.AddMinutes(completed15Block);
                        var candleOpenLocal = closedCandleTime.AddMinutes(-15);
                        return ToUtc(candleOpenLocal);
                    }
                }

                var prevDay = isTrading ? GetPreviousTradingDay(date) : GetPreviousTradingDay(date);
                var prevClose = GetMarketCloseTime(prevDay);
                return ToUtc(prevDay.ToDateTime(TimeOnly.FromTimeSpan(prevClose)).AddMinutes(-15));
            }

            default:
                return null;
        }
    }

    public DateTime ExpectedLatestBarTimeUtc(Timeframe timeframe, DateTime utcTime)
    {
        var lastClosed = GetLastClosedCandleTimeUtc(timeframe, utcTime);
        if (lastClosed.HasValue)
        {
            return lastClosed.Value;
        }

        return utcTime;
    }
}
