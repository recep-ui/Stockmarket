using BistQuant.Domain.Common;
using BistQuant.Domain.Enums;

namespace BistQuant.Domain.Entities;

public class PriceBar : BaseEntity<long>
{
    public int SymbolId { get; set; }
    public Symbol Symbol { get; set; } = null!;

    public Timeframe Timeframe { get; set; }
    public DateTime Timestamp { get; set; }

    public decimal Open { get; set; }
    public decimal High { get; set; }
    public decimal Low { get; set; }
    public decimal Close { get; set; }
    public decimal Volume { get; set; }
    public decimal? AdjustedClose { get; set; }
}

public class IndicatorSnapshot : BaseEntity<long>
{
    public int SymbolId { get; set; }
    public Symbol Symbol { get; set; } = null!;

    public Timeframe Timeframe { get; set; }
    public DateTime Timestamp { get; set; }

    // Trend
    public decimal? EMA20 { get; set; }
    public decimal? EMA50 { get; set; }
    public decimal? EMA100 { get; set; }
    public decimal? EMA200 { get; set; }

    public decimal? SMA20 { get; set; }
    public decimal? SMA50 { get; set; }
    public decimal? SMA200 { get; set; }

    public decimal? SuperTrend { get; set; }
    public byte? SuperTrendDirection { get; set; } // 1 for Buy/Bullish, -1/0 for Sell/Bearish
    public decimal? ADX14 { get; set; }

    // Momentum
    public decimal? RSI14 { get; set; }
    public decimal? MACD { get; set; }
    public decimal? MACDSignal { get; set; }
    public decimal? MACDHistogram { get; set; }
    public decimal? StochasticK { get; set; }
    public decimal? StochasticD { get; set; }
    public decimal? ROC { get; set; }

    // Volatility
    public decimal? ATR14 { get; set; }
    public decimal? BollingerUpper { get; set; }
    public decimal? BollingerMiddle { get; set; }
    public decimal? BollingerLower { get; set; }

    // Volume
    public decimal? VWAP { get; set; }
    public decimal? OBV { get; set; }
    public decimal? AverageVolume20 { get; set; }
    public decimal? VolumeRatio { get; set; }

    // Price Structure & S/R
    public decimal? Support1 { get; set; }
    public decimal? Support2 { get; set; }
    public decimal? Resistance1 { get; set; }
    public decimal? Resistance2 { get; set; }
    public bool? IsBreakout { get; set; }
}
