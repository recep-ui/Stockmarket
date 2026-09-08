using BistQuant.Domain.Enums;

namespace BistQuant.Application.DTOs.Indicators;

public record IndicatorSnapshotDto(
    string Symbol,
    Timeframe Timeframe,
    DateTime Timestamp,
    decimal? EMA20,
    decimal? EMA50,
    decimal? EMA100,
    decimal? EMA200,
    decimal? SMA20,
    decimal? SMA50,
    decimal? SMA200,
    decimal? RSI14,
    decimal? MACD,
    decimal? MACDSignal,
    decimal? MACDHistogram,
    decimal? ATR14,
    decimal? SuperTrend,
    byte? SuperTrendDirection,
    decimal? BollingerUpper,
    decimal? BollingerMiddle,
    decimal? BollingerLower,
    decimal? AverageVolume20,
    decimal? VolumeRatio,
    decimal? OBV,
    decimal? Support1,
    decimal? Support2,
    decimal? Resistance1,
    decimal? Resistance2,
    bool? IsBreakout
);
