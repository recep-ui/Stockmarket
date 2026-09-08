using BistQuant.Application.Common.Interfaces;
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
        CancellationToken cancellationToken = default);

    Task<Signal?> GetLatestSignalAsync(
        string symbol,
        Timeframe timeframe,
        CancellationToken cancellationToken = default);
}

public class SignalEngine : ISignalEngine
{
    private readonly IApplicationDbContext _context;
    private readonly IScoringEngine _scoringEngine;
    private readonly ITechnicalAnalysisService _technicalService;
    private readonly ILogger<SignalEngine> _logger;

    public SignalEngine(
        IApplicationDbContext context,
        IScoringEngine scoringEngine,
        ITechnicalAnalysisService technicalService,
        ILogger<SignalEngine> logger)
    {
        _context = context;
        _scoringEngine = scoringEngine;
        _technicalService = technicalService;
        _logger = logger;
    }

    public SignalType ClassifySignal(int score)
    {
        return ClassifySignal(score, null, null);
    }

    public SignalType ClassifySignal(int score, IndicatorSnapshot? snapshot, IReadOnlyList<PriceBar>? history)
    {
        if (score >= 85) return SignalType.StrongBuy;
        if (score >= 75) return SignalType.Buy;
        if (score >= 65) return SignalType.BuyCandidate;
        if (score >= 50) return SignalType.Watch;

        // Section 8: Bearish Sell Semantics
        // Lack of BUY evidence != SELL. Must have active bearish confirmation.
        int bearishEvidence = 0;
        if (snapshot != null)
        {
            if (snapshot.EMA20.HasValue && snapshot.EMA50.HasValue && snapshot.EMA20.Value < snapshot.EMA50.Value)
                bearishEvidence++;

            if (snapshot.MACD.HasValue && snapshot.MACDSignal.HasValue && snapshot.MACD.Value < snapshot.MACDSignal.Value)
                bearishEvidence++;

            if (snapshot.MACDHistogram.HasValue && snapshot.MACDHistogram.Value < 0)
                bearishEvidence++;

            if (snapshot.RSI14.HasValue && snapshot.RSI14.Value < 45)
                bearishEvidence++;

            if (snapshot.SuperTrendDirection.HasValue && snapshot.SuperTrendDirection.Value == 0)
                bearishEvidence++;

            if (history != null && history.Count >= 2)
            {
                if (history[^1].High < history[^2].High && history[^1].Low < history[^2].Low)
                    bearishEvidence++;

                if (snapshot.Support1.HasValue && history[^1].Close < snapshot.Support1.Value)
                    bearishEvidence++;
            }
        }
        else
        {
            // If called without snapshot (e.g. legacy test), fall back to score thresholds
            return score switch
            {
                >= 35 => SignalType.Weak,
                >= 20 => SignalType.Sell,
                _ => SignalType.StrongSell
            };
        }

        if (bearishEvidence >= 4 && score <= 25)
            return SignalType.StrongSell;

        if (bearishEvidence >= 2 && score <= 35)
            return SignalType.Sell;

        return SignalType.Weak;
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

        // Section 9 / HGH-01 Fix: StopLoss must be strictly positive and bounded
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
        CancellationToken cancellationToken = default)
    {
        var latestBar = await _context.PriceBars
            .AsNoTracking()
            .Where(p => p.SymbolId == symbolId && p.Timeframe == timeframe)
            .OrderByDescending(p => p.Timestamp)
            .FirstOrDefaultAsync(cancellationToken);

        if (latestBar == null) return null;

        // Section 9: Stale market data guard
        var maxAllowedStaleness = timeframe switch
        {
            Timeframe.M15 => TimeSpan.FromMinutes(45),
            Timeframe.H1 => TimeSpan.FromHours(4),
            _ => TimeSpan.FromDays(4) // Accounts for weekends / market holidays in daily bars
        };

        if (DateTime.UtcNow - latestBar.Timestamp > maxAllowedStaleness)
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

        // Section 9: Insufficient candle history guard
        if (history.Count < 20)
        {
            _logger.LogWarning("Insufficient candle history ({Count} bars) for symbol {SymbolId}. Aborting signal generation.", history.Count, symbolId);
            return null;
        }

        var snapshot = await _technicalService.CalculateAndSaveSnapshotAsync(symbolId, timeframe, cancellationToken);
        if (snapshot == null) return null;

        var scoringResult = _scoringEngine.Evaluate(latestBar, snapshot, history);
        var signalType = ClassifySignal(scoringResult.Scores.TotalScore, snapshot, history);
        var risk = CalculateRiskParameters(latestBar.Close, snapshot);

        var expiresAt = timeframe switch
        {
            Timeframe.M15 => DateTime.UtcNow.AddHours(2),
            Timeframe.H1 => DateTime.UtcNow.AddHours(8),
            _ => DateTime.UtcNow.AddDays(1)
        };

        var signal = new Signal
        {
            SymbolId = symbolId,
            Timeframe = timeframe,
            SignalType = signalType,
            Score = scoringResult.Scores.TotalScore,
            TrendScore = scoringResult.Scores.TrendScore,
            MomentumScore = scoringResult.Scores.MomentumScore,
            VolumeScore = scoringResult.Scores.VolumeScore,
            StructureScore = scoringResult.Scores.StructureScore,
            Price = latestBar.Close,
            StopLoss = risk.StopLoss,
            TakeProfit1 = risk.TakeProfit1,
            TakeProfit2 = risk.TakeProfit2,
            RiskRewardRatio = risk.RiskRewardRatio,
            Confidence = Math.Round(scoringResult.Scores.TotalScore / 100.0m, 2),
            ExpiresAt = expiresAt
        };

        foreach (var reason in scoringResult.Reasons)
        {
            signal.Reasons.Add(reason);
        }

        _context.Signals.Add(signal);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Generated {SignalType} signal for Symbol {SymbolId} with Score {Score}.", signalType, symbolId, signal.Score);

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
            signal = await GenerateAndSaveSignalAsync(sym.Id, timeframe, cancellationToken);
        }

        return signal;
    }
}
