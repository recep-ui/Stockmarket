using BistQuant.Domain.Enums;

namespace BistQuant.Application.DTOs.Backtests;

public record BacktestRunRequest(
    int? StrategyId,
    string Symbol, // e.g. "THYAO" or "ALL"
    Timeframe Timeframe = Timeframe.Daily,
    DateTime? StartDate = null,
    DateTime? EndDate = null,
    decimal InitialCapital = 100000m,
    decimal CommissionRate = 0.0015m,
    decimal SlippageRate = 0.0010m
);

public record BacktestTradeDto(
    long Id,
    string Symbol,
    DateTime EntryDate,
    decimal EntryPrice,
    DateTime ExitDate,
    decimal ExitPrice,
    decimal Quantity,
    decimal GrossPnL,
    decimal NetPnL,
    decimal ReturnPercent,
    string ExitReason
);

public record EquityPointDto(
    string Date,
    decimal Equity,
    decimal DrawdownPercent
);

public record BacktestResultDto(
    long RunId,
    int TotalTrades,
    int WinningTrades,
    int LosingTrades,
    decimal WinRate,
    decimal TotalReturn,
    decimal AnnualizedReturn,
    decimal AverageWin,
    decimal AverageLoss,
    decimal ProfitFactor,
    decimal MaxDrawdown,
    decimal? SharpeRatio,
    decimal? SortinoRatio,
    decimal Expectancy,
    List<EquityPointDto> EquityCurve
);

public record BacktestRunDto(
    long Id,
    int? StrategyId,
    string StrategyName,
    string Symbol,
    Timeframe Timeframe,
    DateTime StartDate,
    DateTime EndDate,
    decimal InitialCapital,
    BacktestStatus Status,
    BacktestResultDto? Result
);
