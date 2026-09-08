using BistQuant.Domain.Common;
using BistQuant.Domain.Enums;

namespace BistQuant.Domain.Entities;

public class Strategy : BaseEntity<int>
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string StrategyType { get; set; } = "Technical"; // TrendFollowing, Momentum, Breakout, Reversal, Swing
    public Timeframe Timeframe { get; set; } = Timeframe.Daily;
    public bool IsActive { get; set; } = true;

    public ICollection<StrategyRule> Rules { get; set; } = new List<StrategyRule>();
    public ICollection<Signal> Signals { get; set; } = new List<Signal>();
    public ICollection<BacktestRun> BacktestRuns { get; set; } = new List<BacktestRun>();
}

public class StrategyRule : BaseEntity<int>
{
    public int StrategyId { get; set; }
    public Strategy Strategy { get; set; } = null!;

    public string Indicator { get; set; } = string.Empty; // e.g. "EMA20", "RSI"
    public RuleOperator Operator { get; set; }            // GreaterThan, LessThan, CrossAbove, etc.
    public decimal? Value { get; set; }                   // e.g. 50.0m
    public decimal? SecondaryValue { get; set; }          // e.g. 65.0m for Between operator
    public string? ComparisonIndicator { get; set; }      // e.g. "EMA50"

    public int Weight { get; set; } = 10;
    public string RuleGroup { get; set; } = "Default";    // Logical grouping
    public bool IsRequired { get; set; } = false;
}
