using BistQuant.Application.Common.Interfaces;
using BistQuant.Domain.Entities;
using BistQuant.Domain.Enums;
using Microsoft.Extensions.Configuration;

namespace BistQuant.Application.Services.MarketData;

public class MarketDataFreshnessPolicy : IMarketDataFreshnessPolicy
{
    private readonly IConfiguration _configuration;

    public MarketDataFreshnessPolicy(IConfiguration? configuration = null)
    {
        _configuration = configuration ?? new ConfigurationBuilder().Build();
    }

    public FreshnessCheckResult CheckFreshness(Timeframe timeframe, DateTime barTimestamp, DateTime? asOf = null)
    {
        var referenceTime = asOf ?? DateTime.UtcNow;
        var age = referenceTime - barTimestamp;

        if (age < TimeSpan.Zero)
        {
            // Bar is in the future relative to reference time (data clock skew)
            return new FreshnessCheckResult(true, age, TimeSpan.FromMinutes(1), "Bar timestamp is current/future.");
        }

        TimeSpan maxAge;
        switch (timeframe)
        {
            case Timeframe.M1:
                var m1 = _configuration.GetValue<int?>("FreshnessThresholds:M1Minutes") ?? 3;
                maxAge = TimeSpan.FromMinutes(m1);
                break;

            case Timeframe.M5:
                var m5 = _configuration.GetValue<int?>("FreshnessThresholds:M5Minutes") ?? 15;
                maxAge = TimeSpan.FromMinutes(m5);
                break;

            case Timeframe.M15:
                var m15 = _configuration.GetValue<int?>("FreshnessThresholds:M15Minutes") ?? 45;
                maxAge = TimeSpan.FromMinutes(m15);
                break;

            case Timeframe.M30:
                var m30 = _configuration.GetValue<int?>("FreshnessThresholds:M30Minutes") ?? 90;
                maxAge = TimeSpan.FromMinutes(m30);
                break;

            case Timeframe.H1:
                var h1 = _configuration.GetValue<int?>("FreshnessThresholds:H1Hours") ?? 3;
                maxAge = TimeSpan.FromHours(h1);
                break;

            case Timeframe.H4:
                var h4 = _configuration.GetValue<int?>("FreshnessThresholds:H4Hours") ?? 12;
                maxAge = TimeSpan.FromHours(h4);
                break;

            case Timeframe.Daily:
                var daily = _configuration.GetValue<int?>("FreshnessThresholds:DailyDays") ?? 4;
                maxAge = TimeSpan.FromDays(daily);
                break;

            case Timeframe.Weekly:
                var weekly = _configuration.GetValue<int?>("FreshnessThresholds:WeeklyDays") ?? 14;
                maxAge = TimeSpan.FromDays(weekly);
                break;

            default:
                // FAIL CLOSED: Unknown or unsupported timeframe must never fall back to unsafe tolerance
                return new FreshnessCheckResult(
                    false,
                    age,
                    TimeSpan.Zero,
                    $"Unsupported timeframe '{(byte)timeframe}'. Failed closed for safety."
                );
        }

        bool isFresh = age <= maxAge;
        string details = isFresh
            ? $"Fresh (Age: {age.TotalHours:F1}h <= Max: {maxAge.TotalHours:F1}h)"
            : $"Stale (Age: {age.TotalHours:F1}h > Max: {maxAge.TotalHours:F1}h)";

        return new FreshnessCheckResult(isFresh, age, maxAge, details);
    }

    public bool IsFresh(PriceBar bar, DateTime? asOf = null)
    {
        if (bar == null) return false;
        return CheckFreshness(bar.Timeframe, bar.Timestamp, asOf).IsFresh;
    }
}
