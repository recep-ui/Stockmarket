using BistQuant.Application.Services.MarketData;
using BistQuant.Domain.Entities;
using BistQuant.Domain.Enums;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace BistQuant.Application.Tests;

public class MarketDataFreshnessTests
{
    private static MarketDataFreshnessPolicy CreatePolicy()
    {
        var dict = new Dictionary<string, string?>
        {
            ["FreshnessThresholds:M1Minutes"] = "3",
            ["FreshnessThresholds:M5Minutes"] = "15",
            ["FreshnessThresholds:M15Minutes"] = "45",
            ["FreshnessThresholds:M30Minutes"] = "90",
            ["FreshnessThresholds:H1Hours"] = "3",
            ["FreshnessThresholds:H4Hours"] = "12",
            ["FreshnessThresholds:DailyDays"] = "4",
            ["FreshnessThresholds:WeeklyDays"] = "14"
        };
        var config = new ConfigurationBuilder().AddInMemoryCollection(dict).Build();
        return new MarketDataFreshnessPolicy(config);
    }

    [Theory]
    [InlineData(Timeframe.M1, 2, true)]
    [InlineData(Timeframe.M1, 10, false)]
    [InlineData(Timeframe.M5, 10, true)]
    [InlineData(Timeframe.M5, 25, false)]
    [InlineData(Timeframe.M15, 30, true)]
    [InlineData(Timeframe.M15, 60, false)]
    [InlineData(Timeframe.M30, 60, true)]
    [InlineData(Timeframe.M30, 120, false)]
    [InlineData(Timeframe.H1, 120, true)] // 2 hours <= 3 hours
    [InlineData(Timeframe.H1, 240, false)] // 4 hours > 3 hours
    [InlineData(Timeframe.H4, 600, true)] // 10 hours <= 12 hours
    [InlineData(Timeframe.H4, 800, false)] // 13.3 hours > 12 hours
    public void CheckFreshness_IntradayTimeframes_CorrectlyEvaluated(Timeframe timeframe, int ageMinutes, bool expectedFresh)
    {
        var policy = CreatePolicy();
        var now = new DateTime(2026, 9, 8, 12, 0, 0, DateTimeKind.Utc);
        var barTime = now.AddMinutes(-ageMinutes);

        var result = policy.CheckFreshness(timeframe, barTime, asOf: now);

        Assert.Equal(expectedFresh, result.IsFresh);
    }

    [Fact]
    public void CheckFreshness_DailyAndWeeklyTimeframes_CorrectlyEvaluated()
    {
        var policy = CreatePolicy();
        var now = new DateTime(2026, 9, 8, 12, 0, 0, DateTimeKind.Utc);

        // Daily: 3 days ago is fresh (accounts for weekend/holiday)
        var freshDaily = policy.CheckFreshness(Timeframe.Daily, now.AddDays(-3), now);
        Assert.True(freshDaily.IsFresh);

        // Daily: 5 days ago is stale
        var staleDaily = policy.CheckFreshness(Timeframe.Daily, now.AddDays(-5), now);
        Assert.False(staleDaily.IsFresh);

        // Weekly: 10 days ago is fresh
        var freshWeekly = policy.CheckFreshness(Timeframe.Weekly, now.AddDays(-10), now);
        Assert.True(freshWeekly.IsFresh);

        // Weekly: 16 days ago is stale
        var staleWeekly = policy.CheckFreshness(Timeframe.Weekly, now.AddDays(-16), now);
        Assert.False(staleWeekly.IsFresh);
    }

    [Fact]
    public void CheckFreshness_UnknownTimeframe_FailsClosed()
    {
        var policy = CreatePolicy();
        var now = DateTime.UtcNow;

        // An invalid / casted timeframe value
        var unknownTf = (Timeframe)99;

        var result = policy.CheckFreshness(unknownTf, now.AddMinutes(-1), now);

        Assert.False(result.IsFresh, "Unknown timeframe must fail closed.");
        Assert.Contains("Unsupported", result.Details);
    }

    [Fact]
    public void IsFresh_PriceBar_EvaluatesAccurately()
    {
        var policy = CreatePolicy();
        var bar = new PriceBar
        {
            Timeframe = Timeframe.Daily,
            Timestamp = DateTime.UtcNow.Date
        };

        Assert.True(policy.IsFresh(bar));
        Assert.False(policy.IsFresh(null!));
    }
}
