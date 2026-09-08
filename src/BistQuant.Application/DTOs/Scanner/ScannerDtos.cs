using BistQuant.Domain.Enums;

namespace BistQuant.Application.DTOs.Scanner;

public record ScannerItemDto(
    string Symbol,
    string Name,
    string Sector,
    decimal Price,
    decimal DailyChangePercent,
    int Score,
    string Signal,
    int TrendScore,
    int MomentumScore,
    int VolumeScore,
    int StructureScore,
    decimal? RSI,
    decimal? MACD,
    decimal? ADX,
    decimal? VolumeRatio,
    string Trend,
    decimal Volume,
    DateTime SignalTime
);

public record ScannerFilterDto(
    Timeframe Timeframe = Timeframe.Daily,
    string? Signal = null,
    int? MinScore = null,
    string? Sector = null,
    decimal? MinRsi = null,
    decimal? MaxRsi = null,
    decimal? MinVolumeRatio = null,
    string? SortBy = "score", // score, volume, priceChange, signalTime
    string? SortDirection = "desc",
    int Page = 1,
    int PageSize = 20
);

public record MarketOverviewDto(
    int TotalSymbols,
    int StrongBuyCount,
    int BuyCount,
    int CandidateCount,
    int WatchCount,
    int SellCount,
    decimal AverageMarketScore,
    List<ScannerItemDto> TopSignals,
    List<ScannerItemDto> VolumeSurges,
    List<ScannerItemDto> Breakouts
);
