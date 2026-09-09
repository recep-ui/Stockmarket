using BistQuant.Domain.Enums;

namespace BistQuant.Application.DTOs.MarketData;

public record SymbolCoverageDto(
    string Ticker,
    string Name,
    DateOnly? FirstBarDate,
    DateOnly? LastBarDate,
    int DailyBarCount,
    int ExpectedTradingSessions,
    int MissingSessions,
    decimal CoveragePercent,
    bool Ema200Ready,
    DateOnly? LatestBulletinDate,
    bool HasCorporateActionWarning
);

public record UniverseCoverageSummaryDto(
    int ActiveSymbols,
    int SymbolsWith300PlusBars,
    int SymbolsWith500PlusBars,
    decimal AverageCoveragePercent,
    int TotalMissingSessions,
    int SymbolsWithCorporateActionWarnings,
    List<SymbolCoverageDto> SymbolCoverages
);

public record MarketDataGapDto(
    DateOnly SessionDate,
    MarketDataGapReason Reason,
    string Description
);

public record SymbolGapsDto(
    string Ticker,
    int TotalGaps,
    List<MarketDataGapDto> Gaps
);
