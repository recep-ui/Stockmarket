using BistQuant.Domain.Entities;
using BistQuant.Domain.Enums;

namespace BistQuant.Application.Common.Interfaces;

public interface IMarketSessionDateResolver
{
    /// <summary>
    /// Resolves the trading session date represented by a PriceBar or timestamp.
    /// For Daily bars, PriceBar.Timestamp is a normalized session-date key (SessionDate at 00:00 UTC).
    /// </summary>
    DateOnly ResolveSessionDate(DateTime timestampUtc, Timeframe timeframe);

    /// <summary>
    /// Resolves the trading session date directly from a PriceBar.
    /// </summary>
    DateOnly ResolveSessionDate(PriceBar bar);

    /// <summary>
    /// Converts a session trading date to the standardized normalized Daily PriceBar timestamp (00:00 UTC).
    /// </summary>
    DateTime ToDailyBarTimestampUtc(DateOnly sessionDate);
}
