using BistQuant.Domain.Enums;

namespace BistQuant.Application.Common.Interfaces;

public interface IMarketSessionCalendar
{
    TimeZoneInfo MarketTimeZone { get; }
    TimeSpan SessionOpenTime { get; }
    TimeSpan SessionCloseTime { get; }
    TimeSpan DailyFinalizationTime { get; }

    bool IsTradingDay(DateTime utcTime);
    bool IsMarketOpen(DateTime utcTime);
    DateTime GetSessionOpenUtc(DateTime dateUtc);
    DateTime GetSessionCloseUtc(DateTime dateUtc);
    DateTime GetDailyFinalizationUtc(DateTime dateUtc);
    bool IsClosedCandleAvailable(Timeframe timeframe, DateTime utcTime);
    DateTime? GetLastClosedCandleTimeUtc(Timeframe timeframe, DateTime utcTime);
    DateTime ExpectedLatestBarTimeUtc(Timeframe timeframe, DateTime utcTime);
}
