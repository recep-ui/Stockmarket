using BistQuant.Domain.Enums;

namespace BistQuant.Application.DTOs.Strategies;

public record StrategyRuleDto(
    int Id,
    string Indicator,
    RuleOperator Operator,
    decimal? Value,
    decimal? SecondaryValue,
    string? ComparisonIndicator,
    int Weight,
    string RuleGroup,
    bool IsRequired
);

public record StrategyDto(
    int Id,
    string Name,
    string Description,
    string StrategyType,
    Timeframe Timeframe,
    bool IsActive,
    List<StrategyRuleDto> Rules,
    long? UserId = null,
    bool IsSystem = false
);

public record CreateStrategyRequest(
    string Name,
    string Description,
    string StrategyType,
    Timeframe Timeframe,
    List<StrategyRuleDto> Rules
);
