using BistQuant.Application.Common.Interfaces;
using BistQuant.Domain.Entities;
using BistQuant.Domain.Enums;

namespace BistQuant.Application.Services.MarketData;

public class MarketSessionDateResolver : IMarketSessionDateResolver
{
    public DateOnly ResolveSessionDate(DateTime timestampUtc, Timeframe timeframe)
    {
        if (timeframe == Timeframe.Daily)
        {
            // Daily bars are normalized to SessionDate at 00:00 UTC
            return DateOnly.FromDateTime(timestampUtc);
        }

        // Intraday bars: convert UTC timestamp to Europe/Istanbul to get the trading session date
        var turkeyZone = TimeZoneInfo.FindSystemTimeZoneById("Europe/Istanbul");
        var localTime = TimeZoneInfo.ConvertTimeFromUtc(timestampUtc, turkeyZone);
        return DateOnly.FromDateTime(localTime);
    }

    public DateOnly ResolveSessionDate(PriceBar bar)
    {
        return ResolveSessionDate(bar.Timestamp, bar.Timeframe);
    }

    public DateTime ToDailyBarTimestampUtc(DateOnly sessionDate)
    {
        return sessionDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
    }
}
