using BistQuant.Domain.Common;
using BistQuant.Domain.Enums;

namespace BistQuant.Domain.Entities;

public class Signal : BaseEntity<long>
{
    public int SymbolId { get; set; }
    public Symbol Symbol { get; set; } = null!;

    public int? StrategyId { get; set; }
    public Strategy? Strategy { get; set; }

    public Timeframe Timeframe { get; set; }
    public SignalType SignalType { get; set; }

    // Multi-factor scores
    public int Score { get; set; }
    public int TrendScore { get; set; }
    public int MomentumScore { get; set; }
    public int VolumeScore { get; set; }
    public int StructureScore { get; set; }

    // Execution & Risk Parameters
    public decimal Price { get; set; }
    public decimal StopLoss { get; set; }
    public decimal TakeProfit1 { get; set; }
    public decimal TakeProfit2 { get; set; }
    public decimal RiskRewardRatio { get; set; }
    public decimal Confidence { get; set; } = 1.0m;

    public DateTime? ExpiresAt { get; set; }
    public DateOnly? SourceSessionDate { get; set; }

    public ICollection<SignalReason> Reasons { get; set; } = new List<SignalReason>();
}

public class SignalReason : BaseEntity<long>
{
    public long SignalId { get; set; }
    public Signal Signal { get; set; } = null!;

    public string Code { get; set; } = string.Empty; // e.g. "EMA_BULLISH", "MACD_CROSS", "VOLUME_SURGE"
    public string Title { get; set; } = string.Empty; // e.g. "EMA20 > EMA50"
    public string Description { get; set; } = string.Empty; // e.g. "Short-term momentum above medium-term trend"
    public int ScoreContribution { get; set; } // e.g. 10
    public string Indicator { get; set; } = string.Empty; // e.g. "EMA"
    public decimal? IndicatorValue { get; set; }
}
