using BistQuant.Application.Common.Interfaces;
using BistQuant.Application.DTOs.Strategies;
using BistQuant.Domain.Entities;
using BistQuant.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BistQuant.Application.Services;

public record StrategyEvaluationResult(
    bool IsSignalTriggered,
    int MatchedScore,
    List<string> MatchedRules
);

public interface IStrategyEngine
{
    StrategyEvaluationResult EvaluateRules(
        IReadOnlyList<StrategyRule> rules,
        IndicatorSnapshot snapshot,
        PriceBar currentBar,
        IndicatorSnapshot? prevSnapshot = null,
        PriceBar? prevBar = null);

    Task SeedPredefinedStrategiesAsync(CancellationToken cancellationToken = default);

    Task<List<StrategyDto>> GetAllStrategiesAsync(long? currentUserId = null, CancellationToken cancellationToken = default);

    Task<StrategyDto?> GetStrategyByIdAsync(int id, long? currentUserId = null, CancellationToken cancellationToken = default);

    Task<StrategyDto> CreateStrategyAsync(CreateStrategyRequest request, long? userId = null, CancellationToken cancellationToken = default);

    Task<bool> DeleteStrategyAsync(int id, long? userId = null, bool isAdmin = false, CancellationToken cancellationToken = default);
}

public class StrategyEngine : IStrategyEngine
{
    private readonly IApplicationDbContext _context;
    private readonly ILogger<StrategyEngine> _logger;

    public StrategyEngine(IApplicationDbContext context, ILogger<StrategyEngine> logger)
    {
        _context = context;
        _logger = logger;
    }

    public StrategyEvaluationResult EvaluateRules(
        IReadOnlyList<StrategyRule> rules,
        IndicatorSnapshot snapshot,
        PriceBar currentBar,
        IndicatorSnapshot? prevSnapshot = null,
        PriceBar? prevBar = null)
    {
        if (rules.Count == 0)
        {
            return new StrategyEvaluationResult(false, 0, new List<string>());
        }

        int score = 0;
        var matched = new List<string>();
        bool requiredFailed = false;

        foreach (var rule in rules)
        {
            decimal? leftValue = GetIndicatorValue(rule.Indicator, snapshot, currentBar);
            decimal? rightValue = rule.Value;

            if (!string.IsNullOrWhiteSpace(rule.ComparisonIndicator))
            {
                rightValue = GetIndicatorValue(rule.ComparisonIndicator, snapshot, currentBar);
            }

            if (!leftValue.HasValue)
            {
                if (rule.IsRequired) requiredFailed = true;
                continue;
            }

            bool isPassed;
            switch (rule.Operator)
            {
                case RuleOperator.GreaterThan:
                    isPassed = rightValue.HasValue && leftValue.Value > rightValue.Value;
                    break;
                case RuleOperator.GreaterThanOrEqual:
                    isPassed = rightValue.HasValue && leftValue.Value >= rightValue.Value;
                    break;
                case RuleOperator.LessThan:
                    isPassed = rightValue.HasValue && leftValue.Value < rightValue.Value;
                    break;
                case RuleOperator.LessThanOrEqual:
                    isPassed = rightValue.HasValue && leftValue.Value <= rightValue.Value;
                    break;
                case RuleOperator.Equal:
                    isPassed = rightValue.HasValue && leftValue.Value == rightValue.Value;
                    break;
                case RuleOperator.Between:
                    decimal lower = Math.Min(rule.Value ?? 0, rule.SecondaryValue ?? rule.Value ?? 0);
                    decimal upper = Math.Max(rule.Value ?? 0, rule.SecondaryValue ?? rule.Value ?? 0);
                    isPassed = leftValue.Value >= lower && leftValue.Value <= upper;
                    break;
                case RuleOperator.CrossAbove:
                    if (prevSnapshot != null || prevBar != null)
                    {
                        decimal? prevLeft = GetIndicatorValue(rule.Indicator, prevSnapshot ?? snapshot, prevBar ?? currentBar);
                        decimal? prevRight = !string.IsNullOrWhiteSpace(rule.ComparisonIndicator)
                            ? GetIndicatorValue(rule.ComparisonIndicator, prevSnapshot ?? snapshot, prevBar ?? currentBar)
                            : rule.Value;

                        isPassed = prevLeft.HasValue && prevRight.HasValue && rightValue.HasValue
                            && prevLeft.Value <= prevRight.Value && leftValue.Value > rightValue.Value;
                    }
                    else
                    {
                        isPassed = false; // No look-behind data available, crossover cannot be proven
                    }
                    break;
                case RuleOperator.CrossBelow:
                    if (prevSnapshot != null || prevBar != null)
                    {
                        decimal? prevLeft = GetIndicatorValue(rule.Indicator, prevSnapshot ?? snapshot, prevBar ?? currentBar);
                        decimal? prevRight = !string.IsNullOrWhiteSpace(rule.ComparisonIndicator)
                            ? GetIndicatorValue(rule.ComparisonIndicator, prevSnapshot ?? snapshot, prevBar ?? currentBar)
                            : rule.Value;

                        isPassed = prevLeft.HasValue && prevRight.HasValue && rightValue.HasValue
                            && prevLeft.Value >= prevRight.Value && leftValue.Value < rightValue.Value;
                    }
                    else
                    {
                        isPassed = false; // No look-behind data available, crossover cannot be proven
                    }
                    break;
                default:
                    throw new NotSupportedException($"RuleOperator '{rule.Operator}' is not supported.");
            }

            if (isPassed)
            {
                score += rule.Weight;
                matched.Add($"{rule.Indicator} {rule.Operator} {(rule.ComparisonIndicator ?? rule.Value?.ToString())}");
            }
            else if (rule.IsRequired)
            {
                requiredFailed = true;
            }
        }

        bool triggered = !requiredFailed && score >= 20;
        return new StrategyEvaluationResult(triggered, score, matched);
    }

