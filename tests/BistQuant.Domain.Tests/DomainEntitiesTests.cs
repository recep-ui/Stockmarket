using BistQuant.Domain.Entities;
using BistQuant.Domain.Enums;
using BistQuant.Domain.Models;

namespace BistQuant.Domain.Tests;

public class DomainEntitiesTests
{
    [Fact]
    public void Symbol_Instantiation_ShouldHoldProperties()
    {
        // Arrange & Act
        var symbol = new Symbol
        {
            Id = 1,
            MarketId = 10,
            Ticker = "THYAO",
            Name = "Turk Hava Yollari",
            Sector = "Transportation",
            IsActive = true
        };

        // Assert
        Assert.Equal("THYAO", symbol.Ticker);
        Assert.Equal("Turk Hava Yollari", symbol.Name);
        Assert.True(symbol.IsActive);
        Assert.NotNull(symbol.PriceBars);
        Assert.NotNull(symbol.Signals);
    }

    [Fact]
    public void TechnicalScores_Record_ShouldRetainValues()
    {
        // Arrange & Act
        var scores = new TechnicalScores(
            TotalScore: 84,
            TrendScore: 36,
            MomentumScore: 25,
            VolumeScore: 13,
            StructureScore: 10
        );

        // Assert
        Assert.Equal(84, scores.TotalScore);
        Assert.Equal(36, scores.TrendScore);
        Assert.Equal(25, scores.MomentumScore);
        Assert.Equal(13, scores.VolumeScore);
        Assert.Equal(10, scores.StructureScore);
        Assert.Equal(84, scores.TrendScore + scores.MomentumScore + scores.VolumeScore + scores.StructureScore);
    }

    [Fact]
    public void Signal_WithReasons_ShouldLinkCorrectly()
    {
        // Arrange
        var signal = new Signal
        {
            Id = 100,
            SymbolId = 1,
            Timeframe = Timeframe.Daily,
            SignalType = SignalType.StrongBuy,
            Score = 88,
            Price = 320.5m,
            StopLoss = 308.0m,
            TakeProfit1 = 338.0m,
            TakeProfit2 = 350.0m,
            RiskRewardRatio = 2.4m
        };

        var reason = new SignalReason
        {
            SignalId = 100,
            Code = "EMA_BULLISH",
            Title = "EMA20 > EMA50",
            ScoreContribution = 10,
            Indicator = "EMA",
            IndicatorValue = 315.0m
        };

        signal.Reasons.Add(reason);

        // Assert
        Assert.Equal(SignalType.StrongBuy, signal.SignalType);
        Assert.Single(signal.Reasons);
        Assert.Equal("EMA_BULLISH", signal.Reasons.First().Code);
    }
}
