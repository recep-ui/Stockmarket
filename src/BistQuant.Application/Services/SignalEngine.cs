using BistQuant.Application.Common.Interfaces;
using BistQuant.Application.Services.MarketData;
using BistQuant.Domain.Entities;
using BistQuant.Domain.Enums;
using BistQuant.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BistQuant.Application.Services;

public interface ISignalEngine
{
    SignalType ClassifySignal(int score);

    SignalType ClassifySignal(int score, IndicatorSnapshot? snapshot, IReadOnlyList<PriceBar>? history);

    RiskParameters CalculateRiskParameters(decimal currentPrice, IndicatorSnapshot snapshot);

    Task<Signal?> GenerateAndSaveSignalAsync(
        int symbolId,
        Timeframe timeframe,
        Strategy? strategy = null,
        CancellationToken cancellationToken = default);

    Task<Signal?> GetLatestSignalAsync(
        string symbol,
        Timeframe timeframe,
        CancellationToken cancellationToken = default);
}

public class SignalEngine : ISignalEngine
{
    private readonly IApplicationDbContext _context;
    private readonly IStrategyEvaluationPipeline _pipeline;
    private readonly ISignalClassifier _signalClassifier;
    private readonly ITechnicalAnalysisService _technicalService;
    private readonly IMarketDataFreshnessPolicy _freshnessPolicy;
    private readonly IMarketSessionDateResolver _sessionDateResolver;
    private readonly ILogger<SignalEngine> _logger;

    [Microsoft.Extensions.DependencyInjection.ActivatorUtilitiesConstructor]
    public SignalEngine(
        IApplicationDbContext context,
        IStrategyEvaluationPipeline pipeline,
        ISignalClassifier signalClassifier,
        ITechnicalAnalysisService technicalService,
        IMarketDataFreshnessPolicy freshnessPolicy,
        IMarketSessionDateResolver sessionDateResolver,
        ILogger<SignalEngine> logger)
    {
        _context = context;
        _pipeline = pipeline;
        _signalClassifier = signalClassifier;
        _technicalService = technicalService;
        _freshnessPolicy = freshnessPolicy;
        _sessionDateResolver = sessionDateResolver;
        _logger = logger;
    }

    public SignalEngine(
        IApplicationDbContext context,
        IStrategyEvaluationPipeline pipeline,
        ISignalClassifier signalClassifier,
        ITechnicalAnalysisService technicalService,
        IMarketDataFreshnessPolicy freshnessPolicy,
        ILogger<SignalEngine> logger)
        : this(context, pipeline, signalClassifier, technicalService, freshnessPolicy, new MarketSessionDateResolver(), logger)
    {
    }

    public SignalEngine(
        IApplicationDbContext context,
        IScoringEngine scoringEngine,
        ITechnicalAnalysisService technicalService,
        ILogger<SignalEngine> logger)
        : this(context, null!, new SignalClassifier(), technicalService, new MarketDataFreshnessPolicy(), new MarketSessionDateResolver(), logger)
    {
    }

    public SignalType ClassifySignal(int score)
    {
        return _signalClassifier.ClassifySignal(score);
    }

    public SignalType ClassifySignal(int score, IndicatorSnapshot? snapshot, IReadOnlyList<PriceBar>? history)
    {
        return _signalClassifier.ClassifySignal(score, snapshot, history);
    }

    public RiskParameters CalculateRiskParameters(decimal currentPrice, IndicatorSnapshot snapshot)
    {
        var atr = (snapshot.ATR14.HasValue && snapshot.ATR14.Value > 0) ? snapshot.ATR14.Value : (currentPrice * 0.02m);
        var atrMultiplier = 2.0m;

        // Stop Loss: ATR-based stop or Support level
        decimal atrStop = Math.Round(currentPrice - (atr * atrMultiplier), 2);
        decimal stopLoss = atrStop;

        if (snapshot.Support1.HasValue && snapshot.Support1.Value < currentPrice && snapshot.Support1.Value > (currentPrice * 0.85m))
        {
            // Support-based stop just below support level
            decimal supportStop = Math.Round(snapshot.Support1.Value * 0.99m, 2);
            stopLoss = Math.Max(atrStop, supportStop);
        }

        // StopLoss must be strictly positive and bounded
        decimal minStopAllowed = Math.Round(currentPrice * 0.80m, 2);
        if (stopLoss <= 0 || stopLoss < minStopAllowed)
        {
            stopLoss = minStopAllowed;
        }

        // Safety fallback: StopLoss must be strictly less than current price
        if (stopLoss >= currentPrice)
        {
            stopLoss = Math.Round(currentPrice * 0.95m, 2);
        }

        decimal risk = currentPrice - stopLoss;
        if (risk <= 0) risk = Math.Round(currentPrice * 0.05m, 2);

        // Targets: R:R based or Resistance based
        decimal tp1 = Math.Round(currentPrice + (risk * 1.5m), 2);
        if (snapshot.Resistance1.HasValue && snapshot.Resistance1.Value > currentPrice)
        {
            tp1 = Math.Max(tp1, snapshot.Resistance1.Value);
        }

        decimal tp2 = Math.Round(currentPrice + (risk * 2.5m), 2);
        if (snapshot.Resistance2.HasValue && snapshot.Resistance2.Value > tp1)
        {
            tp2 = Math.Max(tp2, snapshot.Resistance2.Value);
        }
        if (tp2 <= tp1) tp2 = Math.Round(tp1 * 1.05m, 2);

        decimal rr = risk > 0 ? Math.Round((tp1 - currentPrice) / risk, 2) : 1.5m;

        return new RiskParameters(currentPrice, stopLoss, tp1, tp2, rr);
    }