    private static decimal? GetIndicatorValue(string indicator, IndicatorSnapshot snapshot, PriceBar currentBar)
    {
        return indicator.ToUpperInvariant() switch
        {
            "CLOSE" => currentBar.Close,
            "OPEN" => currentBar.Open,
            "HIGH" => currentBar.High,
            "LOW" => currentBar.Low,
            "VOLUME" => currentBar.Volume,
            "EMA20" => snapshot.EMA20,
            "EMA50" => snapshot.EMA50,
            "EMA100" => snapshot.EMA100,
            "EMA200" => snapshot.EMA200,
            "SMA20" => snapshot.SMA20,
            "SMA50" => snapshot.SMA50,
            "SMA200" => snapshot.SMA200,
            "RSI" or "RSI14" => snapshot.RSI14,
            "MACD" => snapshot.MACD,
            "MACDSIGNAL" => snapshot.MACDSignal,
            "MACDHISTOGRAM" => snapshot.MACDHistogram,
            "ATR" or "ATR14" => snapshot.ATR14,
            "ADX" or "ADX14" => snapshot.ADX14,
            "SUPERTREND" => snapshot.SuperTrend,
            "SUPERTRENDDIRECTION" => snapshot.SuperTrendDirection,
            "STOCHASTIC" or "STOCHASTICK" => snapshot.StochasticK,
            "STOCHASTICD" => snapshot.StochasticD,
            "VOLUMERATIO" => snapshot.VolumeRatio,
            "OBV" => snapshot.OBV,
            "RESISTANCE1" => snapshot.Resistance1,
            "SUPPORT1" => snapshot.Support1,
            "BREAKOUT" or "ISBREAKOUT" => snapshot.IsBreakout == true ? 1m : 0m,
            _ => null
        };
    }

