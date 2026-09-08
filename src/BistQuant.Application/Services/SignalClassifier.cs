using BistQuant.Domain.Entities;
using BistQuant.Domain.Enums;

namespace BistQuant.Application.Services;

public interface ISignalClassifier
{
    SignalType ClassifySignal(int score);
    SignalType ClassifySignal(int score, IndicatorSnapshot? snapshot, IReadOnlyList<PriceBar>? history);
}

public class SignalClassifier : ISignalClassifier
{
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

        // Bearish Sell Semantics: Lack of BUY evidence != SELL. Must have active bearish confirmation.
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
            // If called without snapshot, fall back to score thresholds
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
}
