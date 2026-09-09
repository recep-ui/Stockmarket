namespace BistQuant.Application.DTOs.ForwardTesting;

public record PerformanceMetricDto(
    decimal InitialCapital,
    decimal Equity,
    decimal Cash,
    decimal OpenPositionValue,
    decimal RealizedPnL,
    decimal UnrealizedPnL,
    decimal TotalReturnPercent,
    decimal WinRatePercent,
    decimal ProfitFactor,
    decimal Expectancy,
    decimal MaxDrawdownPercent,
    double AvgHoldingPeriodDays,
    int TotalTrades,
    int WinningTrades,
    int LosingTrades
);

public record ScoreBracketBreakdownDto(
    string ScoreBracket,
    int TradeCount,
    int WinningTrades,
    decimal WinRatePercent,
    decimal RealizedPnL,
    decimal TotalProfit
);

public record StrategyBreakdownDto(
    string StrategyName,
    int TradeCount,
    decimal WinRatePercent,
    decimal RealizedPnL
);

public record SymbolBreakdownDto(
    string Symbol,
    int TradeCount,
    decimal WinRatePercent,
    decimal RealizedPnL
);

public record MonthlyBreakdownDto(
    string Month,
    int TradeCount,
    decimal RealizedPnL,
    decimal ReturnPercent
);

public record CurvePointDto(
    DateOnly Date,
    decimal Equity,
    decimal DrawdownPercent
);

public record ForwardTestDailyReportDto(
    long Id,
    long PortfolioId,
    DateOnly SessionDate,
    int BulletinRevision,
    int SymbolsAnalyzed,
    int SignalsCreated,
    int BuySignals,
    int SellSignals,
    int OrdersQueued,
    int OrdersFilled,
    int OrdersExpired,
    decimal RealizedPnL,
    decimal UnrealizedPnL,
    decimal PortfolioEquity,
    decimal DrawdownPercent,
    string? Errors,
    DateTime CreatedAt
);

public record ForwardTestPerformanceDto(
    long PortfolioId,
    string PortfolioName,
    DateOnly StartDate,
    bool IsForwardTest,
    PerformanceMetricDto Metrics,
    List<ScoreBracketBreakdownDto> ScoreBrackets,
    List<StrategyBreakdownDto> Strategies,
    List<SymbolBreakdownDto> Symbols,
    List<MonthlyBreakdownDto> Monthly,
    List<CurvePointDto> EquityCurve,
    List<ForwardTestDailyReportDto> RecentDailyReports
);