    public async Task SeedPredefinedStrategiesAsync(CancellationToken cancellationToken = default)
    {
        if (await _context.Strategies.AnyAsync(s => s.IsSystem, cancellationToken)) return;

        var strategies = new List<Strategy>
        {
            new()
            {
                Name = "Trend Following (Golden Stack)",
                Description = "EMA20 > EMA50, EMA50 > EMA200, RSI 50-65 ve pozitif SuperTrend ile guclu trend takibi.",
                StrategyType = "TrendFollowing",
                Timeframe = Timeframe.Daily,
                IsActive = true,
                IsSystem = true,
                UserId = null,
                Rules = new List<StrategyRule>
                {
                    new() { Indicator = "EMA20", Operator = RuleOperator.GreaterThan, ComparisonIndicator = "EMA50", Weight = 15, IsRequired = true },
                    new() { Indicator = "EMA50", Operator = RuleOperator.GreaterThan, ComparisonIndicator = "EMA200", Weight = 15, IsRequired = false },
                    new() { Indicator = "RSI", Operator = RuleOperator.GreaterThan, Value = 50, Weight = 10, IsRequired = false },
                    new() { Indicator = "RSI", Operator = RuleOperator.LessThan, Value = 65, Weight = 10, IsRequired = false },
                    new() { Indicator = "VolumeRatio", Operator = RuleOperator.GreaterThan, Value = 1.2m, Weight = 10, IsRequired = false }
                }
            },
            new()
            {
                Name = "Momentum Surge",
                Description = "MACD Sinyal kesişimi ve ortalama üstü hacim ile momentum patlaması stratejisi.",
                StrategyType = "Momentum",
                Timeframe = Timeframe.Daily,
                IsActive = true,
                IsSystem = true,
                UserId = null,
                Rules = new List<StrategyRule>
                {
                    new() { Indicator = "MACD", Operator = RuleOperator.GreaterThan, ComparisonIndicator = "MACDSignal", Weight = 20, IsRequired = true },
                    new() { Indicator = "VolumeRatio", Operator = RuleOperator.GreaterThan, Value = 1.5m, Weight = 15, IsRequired = false },
                    new() { Indicator = "RSI", Operator = RuleOperator.GreaterThan, Value = 52, Weight = 10, IsRequired = false }
                }
            },
            new()
            {
                Name = "Breakout Hunter",
                Description = "20 günlük direnç seviyesi kırılımı ve yüksek hacim teyidi.",
                StrategyType = "Breakout",
                Timeframe = Timeframe.Daily,
                IsActive = true,
                IsSystem = true,
                UserId = null,
                Rules = new List<StrategyRule>
                {
                    new() { Indicator = "Close", Operator = RuleOperator.GreaterThan, ComparisonIndicator = "Resistance1", Weight = 25, IsRequired = true },
                    new() { Indicator = "VolumeRatio", Operator = RuleOperator.GreaterThan, Value = 1.3m, Weight = 15, IsRequired = false }
                }
            },
            new()
            {
                Name = "Oversold Reversal",
                Description = "Aşırı satım bölgesinden (RSI < 35) destek sıçraması arayan dönüş stratejisi.",
                StrategyType = "Reversal",
                Timeframe = Timeframe.Daily,
                IsActive = true,
                IsSystem = true,
                UserId = null,
                Rules = new List<StrategyRule>
                {
                    new() { Indicator = "RSI", Operator = RuleOperator.LessThan, Value = 38, Weight = 20, IsRequired = true },
                    new() { Indicator = "Close", Operator = RuleOperator.GreaterThan, ComparisonIndicator = "Support1", Weight = 15, IsRequired = false }
                }
            }
        };

        _context.Strategies.AddRange(strategies);
        await _context.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Seeded {Count} predefined quantitative system strategies.", strategies.Count);
    }