    public async Task<Signal?> GenerateAndSaveSignalAsync(
        int symbolId,
        Timeframe timeframe,
        Strategy? strategy = null,
        CancellationToken cancellationToken = default)
    {
        var recentBars = await _context.PriceBars
            .AsNoTracking()
            .Where(p => p.SymbolId == symbolId && p.Timeframe == timeframe)
            .OrderByDescending(p => p.Timestamp)
            .Take(2)
            .ToListAsync(cancellationToken);

        if (recentBars.Count == 0) return null;

        var latestBar = recentBars[0];
        var prevBar = recentBars.Count > 1 ? recentBars[1] : null;

        // Freshness guard via central IMarketDataFreshnessPolicy
        if (!_freshnessPolicy.IsFresh(latestBar))
        {
            _logger.LogWarning("Market data for symbol {SymbolId} is stale (last: {Timestamp}). Aborting signal generation.", symbolId, latestBar.Timestamp);
            return null;
        }

        var history = await _context.PriceBars
            .AsNoTracking()
            .Where(p => p.SymbolId == symbolId && p.Timeframe == timeframe)
            .OrderByDescending(p => p.Timestamp)
            .Take(25)
            .OrderBy(p => p.Timestamp)
            .ToListAsync(cancellationToken);

        // Insufficient candle history guard
        if (history.Count < 20)
        {
            _logger.LogWarning("Insufficient candle history ({Count} bars) for symbol {SymbolId}. Aborting signal generation.", history.Count, symbolId);
            return null;
        }

        var snapshot = await _technicalService.CalculateAndSaveSnapshotAsync(symbolId, timeframe, cancellationToken);
        if (snapshot == null) return null;

        IndicatorSnapshot? prevSnapshot = null;
        if (prevBar != null)
        {
            prevSnapshot = await _context.IndicatorSnapshots
                .AsNoTracking()
                .Where(i => i.SymbolId == symbolId && i.Timeframe == timeframe && i.Timestamp < latestBar.Timestamp)
                .OrderByDescending(i => i.Timestamp)
                .FirstOrDefaultAsync(cancellationToken);
        }

        // Unified evaluation pipeline shared directly with BacktestEngine
        var evalResult = _pipeline.Evaluate(latestBar, snapshot, history, strategy, prevBar, prevSnapshot);

        // Required rule: If custom strategy is supplied and evalResult.IsSignalTriggered == false,
        // do not persist signal, do not trigger alerts, and do not emit scanner result.
        if (strategy != null && !evalResult.IsSignalTriggered)
        {
            _logger.LogDebug("Strategy '{StrategyName}' (ID {StrategyId}) did not trigger for symbol {SymbolId}.", strategy.Name, strategy.Id, symbolId);
            return null;
        }

        // Extract session date and check for existing signal to guarantee idempotency across scan runs
        var sessionDate = _sessionDateResolver.ResolveSessionDate(latestBar);
        var existingSignal = await _context.Signals
            .Include(s => s.Reasons)
            .FirstOrDefaultAsync(s =>
                s.SymbolId == symbolId &&
                s.StrategyId == (strategy != null ? strategy.Id : null) &&
                s.Timeframe == timeframe &&
                s.SourceSessionDate == sessionDate,
                cancellationToken);

        var risk = CalculateRiskParameters(latestBar.Close, snapshot);
        var expiresAt = CalculateExpiration(timeframe, DateTime.UtcNow);

        if (existingSignal != null)
        {
            if (existingSignal.Price == latestBar.Close &&
                existingSignal.Score == evalResult.TotalScore &&
                existingSignal.SignalType == evalResult.SignalType &&
                !existingSignal.IsSuperseded)
            {
                _logger.LogInformation(
                    "Signal for symbol {SymbolId}, strategy {StrategyId}, session {SessionDate} already exists and matches. Returning existing signal (0 duplicates).",
                    symbolId, strategy?.Id, sessionDate);
                return existingSignal;
            }

            _logger.LogInformation(
                "Bulletin revision or indicator recalculation detected for symbol {SymbolId}, session {SessionDate}. Updating existing signal deterministically.",
                symbolId, sessionDate);

            existingSignal.SignalType = evalResult.SignalType;
            existingSignal.Score = evalResult.TotalScore;
            existingSignal.TrendScore = evalResult.TrendScore;
            existingSignal.MomentumScore = evalResult.MomentumScore;
            existingSignal.VolumeScore = evalResult.VolumeScore;
            existingSignal.StructureScore = evalResult.StructureScore;
            existingSignal.Price = latestBar.Close;
            existingSignal.StopLoss = risk.StopLoss;
            existingSignal.TakeProfit1 = risk.TakeProfit1;
            existingSignal.TakeProfit2 = risk.TakeProfit2;
            existingSignal.RiskRewardRatio = risk.RiskRewardRatio;
            existingSignal.Confidence = Math.Round(evalResult.TotalScore / 100.0m, 2);
            existingSignal.ExpiresAt = expiresAt;
            existingSignal.UpdatedAt = DateTime.UtcNow;
            existingSignal.IsSuperseded = false;

            existingSignal.Reasons.Clear();
            foreach (var rule in evalResult.MatchedRules)
            {
                existingSignal.Reasons.Add(new SignalReason
                {
                    Code = rule,
                    Title = rule,
                    Description = $"Rule matched: {rule}",
                    Indicator = rule.Split(' ')[0]
                });
            }

            await _context.SaveChangesAsync(cancellationToken);
            return existingSignal;
        }

        var signal = new Signal
        {
            SymbolId = symbolId,
            StrategyId = strategy?.Id,
            Timeframe = timeframe,
            SourceSessionDate = sessionDate,
            SignalType = evalResult.SignalType,
            Score = evalResult.TotalScore,
            TrendScore = evalResult.TrendScore,
            MomentumScore = evalResult.MomentumScore,
            VolumeScore = evalResult.VolumeScore,
            StructureScore = evalResult.StructureScore,
            Price = latestBar.Close,
            StopLoss = risk.StopLoss,
            TakeProfit1 = risk.TakeProfit1,
            TakeProfit2 = risk.TakeProfit2,
            RiskRewardRatio = risk.RiskRewardRatio,
            Confidence = Math.Round(evalResult.TotalScore / 100.0m, 2),
            ExpiresAt = expiresAt
        };

        foreach (var rule in evalResult.MatchedRules)
        {
            signal.Reasons.Add(new SignalReason
            {
                Code = rule,
                Title = rule,
                Description = $"Rule matched: {rule}",
                Indicator = rule.Split(' ')[0]
            });
        }

        _context.Signals.Add(signal);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Generated {SignalType} signal for Symbol {SymbolId} (Session {SessionDate}) with Score {Score} via unified pipeline.", evalResult.SignalType, symbolId, sessionDate, signal.Score);

        return signal;
    }

