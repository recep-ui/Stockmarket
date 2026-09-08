using BistQuant.Domain.Entities;
using BistQuant.Domain.Enums;

namespace BistQuant.Application.Services;

public record PipelineEvaluationResult(
    bool IsSignalTriggered,
    SignalType SignalType,
    int TotalScore,
    int TrendScore,
    int MomentumScore,
    int VolumeScore,
    int StructureScore,
    List<string> MatchedRules
);

public interface IStrategyEvaluationPipeline
{
    PipelineEvaluationResult Evaluate(
        PriceBar currentBar,
        IndicatorSnapshot snapshot,
        IReadOnlyList<PriceBar> history,
        Strategy? strategy = null,
        PriceBar? prevBar = null,
        IndicatorSnapshot? prevSnapshot = null);
}

public class StrategyEvaluationPipeline : IStrategyEvaluationPipeline
{
    private readonly IScoringEngine _scoringEngine;
    private readonly ISignalClassifier _signalClassifier;
    private readonly IStrategyEngine _strategyEngine;

    public StrategyEvaluationPipeline(
        IScoringEngine scoringEngine,
        ISignalClassifier signalClassifier,
        IStrategyEngine strategyEngine)
    {
        _scoringEngine = scoringEngine;
        _signalClassifier = signalClassifier;
        _strategyEngine = strategyEngine;
    }

    public PipelineEvaluationResult Evaluate(
        PriceBar currentBar,
        IndicatorSnapshot snapshot,
        IReadOnlyList<PriceBar> history,
        Strategy? strategy = null,
        PriceBar? prevBar = null,
        IndicatorSnapshot? prevSnapshot = null)
    {
        var scoringResult = _scoringEngine.Evaluate(currentBar, snapshot, history);
        var signalType = _signalClassifier.ClassifySignal(scoringResult.Scores.TotalScore, snapshot, history);

        bool isTriggered;
        List<string> matchedRules;

        if (strategy != null && strategy.Rules != null && strategy.Rules.Count > 0)
        {
            var stratEval = _strategyEngine.EvaluateRules(strategy.Rules.ToList(), snapshot, currentBar, prevSnapshot, prevBar);
            isTriggered = stratEval.IsSignalTriggered;
            matchedRules = stratEval.MatchedRules;

            if (isTriggered && signalType is not (SignalType.Buy or SignalType.StrongBuy))
            {
                signalType = stratEval.MatchedScore >= 50 ? SignalType.StrongBuy : SignalType.Buy;
            }
        }
        else
        {
            // Default: Buy or StrongBuy
            isTriggered = signalType == SignalType.StrongBuy || signalType == SignalType.Buy;
            matchedRules = scoringResult.Reasons.Select(r => r.Code).ToList();
        }

        return new PipelineEvaluationResult(
            isTriggered,
            signalType,
            scoringResult.Scores.TotalScore,
            scoringResult.Scores.TrendScore,
            scoringResult.Scores.MomentumScore,
            scoringResult.Scores.VolumeScore,
            scoringResult.Scores.StructureScore,
            matchedRules
        );
    }
}