    public async Task<List<StrategyDto>> GetAllStrategiesAsync(long? currentUserId = null, CancellationToken cancellationToken = default)
    {
        await SeedPredefinedStrategiesAsync(cancellationToken);

        var query = _context.Strategies
            .AsNoTracking()
            .Include(s => s.Rules)
            .AsQueryable();

        if (currentUserId.HasValue)
        {
            query = query.Where(s => s.IsSystem || s.UserId == currentUserId.Value);
        }
        else
        {
            query = query.Where(s => s.IsSystem);
        }

        var strategies = await query.OrderBy(s => s.Id).ToListAsync(cancellationToken);

        return strategies.Select(s => new StrategyDto(
            s.Id,
            s.Name,
            s.Description,
            s.StrategyType,
            s.Timeframe,
            s.IsActive,
            s.Rules.Select(r => new StrategyRuleDto(r.Id, r.Indicator, r.Operator, r.Value, r.SecondaryValue, r.ComparisonIndicator, r.Weight, r.RuleGroup, r.IsRequired)).ToList(),
            s.UserId,
            s.IsSystem
        )).ToList();
    }

    public async Task<StrategyDto?> GetStrategyByIdAsync(int id, long? currentUserId = null, CancellationToken cancellationToken = default)
    {
        var s = await _context.Strategies
            .AsNoTracking()
            .Include(st => st.Rules)
            .FirstOrDefaultAsync(st => st.Id == id, cancellationToken);

        if (s == null) return null;

        // Custom user strategy: hide if requesting user is neither owner nor admin
        if (!s.IsSystem && currentUserId.HasValue && s.UserId.HasValue && s.UserId.Value != currentUserId.Value)
        {
            return null;
        }

        return new StrategyDto(
            s.Id,
            s.Name,
            s.Description,
            s.StrategyType,
            s.Timeframe,
            s.IsActive,
            s.Rules.Select(r => new StrategyRuleDto(r.Id, r.Indicator, r.Operator, r.Value, r.SecondaryValue, r.ComparisonIndicator, r.Weight, r.RuleGroup, r.IsRequired)).ToList(),
            s.UserId,
            s.IsSystem
        );
    }

    public async Task<StrategyDto> CreateStrategyAsync(CreateStrategyRequest request, long? userId = null, CancellationToken cancellationToken = default)
    {
        var strategy = new Strategy
        {
            UserId = userId,
            IsSystem = false,
            Name = request.Name,
            Description = request.Description,
            StrategyType = request.StrategyType,
            Timeframe = request.Timeframe,
            IsActive = true
        };

        foreach (var r in request.Rules)
        {
            strategy.Rules.Add(new StrategyRule
            {
                Indicator = r.Indicator,
                Operator = r.Operator,
                Value = r.Value,
                SecondaryValue = r.SecondaryValue,
                ComparisonIndicator = r.ComparisonIndicator,
                Weight = r.Weight,
                RuleGroup = r.RuleGroup,
                IsRequired = r.IsRequired
            });
        }

        _context.Strategies.Add(strategy);
        await _context.SaveChangesAsync(cancellationToken);

        return new StrategyDto(
            strategy.Id,
            strategy.Name,
            strategy.Description,
            strategy.StrategyType,
            strategy.Timeframe,
            strategy.IsActive,
            strategy.Rules.Select(r => new StrategyRuleDto(r.Id, r.Indicator, r.Operator, r.Value, r.SecondaryValue, r.ComparisonIndicator, r.Weight, r.RuleGroup, r.IsRequired)).ToList(),
            strategy.UserId,
            strategy.IsSystem
        );
    }

    public async Task<bool> DeleteStrategyAsync(int id, long? userId = null, bool isAdmin = false, CancellationToken cancellationToken = default)
    {
        var s = await _context.Strategies.FindAsync(new object[] { id }, cancellationToken);
        if (s == null) return false;

        if (s.IsSystem && !isAdmin)
        {
            throw new InvalidOperationException("System strategies are protected and cannot be deleted.");
        }

        if (s.UserId.HasValue && userId.HasValue && s.UserId.Value != userId.Value && !isAdmin)
        {
            throw new UnauthorizedAccessException("You do not have permission to delete this strategy.");
        }

        if (s.UserId.HasValue && !userId.HasValue && !isAdmin)
        {
            throw new UnauthorizedAccessException("Authentication required to delete custom strategy.");
        }

        _context.Strategies.Remove(s);
        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }
}
