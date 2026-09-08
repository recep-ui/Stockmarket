using BistQuant.Domain.Entities;
using BistQuant.Domain.Enums;

namespace BistQuant.Application.Common.Interfaces;

public record FreshnessCheckResult(
    bool IsFresh,
    TimeSpan Age,
    TimeSpan MaxAllowedAge,
    string Details
);

public interface IMarketDataFreshnessPolicy
{
    FreshnessCheckResult CheckFreshness(Timeframe timeframe, DateTime barTimestamp, DateTime? asOf = null);
    bool IsFresh(PriceBar bar, DateTime? asOf = null);
}
