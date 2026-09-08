using BistQuant.Application.Common.Interfaces;
using BistQuant.Domain.Entities;
using BistQuant.Domain.Enums;
using Microsoft.Extensions.Configuration;

namespace BistQuant.Application.Services.MarketData;

public class MarketDataFreshnessPolicy : IMarketDataFreshnessPolicy
{
    private readonly IConfiguration _configuration;
    private readonly IMarketSessionCalendar? _calendar;

    public MarketDataFreshnessPolicy(IConfiguration? configuration = null, IMarketSessionCalendar? calendar = null)
    {
        _configuration = configuration ?? new ConfigurationBuilder().Build();
        _calendar = calendar;
    }

    public FreshnessCheckResult CheckFreshness(Timeframe timeframe, DateTime barTimestamp, DateTime? asOf = null)
    {
        var referenceTime = asOf ?? DateTime.UtcNow;
        var age = referenceTime - barTimestamp;

        var allowedSkewSeconds = _configuration.GetValue<int?>("FreshnessThresholds:AllowedFutureSkewSeconds") ?? 60;
        var allowedSkew = TimeSpan.FromSeconds(allowedSkewSeconds);

        // Fail-closed future timestamp guard
        if (barTimestamp > referenceTime + allowedSkew)
        {
            return new FreshnessCheckResult(
                false,
                age,
                allowedSkew,
                $"Future timestamp / clock skew violation: bar timestamp is {(barTimestamp - referenceTime).TotalSeconds:F1}s ahead of reference time (allowed skew: {allowedSkewSeconds}s)."
            );
        }

        if (age < TimeSpan.Zero)
        {
            // Bar is slightly in the future within allowed clock skew tolerance
            return new FreshnessCheckResult(true, age, allowedSkew, $"Bar timestamp is within allowed future clock skew ({allowedSkewSeconds}s).");
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
                if (_calendar != null)
                {
                    // Daily checks compare session trading dates (DateOnly) rather than midnight timestamps against physical close time
                    var barSessionDate = DateOnly.FromDateTime(barTimestamp);
                    var turkeyTz = _calendar.MarketTimeZone;
                    var localRef = TimeZoneInfo.ConvertTimeFromUtc(referenceTime, turkeyTz);
                    var todayDate = DateOnly.FromDateTime(localRef);

                    bool isTradingToday = _calendar.IsTradingDay(todayDate);
                    DateTime pubTimeUtc = isTradingToday ? _calendar.GetBulletinPublicationTimeUtc(todayDate) : DateTime.MaxValue;

                    DateOnly expectedSessionDate;
                    if (isTradingToday && referenceTime >= pubTimeUtc)
                    {
                        expectedSessionDate = todayDate;
                    }
                    else
                    {
                        expectedSessionDate = _calendar.GetPreviousTradingDay(todayDate);
                    }

                    if (barSessionDate >= expectedSessionDate)
                    {
                        return new FreshnessCheckResult(true, age, TimeSpan.FromDays(1), $"Daily bar for session {barSessionDate:yyyy-MM-dd} is fresh for expected session {expectedSessionDate:yyyy-MM-dd}.");
                    }

                    int missedTradingDays = 0;
                    var cur = barSessionDate;
                    while (cur < expectedSessionDate && missedTradingDays < 30)
                    {
                        cur = _calendar.GetNextTradingDay(cur);
                        missedTradingDays++;
                    }

                    var maxAllowedLag = _configuration.GetValue<int?>("FreshnessThresholds:DailyAllowedSessionLag") ?? 1;
                    bool isDailyFresh = missedTradingDays <= maxAllowedLag;
                    return new FreshnessCheckResult(
                        isDailyFresh,
                        age,
                        TimeSpan.FromDays(missedTradingDays),
                        isDailyFresh
                            ? $"Daily bar session {barSessionDate:yyyy-MM-dd} is {missedTradingDays} session(s) behind expected {expectedSessionDate:yyyy-MM-dd} (allowed: {maxAllowedLag})."
                            : $"Stale: Daily bar session {barSessionDate:yyyy-MM-dd} is {missedTradingDays} trading session(s) behind expected {expectedSessionDate:yyyy-MM-dd}."
                    );
                }

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
