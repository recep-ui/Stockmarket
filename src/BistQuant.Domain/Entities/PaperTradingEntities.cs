using BistQuant.Domain.Common;
using BistQuant.Domain.Enums;

namespace BistQuant.Domain.Entities;

public class PaperPortfolio : BaseEntity<long>
{
    public long UserId { get; set; }
    public User User { get; set; } = null!;

    public string Name { get; set; } = "Ana Portfoy";
    public decimal InitialBalance { get; set; } = 100000m;
    public decimal CashBalance { get; set; } = 100000m;

    public bool IsAutoTradingEnabled { get; set; } = false;
    public int AutoTradingMinScore { get; set; } = 80;
    public decimal AutoTradingMaxAllocationPercent { get; set; } = 5.0m;

    public bool IsForwardTest { get; set; } = false;
    public DateOnly? ForwardTestStartDate { get; set; }

    public ICollection<PaperPosition> Positions { get; set; } = new List<PaperPosition>();
    public ICollection<PaperOrder> Orders { get; set; } = new List<PaperOrder>();
    public ICollection<PaperTrade> Trades { get; set; } = new List<PaperTrade>();
}

public class PaperPosition : BaseEntity<long>
{
    public long PortfolioId { get; set; }
    public PaperPortfolio Portfolio { get; set; } = null!;

    public int SymbolId { get; set; }
    public Symbol Symbol { get; set; } = null!;

    public decimal Quantity { get; set; }
    public decimal AveragePrice { get; set; }
    public decimal CurrentPrice { get; set; }

    public decimal UnrealizedPnL => (CurrentPrice - AveragePrice) * Quantity;
    public decimal UnrealizedPnLPercent => AveragePrice > 0 ? ((CurrentPrice - AveragePrice) / AveragePrice) * 100m : 0m;
}

public class PaperOrder : BaseEntity<long>
{
    public long PortfolioId { get; set; }
    public PaperPortfolio Portfolio { get; set; } = null!;

    public int SymbolId { get; set; }
    public Symbol Symbol { get; set; } = null!;

    public string? ClientOrderId { get; set; }

    public OrderSide Side { get; set; }
    public OrderType Type { get; set; }
    public OrderStatus Status { get; set; } = OrderStatus.Pending;

    public decimal Quantity { get; set; }
    public decimal? LimitPrice { get; set; }
    public decimal? TargetPrice { get; set; }
    public decimal? StopLossPrice { get; set; }
    public decimal? FilledPrice { get; set; }
    public DateTime? FilledAt { get; set; }

    // T+1 Session Identity and Execution Audit Fields
    public long? SourceSignalId { get; set; }
    public DateOnly? SignalSessionDate { get; set; }
    public DateOnly? TargetExecutionSessionDate { get; set; }
    public DateOnly? ExecutedSessionDate { get; set; }
    public string? CancellationReason { get; set; }
    public bool SourceBulletinRevisedAfterExecution { get; set; }
}

public class PaperTrade : BaseEntity<long>
{
    public long PortfolioId { get; set; }
    public PaperPortfolio Portfolio { get; set; } = null!;

    public int SymbolId { get; set; }
    public Symbol Symbol { get; set; } = null!;

    public long? PaperOrderId { get; set; }
    public PaperOrder? PaperOrder { get; set; }

    public long? SourceSignalId { get; set; }
    public bool SourceBulletinRevisedAfterExecution { get; set; }

    public OrderSide Side { get; set; }
    public decimal Quantity { get; set; }
    public decimal Price { get; set; }
    public decimal RealizedPnL { get; set; }
    public decimal Commission { get; set; }

    public DateTime ExecutedAt { get; set; } = DateTime.UtcNow;
}

