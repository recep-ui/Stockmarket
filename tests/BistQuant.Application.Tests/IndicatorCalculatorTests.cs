using BistQuant.Application.Services.Indicators;
using BistQuant.Domain.Entities;
using BistQuant.Domain.Enums;

namespace BistQuant.Application.Tests;

public class IndicatorCalculatorTests
{
    [Fact]
    public void CalculateSma_ShouldComputeAccurateAverages()
    {
        // Arrange
        var prices = new List<decimal> { 10, 20, 30, 40, 50 };

        // Act
        var sma3 = IndicatorCalculators.CalculateSma(prices, 3);

        // Assert
        Assert.Null(sma3[0]);
        Assert.Null(sma3[1]);
        Assert.Equal(20m, sma3[2]); // (10+20+30)/3 = 20
        Assert.Equal(30m, sma3[3]); // (20+30+40)/3 = 30
        Assert.Equal(40m, sma3[4]); // (30+40+50)/3 = 40
    }

    [Fact]
    public void CalculateEma_ShouldSmoothPricesDeterministically()
    {
        // Arrange
        var prices = new List<decimal> { 10, 11, 12, 13, 14, 15, 16, 17, 18, 19 };

        // Act
        var ema5 = IndicatorCalculators.CalculateEma(prices, 5);

        // Assert
        Assert.Null(ema5[0]);
        Assert.Null(ema5[3]);
        Assert.NotNull(ema5[4]); // First value is SMA of first 5 items = (10+11+12+13+14)/5 = 12.0
        Assert.Equal(12.0m, ema5[4]);
        Assert.True(ema5[5] > 12.0m); // Follows upward trend
    }

    [Fact]
    public void CalculateRsi_OnUptrend_ShouldBeGreaterThan50()
    {
        // Arrange: Monotonically increasing prices
        var prices = new List<decimal>();
        for (int i = 0; i < 30; i++)
        {
            prices.Add(100 + i * 2);
        }

        // Act
        var rsi = IndicatorCalculators.CalculateRsi(prices, 14);

        // Assert
        var lastRsi = rsi[^1];
        Assert.NotNull(lastRsi);
        Assert.Equal(100m, lastRsi.Value); // Pure gains = 100 RSI
    }

    [Fact]
    public void CalculateAtr_ShouldReturnPositiveVolatility()
    {
        // Arrange
        var bars = new List<PriceBar>();
        var date = DateTime.UtcNow.AddDays(-25);
        for (int i = 0; i < 25; i++)
        {
            bars.Add(new PriceBar
            {
                Timestamp = date.AddDays(i),
                Open = 100,
                High = 105,
                Low = 95,
                Close = 102,
                Volume = 1000
            });
        }

        // Act
        var atr = IndicatorCalculators.CalculateAtr(bars, 14);

        // Assert
        var lastAtr = atr[^1];
        Assert.NotNull(lastAtr);
        Assert.Equal(10.0m, lastAtr.Value); // High - Low = 10 every day
    }

    [Fact]
    public void CalculateBollingerBands_ShouldEncloseMiddleSma()
    {
        // Arrange
        var prices = new List<decimal>();
        for (int i = 0; i < 30; i++)
        {
            prices.Add(100 + (i % 5));
        }

        // Act
        var bb = IndicatorCalculators.CalculateBollingerBands(prices, 20, 2.0m);

        // Assert
        var last = bb[^1];
        Assert.NotNull(last.Upper);
        Assert.NotNull(last.Middle);
        Assert.NotNull(last.Lower);
        Assert.True(last.Upper > last.Middle);
        Assert.True(last.Middle > last.Lower);
    }

    [Fact]
    public void DetectSupportResistance_OnBreakout_ShouldDetectTrue()
    {
        // Arrange
        var bars = new List<PriceBar>();
        var date = DateTime.UtcNow.AddDays(-30);
        for (int i = 0; i < 25; i++)
        {
            bars.Add(new PriceBar
            {
                Timestamp = date.AddDays(i),
                Open = 100,
                High = 110,
                Low = 95,
                Close = 105,
                Volume = 1000
            });
        }

        // 26th bar breaks above previous 110 high with high close
        bars.Add(new PriceBar
        {
            Timestamp = date.AddDays(26),
            Open = 109,
            High = 120,
            Low = 108,
            Close = 118,
            Volume = 3000
        });

        // Act
        var sr = IndicatorCalculators.DetectSupportResistance(bars, 20);

        // Assert
        Assert.True(sr.IsBreakout);
        Assert.NotNull(sr.Support1);
        Assert.NotNull(sr.Resistance1);
    }
}
