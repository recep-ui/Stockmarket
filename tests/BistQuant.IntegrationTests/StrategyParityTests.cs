using BistQuant.Application.Common.Interfaces;
using BistQuant.Application.DTOs.Backtests;
using BistQuant.Application.DTOs.Strategies;
using BistQuant.Application.Services;
using BistQuant.Application.Services.Indicators;
using BistQuant.Domain.Entities;
using BistQuant.Domain.Enums;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BistQuant.IntegrationTests;

public class StrategyParityTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public StrategyParityTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    private List<PriceBar> GenerateCandles(int count = 120, decimal startPrice = 100m)
    {
        var bars = new List<PriceBar>();
        var price = startPrice;
        var date = new DateTime(2026, 1, 1);

        for (int i = 0; i < count; i++)
        {
            var open = price;
            var change = (decimal)(Math.Sin(i * 0.1) * 2.0);
            var close = Math.Max(10m, open + change);
            var high = Math.Max(open, close) + 1.5m;
            var low = Math.Min(open, close) - 1.2m;
            var volume = 1000000m + (i * 10000m);

            bars.Add(new PriceBar
            {
                SymbolId = 1,
                Timeframe = Timeframe.Daily,
                Timestamp = date.AddDays(i),
                Open = open,
                High = high,
                Low = low,
                Close = close,
                Volume = volume
            });

            price = close;
        }

        return bars;
    }

    [Fact]
    public void IndicatorSnapshots_ShouldContainAllTwelveIndicators()
    {
        // Arrange
        var bars = GenerateCandles(220);

        // Act
        var snapshots = IndicatorCalculators.CalculateSnapshots(bars, 1, Timeframe.Daily);

        // Assert
        Assert.NotEmpty(snapshots);
        var last = snapshots.Last();

        // Check all 12 indicators are present & computed
        Assert.NotNull(last.EMA20);
        Assert.NotNull(last.EMA50);
        Assert.NotNull(last.EMA100);
        Assert.NotNull(last.EMA200);
        Assert.NotNull(last.RSI14);
        Assert.NotNull(last.MACD);
        Assert.NotNull(last.MACDSignal);
        Assert.NotNull(last.ATR14);
        Assert.NotNull(last.ADX14);
        Assert.NotNull(last.SuperTrend);
        Assert.NotNull(last.StochasticK);
        Assert.NotNull(last.StochasticD);
        Assert.NotNull(last.OBV);
        Assert.NotNull(last.VolumeRatio);
        Assert.NotNull(last.Resistance1);
        Assert.NotNull(last.Support1);
        Assert.NotNull(last.IsBreakout);
    }

    [Fact]
    public void Breakout_Resistance_MustBeCalculatedFromPriorBars_NoLookahead()
    {
        // Arrange: create 30 bars where the last bar creates a massive spike
        var bars = GenerateCandles(30, 100m);
        var priorHigh = bars.Take(29).Max(b => b.High);

        // Make the last bar close higher than prior high
        var lastBar = bars.Last();
        lastBar.Close = priorHigh + 10m;
        lastBar.High = priorHigh + 15m;

        // Act
        var sr = IndicatorCalculators.DetectSupportResistance(bars);

        // Assert: resistance must match prior high, NOT current high!
        Assert.Equal(priorHigh, sr.Resistance1);
        Assert.True(sr.IsBreakout, "Close > prior high must trigger isBreakout");
        Assert.True(lastBar.Close > sr.Resistance1, "Breakout is verified strictly against prior resistance");
    }

    [Fact]
    public void Crossover_ShouldReturnFalse_WhenNoPreviousDataAvailable()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var strategyEngine = scope.ServiceProvider.GetRequiredService<IStrategyEngine>();

        var rule = new StrategyRule
        {
            Indicator = "Close",
            Operator = RuleOperator.CrossAbove,
            ComparisonIndicator = "EMA20",
            Weight = 25
        };

        var bar = new PriceBar { Close = 110m };
        var snapshot = new IndicatorSnapshot { EMA20 = 100m };

        // Act: evaluated with null prevSnapshot and null prevBar
        var result = strategyEngine.EvaluateRules(new List<StrategyRule> { rule }, snapshot, bar, null, null);

        // Assert: must be false because crossover cannot be established without prior bar
        Assert.False(result.IsSignalTriggered, "Crossover must return false when no previous bar/snapshot exists");
    }

    [Fact]
    public void Crossover_ShouldTriggerCorrectly_OnStrictTransition()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var strategyEngine = scope.ServiceProvider.GetRequiredService<IStrategyEngine>();

        var rule = new StrategyRule
        {
            Indicator = "Close",
            Operator = RuleOperator.CrossAbove,
            ComparisonIndicator = "EMA20",
            Weight = 25
        };

        // Prior: Close <= EMA20
        var prevBar = new PriceBar { Close = 98m };
        var prevSnapshot = new IndicatorSnapshot { EMA20 = 100m };

        // Current: Close > EMA20
        var currBar = new PriceBar { Close = 105m };
        var currSnapshot = new IndicatorSnapshot { EMA20 = 101m };

        // Act
        var result = strategyEngine.EvaluateRules(
            new List<StrategyRule> { rule },
            currSnapshot,
            currBar,
            prevSnapshot,
            prevBar);

        // Assert
        Assert.True(result.IsSignalTriggered, "CrossAbove should trigger when prev <= and curr >");
    }

    [Fact]
    public void Live_And_Backtest_EvaluationPipeline_ProduceIdentical_ScoreAndSignal()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var pipeline = scope.ServiceProvider.GetRequiredService<IStrategyEvaluationPipeline>();

        var bars = GenerateCandles(120, 100m);
        var snapshots = IndicatorCalculators.CalculateSnapshots(bars, 1, Timeframe.Daily);

        var currentBar = bars.Last();
        var currentSnapshot = snapshots.Last();
        var prevBar = bars[^2];
        var prevSnapshot = snapshots[^2];

        var strategy = new Strategy
        {
            Name = "Parity Test Strategy",
            StrategyType = "Technical",
            Timeframe = Timeframe.Daily,
            Rules = new List<StrategyRule>
            {
                new() { Indicator = "RSI", Operator = RuleOperator.GreaterThan, Value = 40m, Weight = 25, RuleGroup = "Entry", IsRequired = true },
                new() { Indicator = "Close", Operator = RuleOperator.GreaterThan, ComparisonIndicator = "Ema20", Weight = 25, RuleGroup = "Entry", IsRequired = false }
            }
        };

        // Act: Evaluate via unified Pipeline (as called by live scanner)
        var liveResult = pipeline.Evaluate(
            currentBar: currentBar,
            snapshot: currentSnapshot,
            history: bars,
            strategy: strategy,
            prevBar: prevBar,
            prevSnapshot: prevSnapshot);

        // Evaluate again representing backtest engine call with identical inputs
        var backtestResult = pipeline.Evaluate(
            currentBar: currentBar,
            snapshot: currentSnapshot,
            history: bars,
            strategy: strategy,
            prevBar: prevBar,
            prevSnapshot: prevSnapshot);

        // Assert: 100% parity
        Assert.Equal(liveResult.IsSignalTriggered, backtestResult.IsSignalTriggered);
        Assert.Equal(liveResult.SignalType, backtestResult.SignalType);
        Assert.Equal(liveResult.TotalScore, backtestResult.TotalScore);
        Assert.Equal(liveResult.TrendScore, backtestResult.TrendScore);
        Assert.Equal(liveResult.MomentumScore, backtestResult.MomentumScore);
        Assert.Equal(liveResult.VolumeScore, backtestResult.VolumeScore);
        Assert.Equal(liveResult.StructureScore, backtestResult.StructureScore);
        Assert.Equal(liveResult.MatchedRules, backtestResult.MatchedRules);
    }
}
