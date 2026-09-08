using BistQuant.Domain.Enums;

namespace BistQuant.Application.Common.Interfaces;

public record MarketSessionOverride(
    DateOnly Date,
    bool IsTradingDay,
    TimeSpan? OpenTime,
    TimeSpan? CloseTime,
    string Reason
);

public record MarketSessionInfo(
    DateOnly Date,
    bool IsTradingDay,
    bool IsHalfDay,
    TimeSpan OpenTime,
    TimeSpan CloseTime,
    string Description
);

public interface IMarketSessionCalendar
{
    TimeZoneInfo MarketTimeZone { get; }
    TimeSpan SessionOpenTime { get; }
    TimeSpan SessionCloseTime { get; }
    TimeSpan DailyFinalizationTime { get; }

    MarketSessionInfo GetSessionInfo(DateOnly date);
    bool IsTradingDay(DateOnly date);
    bool IsTradingDay(DateTime utcTime);
    bool IsHalfDay(DateOnly date);
    TimeSpan GetMarketOpenTime(DateOnly date);
    TimeSpan GetMarketCloseTime(DateOnly date);
    DateTime GetSessionOpenUtc(DateTime dateUtc);
    DateTime GetSessionOpenUtc(DateOnly date);
    DateTime GetSessionCloseUtc(DateTime dateUtc);
    DateTime GetSessionCloseUtc(DateOnly date);
    DateTime GetDailyFinalizationUtc(DateTime dateUtc);
    DateTime GetBulletinPublicationTimeUtc(DateOnly date);
    DateTime GetBulletinCutoffTimeUtc(DateOnly date);

    DateOnly GetNextTradingDay(DateOnly date);
    DateOnly GetPreviousTradingDay(DateOnly date);

    bool IsMarketOpen(DateTime utcTime);
    bool IsClosedCandleAvailable(Timeframe timeframe, DateTime utcTime);
    DateTime? GetLastClosedCandleTimeUtc(Timeframe timeframe, DateTime utcTime);
    DateTime ExpectedLatestBarTimeUtc(Timeframe timeframe, DateTime utcTime);
}
