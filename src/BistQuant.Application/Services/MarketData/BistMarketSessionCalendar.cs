using BistQuant.Application.Common.Interfaces;
using BistQuant.Domain.Enums;
using Microsoft.Extensions.Configuration;

namespace BistQuant.Application.Services.MarketData;

public class BistMarketSessionCalendar : IMarketSessionCalendar
{
    private readonly IHolidayCalendar _holidayCalendar;
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

    public bool IsTradingDay(DateTime utcTime)
    {
        var local = ToLocal(utcTime);
        if (local.DayOfWeek == DayOfWeek.Saturday || local.DayOfWeek == DayOfWeek.Sunday)
        {
            return false;
        }

        var date = DateOnly.FromDateTime(local);
        return !_holidayCalendar.IsHoliday(date);
    }

    public bool IsMarketOpen(DateTime utcTime)
    {
        if (!IsTradingDay(utcTime)) return false;

        var local = ToLocal(utcTime);
        var timeOfDay = local.TimeOfDay;
        return timeOfDay >= _openTime && timeOfDay < _closeTime;
    }

    public DateTime GetSessionOpenUtc(DateTime dateUtc)
    {
        var local = ToLocal(dateUtc);
        var localOpen = local.Date.Add(_openTime);
        return ToUtc(localOpen);
    }

    public DateTime GetSessionCloseUtc(DateTime dateUtc)
    {
        var local = ToLocal(dateUtc);
        var localClose = local.Date.Add(_closeTime);
        return ToUtc(localClose);
    }

    public DateTime GetDailyFinalizationUtc(DateTime dateUtc)
    {
        var local = ToLocal(dateUtc);
        var localFin = local.Date.Add(_dailyFinalizationTime);
        return ToUtc(localFin);
    }

    public bool IsClosedCandleAvailable(Timeframe timeframe, DateTime utcTime)
    {
        return GetLastClosedCandleTimeUtc(timeframe, utcTime) != null;
    }

    public DateTime? GetLastClosedCandleTimeUtc(Timeframe timeframe, DateTime utcTime)
    {
        var local = ToLocal(utcTime);

        switch (timeframe)
        {
            case Timeframe.Daily:
            {
                if (IsTradingDay(utcTime) && local.TimeOfDay >= _dailyFinalizationTime)
                {
                    return ToUtc(local.Date);
                }

                var prevTradingDay = GetPreviousTradingDay(local.Date);
                return ToUtc(prevTradingDay);
            }

            case Timeframe.H1:
            {
                if (IsTradingDay(utcTime))
                {
                    if (local.TimeOfDay >= _openTime.Add(TimeSpan.FromHours(1)))
                    {
                        var cappedTime = local.TimeOfDay >= _closeTime ? _closeTime : local.TimeOfDay;
                        var completedHours = (int)cappedTime.TotalHours;
                        var closedCandleTime = local.Date.AddHours(completedHours);
                        var candleOpenLocal = closedCandleTime.AddHours(-1);
                        return ToUtc(candleOpenLocal);
                    }
                }

                var prevDay = IsTradingDay(utcTime) ? GetPreviousTradingDay(local.Date) : GetLastTradingDayOnOrBefore(local.Date.AddDays(-1));
                return ToUtc(prevDay.Add(_closeTime).AddHours(-1));
            }

            case Timeframe.M15:
            {
                if (IsTradingDay(utcTime))
                {
                    if (local.TimeOfDay >= _openTime.Add(TimeSpan.FromMinutes(15)))
                    {
                        var cappedTime = local.TimeOfDay >= _closeTime ? _closeTime : local.TimeOfDay;
                        var totalMinutes = (int)cappedTime.TotalMinutes;
                        var completed15Block = (totalMinutes / 15) * 15;
                        var closedCandleTime = local.Date.AddMinutes(completed15Block);
                        var candleOpenLocal = closedCandleTime.AddMinutes(-15);
                        return ToUtc(candleOpenLocal);
                    }
                }

                var prevDay = IsTradingDay(utcTime) ? GetPreviousTradingDay(local.Date) : GetLastTradingDayOnOrBefore(local.Date.AddDays(-1));
                return ToUtc(prevDay.Add(_closeTime).AddMinutes(-15));
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

    private DateTime GetPreviousTradingDay(DateTime localDate)
    {
        var candidate = localDate.AddDays(-1);
        while (candidate.DayOfWeek == DayOfWeek.Saturday || candidate.DayOfWeek == DayOfWeek.Sunday || _holidayCalendar.IsHoliday(DateOnly.FromDateTime(candidate)))
        {
            candidate = candidate.AddDays(-1);
        }
        return candidate.Date;
    }

    private DateTime GetLastTradingDayOnOrBefore(DateTime localDate)
    {
        var candidate = localDate;
        while (candidate.DayOfWeek == DayOfWeek.Saturday || candidate.DayOfWeek == DayOfWeek.Sunday || _holidayCalendar.IsHoliday(DateOnly.FromDateTime(candidate)))
        {
            candidate = candidate.AddDays(-1);
        }
        return candidate.Date;
    }
}
