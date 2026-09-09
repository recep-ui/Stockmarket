using BistQuant.Domain.Common;

namespace BistQuant.Domain.Entities;

public class ForwardTestDailyReport : BaseEntity<long>
{
    public long PortfolioId { get; set; }
    public PaperPortfolio Portfolio { get; set; } = null!;

    public DateOnly SessionDate { get; set; }
    public int BulletinRevision { get; set; } = 1;
    public int SymbolsAnalyzed { get; set; }
    public int SignalsCreated { get; set; }
    public int BuySignals { get; set; }
    public int SellSignals { get; set; }
    public int OrdersQueued { get; set; }
    public int OrdersFilled { get; set; }
    public int OrdersExpired { get; set; }
    public decimal RealizedPnL { get; set; }
    public decimal UnrealizedPnL { get; set; }
    public decimal PortfolioEquity { get; set; }
    public decimal DrawdownPercent { get; set; }
    public string? Errors { get; set; }
}
