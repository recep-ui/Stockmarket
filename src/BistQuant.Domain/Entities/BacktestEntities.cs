using BistQuant.Domain.Common;
using BistQuant.Domain.Enums;

namespace BistQuant.Domain.Entities;

public class BacktestRun : BaseEntity<long>
{
    public int StrategyId { get; set; }
    public Strategy Strategy { get; set; } = null!;

    public string SymbolList { get; set; } = string.Empty; // Comma separated symbols or "ALL"
    public Timeframe Timeframe { get; set; } = Timeframe.Daily;

    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }

    public decimal InitialCapital { get; set; } = 100000m;
    public decimal CommissionRate { get; set; } = 0.0015m; // 0.15% standard BIST commission
    public decimal SlippageRate { get; set; } = 0.0010m;   // 0.10% slippage

    public BacktestStatus Status { get; set; } = BacktestStatus.Pending;
    public string? ErrorMessage { get; set; }

    public BacktestResult? Result { get; set; }
    public ICollection<BacktestTrade> Trades { get; set; } = new List<BacktestTrade>();
}

public class BacktestTrade : BaseEntity<long>
{
    public long BacktestRunId { get; set; }
    public BacktestRun BacktestRun { get; set; } = null!;

    public int SymbolId { get; set; }
    public Symbol Symbol { get; set; } = null!;

    public DateTime EntryDate { get; set; }
    public decimal EntryPrice { get; set; }

    public DateTime ExitDate { get; set; }
    public decimal ExitPrice { get; set; }

    public decimal Quantity { get; set; }

    public decimal GrossPnL { get; set; }
    public decimal NetPnL { get; set; }
    public decimal ReturnPercent { get; set; }

    public string ExitReason { get; set; } = string.Empty; // "TakeProfit1", "TakeProfit2", "StopLoss", "RuleExit", "EndOfData"
}

public class BacktestResult : BaseEntity<long>
{
    public long BacktestRunId { get; set; }
    public BacktestRun BacktestRun { get; set; } = null!;

    public int TotalTrades { get; set; }
    public int WinningTrades { get; set; }
    public int LosingTrades { get; set; }

    public decimal WinRate { get; set; } // %
    public decimal TotalReturn { get; set; } // %
    public decimal AnnualizedReturn { get; set; } // %

    public decimal AverageWin { get; set; }
    public decimal AverageLoss { get; set; }
    public decimal ProfitFactor { get; set; }

    public decimal MaxDrawdown { get; set; }
    public decimal? SharpeRatio { get; set; }
    public decimal? SortinoRatio { get; set; }
    public decimal Expectancy { get; set; }

    public string? EquityCurveJson { get; set; } // Serialized series of { Date, Equity, Drawdown }
}
