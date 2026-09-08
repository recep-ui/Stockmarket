using BistQuant.Domain.Enums;

namespace BistQuant.Application.Common.Interfaces;

public record ScheduledScanJob(
    Timeframe Timeframe,
    DateTime ScheduledTimeUtc,
    string Reason
);

public interface IMarketScanScheduler
{
    bool IsTimeframeEnabled(Timeframe timeframe);
    List<Timeframe> GetDueTimeframes(DateTime asOfUtc, IDictionary<Timeframe, DateTime> lastCompletedMap);
    TimeSpan GetNextCheckInterval();
}
