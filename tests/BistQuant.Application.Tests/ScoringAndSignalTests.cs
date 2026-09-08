using BistQuant.Application.Services;
using BistQuant.Domain.Entities;
using BistQuant.Domain.Enums;
using Microsoft.Extensions.Configuration;

namespace BistQuant.Application.Tests;

public class ScoringAndSignalTests
{
    private readonly IConfiguration _config;
    private readonly ScoringEngine _scoringEngine;

    public ScoringAndSignalTests()
    {
        var inMemorySettings = new Dictionary<string, string?>
        {
            { "Scoring:Trend:CloseAboveEma20", "5" },
            { "Scoring:Trend:Ema20AboveEma50", "10" },
            { "Scoring:Trend:Ema50AboveEma200", "10" },
            { "Scoring:Trend:SuperTrendBuy", "10" },
            { "Scoring:Trend:AdxScore", "5" },
            { "Scoring:Momentum:RsiScore", "10" },
            { "Scoring:Momentum:MacdAboveSignal", "10" },
            { "Scoring:Momentum:MacdBullishCross", "5" },
            { "Scoring:Volume:VolumeRatioScore2", "10" },
            { "Scoring:Volume:ObvRisingScore", "5" },
            { "Scoring:Structure:ResistanceBreakout", "5" },
            { "Scoring:Structure:HigherHigh", "5" },
            { "Scoring:Structure:HigherLow", "5" }
        };

        _config = new ConfigurationBuilder().AddInMemoryCollection(inMemorySettings).Build();
        _scoringEngine = new ScoringEngine(_config);
    }

    [Fact]
    public void Evaluate_BullishSetup_ShouldProduceHighPositiveScoreWithReasons()
    {
        // Arrange
        var currentBar = new PriceBar
        {
            Close = 325m,
            High = 328m,
            Low = 320m,
            Volume = 5000000m
        };

        var snapshot = new IndicatorSnapshot
        {
            EMA20 = 310m,
            EMA50 = 300m,
            EMA200 = 280m,
            SuperTrendDirection = 1,
            SuperTrend = 305m,
            ADX14 = 28m,
            RSI14 = 58m,
            MACD = 4.2m,
            MACDSignal = 2.1m,
            MACDHistogram = 2.1m,
            VolumeRatio = 1.6m,
            OBV = 100000m,
            IsBreakout = true,
            Resistance1 = 322m,
            Support1 = 312m
        };

        var history = new List<PriceBar>
        {
            new() { High = 322m, Low = 318m, Close = 320m },
            currentBar
        };

        // Act
        var result = _scoringEngine.Evaluate(currentBar, snapshot, history);

        // Assert
        Assert.True(result.Scores.TotalScore >= 80);
        Assert.True(result.Scores.TrendScore > 0);
        Assert.True(result.Scores.MomentumScore > 0);
        Assert.True(result.Scores.VolumeScore > 0);
        Assert.NotEmpty(result.Reasons);
        Assert.Contains(result.Reasons, r => r.Code == "CLOSE_ABOVE_EMA20");
        Assert.Contains(result.Reasons, r => r.Code == "GOLDEN_STACK");
        Assert.Contains(result.Reasons, r => r.Code == "RESISTANCE_BREAKOUT");
    }

    [Theory]
    [InlineData(95, SignalType.StrongBuy)]
    [InlineData(85, SignalType.StrongBuy)]
    [InlineData(80, SignalType.Buy)]
    [InlineData(75, SignalType.Buy)]
    [InlineData(70, SignalType.BuyCandidate)]
    [InlineData(55, SignalType.Watch)]
    [InlineData(40, SignalType.Weak)]
    [InlineData(25, SignalType.Sell)]
    [InlineData(15, SignalType.StrongSell)]
    public void ClassifySignal_LegacyFallback_ShouldMapThresholdsAccurately(int score, SignalType expected)
    {
        var signalEngine = new SignalEngine(null!, _scoringEngine, null!, null!);
        var classification = signalEngine.ClassifySignal(score);
        Assert.Equal(expected, classification);
    }

    [Fact]
    public void ClassifySignal_LowScoreWithoutBearishEvidence_MustReturnWeakNotSell()
    {
        var signalEngine = new SignalEngine(null!, _scoringEngine, null!, null!);
        var snapshot = new IndicatorSnapshot
        {
            EMA20 = 100m,
            EMA50 = 95m, // Bullish
            MACDHistogram = 0.5m, // Bullish
            RSI14 = 50m
        };

        // Low score (e.g. 25) without bearish evidence should be Weak, not Sell
        var signal = signalEngine.ClassifySignal(25, snapshot, null);
        Assert.Equal(SignalType.Weak, signal);
    }

    [Fact]
    public void ClassifySignal_LowScoreWithActiveBearishEvidence_ReturnsSell()
    {
        var signalEngine = new SignalEngine(null!, _scoringEngine, null!, null!);
        var snapshot = new IndicatorSnapshot
        {
            EMA20 = 90m,
            EMA50 = 100m, // Bearish (1)
            MACDHistogram = -1.5m, // Bearish (2)
            RSI14 = 50m // Neutral
        };

        var signal = signalEngine.ClassifySignal(30, snapshot, null);
        Assert.Equal(SignalType.Sell, signal);
    }

    [Fact]
    public void ClassifySignal_LowScoreWithStrongBearishEvidence_ReturnsStrongSell()
    {
        var signalEngine = new SignalEngine(null!, _scoringEngine, null!, null!);
        var snapshot = new IndicatorSnapshot
        {
            EMA20 = 90m,
            EMA50 = 100m, // Bearish (1)
            MACDHistogram = -1.5m, // Bearish (2)
            MACD = -2m,
            MACDSignal = -0.5m, // Bearish (3)
            RSI14 = 38m // Bearish (4)
        };

        var signal = signalEngine.ClassifySignal(15, snapshot, null);
        Assert.Equal(SignalType.StrongSell, signal);
    }

    [Fact]
    public void ScoringEngine_TheoreticalMaximumScore_MustNeverExceed100()
    {
        var currentBar = new PriceBar
        {
            Close = 500m,
            High = 505m,
            Low = 490m,
            Volume = 10000000m
        };

        var snapshot = new IndicatorSnapshot
        {
            EMA20 = 480m,
            EMA50 = 460m,
            EMA200 = 400m,
            SuperTrendDirection = 1,
            SuperTrend = 470m,
            ADX14 = 35m,
            RSI14 = 60m,
            MACD = 10m,
            MACDSignal = 5m,
            MACDHistogram = 5m,
            StochasticK = 75m,
            StochasticD = 70m,
            VolumeRatio = 3.0m,
            OBV = 9999999m,
            IsBreakout = true,
            Resistance1 = 495m,
            Support1 = 480m
        };

        var history = new List<PriceBar>
        {
            new() { High = 480m, Low = 460m, Close = 470m },
            new() { High = 490m, Low = 470m, Close = 485m },
            currentBar
        };

        var result = _scoringEngine.Evaluate(currentBar, snapshot, history);
        Assert.InRange(result.Scores.TotalScore, 0, 100);
        Assert.Equal(100, result.Scores.TotalScore); // Exact theoretical max is 100
    }
}