    public async Task<Signal?> GetLatestSignalAsync(
        string symbol,
        Timeframe timeframe,
        CancellationToken cancellationToken = default)
    {
        var sym = await _context.Symbols
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Ticker == symbol.ToUpper(), cancellationToken);

        if (sym == null) return null;

        var signal = await _context.Signals
            .Include(s => s.Reasons)
            .Where(s => s.SymbolId == sym.Id && s.Timeframe == timeframe)
            .OrderByDescending(s => s.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (signal == null)
        {
            signal = await GenerateAndSaveSignalAsync(sym.Id, timeframe, null, cancellationToken);
        }

        return signal;
    }

    public static DateTime CalculateExpiration(Timeframe timeframe, DateTime asOfUtc)
    {
        return timeframe switch
        {
            Timeframe.M1 => asOfUtc.AddMinutes(15),
            Timeframe.M5 => asOfUtc.AddMinutes(45),
            Timeframe.M15 => asOfUtc.AddHours(2),
            Timeframe.M30 => asOfUtc.AddHours(4),
            Timeframe.H1 => asOfUtc.AddHours(8),
            Timeframe.H4 => asOfUtc.AddHours(24),
            Timeframe.Daily => asOfUtc.AddDays(2),
            Timeframe.Weekly => asOfUtc.AddDays(7),
            _ => asOfUtc.AddDays(1)
        };
    }
}
