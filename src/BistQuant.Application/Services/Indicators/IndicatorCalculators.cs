using BistQuant.Domain.Entities;

namespace BistQuant.Application.Services.Indicators;

public static class IndicatorCalculators
{
    // --- Exponential Moving Average ---
    public static List<decimal?> CalculateEma(IReadOnlyList<decimal> prices, int period)
    {
        var result = new List<decimal?>(prices.Count);
        if (prices.Count < period)
        {
            for (int i = 0; i < prices.Count; i++) result.Add(null);
            return result;
        }

        // Fill leading nulls
        for (int i = 0; i < period - 1; i++)
        {
            result.Add(null);
        }

        // Initial seed is SMA
        decimal sum = 0;
        for (int i = 0; i < period; i++)
        {
            sum += prices[i];
        }
        decimal currentEma = sum / period;
        result.Add(Math.Round(currentEma, 4));

        decimal multiplier = 2.0m / (period + 1.0m);
        for (int i = period; i < prices.Count; i++)
        {
            currentEma = (prices[i] - currentEma) * multiplier + currentEma;
            result.Add(Math.Round(currentEma, 4));
        }

        return result;
    }

    // --- Simple Moving Average ---
    public static List<decimal?> CalculateSma(IReadOnlyList<decimal> prices, int period)
    {
        var result = new List<decimal?>(prices.Count);
        for (int i = 0; i < prices.Count; i++)
        {
            if (i < period - 1)
            {
                result.Add(null);
                continue;
            }

            decimal sum = 0;
            for (int j = i - period + 1; j <= i; j++)
            {
                sum += prices[j];
            }
            result.Add(Math.Round(sum / period, 4));
        }
        return result;
    }

    // --- Relative Strength Index (Wilder's Smoothing) ---
    public static List<decimal?> CalculateRsi(IReadOnlyList<decimal> prices, int period = 14)
    {
        var result = new List<decimal?>(prices.Count);
        if (prices.Count <= period)
        {
            for (int i = 0; i < prices.Count; i++) result.Add(null);
            return result;
        }

        for (int i = 0; i < period; i++)
        {
            result.Add(null);
        }

        decimal gainSum = 0;
        decimal lossSum = 0;

        for (int i = 1; i <= period; i++)
        {
            var diff = prices[i] - prices[i - 1];
            if (diff > 0) gainSum += diff;
            else lossSum += Math.Abs(diff);
        }

        decimal avgGain = gainSum / period;
        decimal avgLoss = lossSum / period;

        decimal rsi = avgLoss == 0 ? 100m : 100m - (100m / (1m + (avgGain / avgLoss)));
        result.Add(Math.Round(rsi, 2));

        for (int i = period + 1; i < prices.Count; i++)
        {
            var diff = prices[i] - prices[i - 1];
            var gain = diff > 0 ? diff : 0m;
            var loss = diff < 0 ? Math.Abs(diff) : 0m;

            avgGain = ((avgGain * (period - 1)) + gain) / period;
            avgLoss = ((avgLoss * (period - 1)) + loss) / period;

            rsi = avgLoss == 0 ? 100m : 100m - (100m / (1m + (avgGain / avgLoss)));
            result.Add(Math.Round(rsi, 2));
        }

        return result;
    }

    // --- Average True Range (ATR) ---
    public static List<decimal?> CalculateAtr(IReadOnlyList<PriceBar> bars, int period = 14)
    {
        var result = new List<decimal?>(bars.Count);
        if (bars.Count < period)
        {
            for (int i = 0; i < bars.Count; i++) result.Add(null);
            return result;
        }

        var trueRanges = new List<decimal>(bars.Count) { bars[0].High - bars[0].Low };
        for (int i = 1; i < bars.Count; i++)
        {
            var hl = bars[i].High - bars[i].Low;
            var hcp = Math.Abs(bars[i].High - bars[i - 1].Close);
            var lcp = Math.Abs(bars[i].Low - bars[i - 1].Close);
            trueRanges.Add(Math.Max(hl, Math.Max(hcp, lcp)));
        }

        for (int i = 0; i < period - 1; i++)
        {
            result.Add(null);
        }

        decimal atr = 0;
        for (int i = 0; i < period; i++)
        {
            atr += trueRanges[i];
        }
        atr /= period;
        result.Add(Math.Round(atr, 4));

        for (int i = period; i < bars.Count; i++)
        {
            atr = ((atr * (period - 1)) + trueRanges[i]) / period;
            result.Add(Math.Round(atr, 4));
        }

        return result;
    }

    // --- MACD (12, 26, 9) ---
    public record MacdPoint(decimal? Macd, decimal? Signal, decimal? Histogram);

    public static List<MacdPoint> CalculateMacd(IReadOnlyList<decimal> prices, int fastPeriod = 12, int slowPeriod = 26, int signalPeriod = 9)
    {
        var fastEma = CalculateEma(prices, fastPeriod);
        var slowEma = CalculateEma(prices, slowPeriod);

        var macdLine = new List<decimal?>(prices.Count);
        for (int i = 0; i < prices.Count; i++)
        {
            if (fastEma[i].HasValue && slowEma[i].HasValue)
            {
                macdLine.Add(fastEma[i]!.Value - slowEma[i]!.Value);
            }
            else
            {
                macdLine.Add(null);
            }
        }

        // Calculate Signal Line (EMA 9 of MACD line)
        var validMacdValues = macdLine.Where(m => m.HasValue).Select(m => m!.Value).ToList();
        var signalEmaValues = CalculateEma(validMacdValues, signalPeriod);

        var points = new List<MacdPoint>(prices.Count);
        int validIdx = 0;

        for (int i = 0; i < prices.Count; i++)
        {
            if (!macdLine[i].HasValue)
            {
                points.Add(new MacdPoint(null, null, null));
            }
            else
            {
                var signal = validIdx < signalEmaValues.Count ? signalEmaValues[validIdx] : null;
                var macd = macdLine[i]!.Value;
                decimal? hist = signal.HasValue ? Math.Round(macd - signal.Value, 4) : null;
                points.Add(new MacdPoint(Math.Round(macd, 4), signal, hist));
                validIdx++;
            }
        }

        return points;
    }

    // --- Bollinger Bands (20, 2.0) ---
    public record BollingerPoint(decimal? Upper, decimal? Middle, decimal? Lower);

    public static List<BollingerPoint> CalculateBollingerBands(IReadOnlyList<decimal> prices, int period = 20, decimal multiplier = 2.0m)
    {
        var sma = CalculateSma(prices, period);
        var result = new List<BollingerPoint>(prices.Count);

        for (int i = 0; i < prices.Count; i++)
        {
            if (i < period - 1 || !sma[i].HasValue)
            {
                result.Add(new BollingerPoint(null, null, null));
                continue;
            }

            var middle = sma[i]!.Value;
            decimal sumSquaredDiffs = 0;
            for (int j = i - period + 1; j <= i; j++)
            {
                var diff = prices[j] - middle;
                sumSquaredDiffs += diff * diff;
            }
            var stdDev = (decimal)Math.Sqrt((double)(sumSquaredDiffs / period));
            var upper = Math.Round(middle + (multiplier * stdDev), 4);
            var lower = Math.Round(middle - (multiplier * stdDev), 4);

            result.Add(new BollingerPoint(upper, middle, lower));
        }

        return result;
    }

    // --- SuperTrend (10, 3.0) ---
    public record SuperTrendPoint(decimal? Value, byte? Direction); // Direction: 1 = Bullish, 0 = Bearish

    public static List<SuperTrendPoint> CalculateSuperTrend(IReadOnlyList<PriceBar> bars, int period = 10, decimal multiplier = 3.0m)
    {
        var atr = CalculateAtr(bars, period);
        var result = new List<SuperTrendPoint>(bars.Count);

        decimal prevFinalUpper = 0;
        decimal prevFinalLower = 0;
        byte prevDirection = 1;

        for (int i = 0; i < bars.Count; i++)
        {
            if (!atr[i].HasValue)
            {
                result.Add(new SuperTrendPoint(null, null));
                continue;
            }

            var hl2 = (bars[i].High + bars[i].Low) / 2.0m;
            var basicUpper = hl2 + (multiplier * atr[i]!.Value);
            var basicLower = hl2 - (multiplier * atr[i]!.Value);

            decimal finalUpper = (basicUpper < prevFinalUpper || (i > 0 && bars[i - 1].Close > prevFinalUpper)) ? basicUpper : prevFinalUpper;
            decimal finalLower = (basicLower > prevFinalLower || (i > 0 && bars[i - 1].Close < prevFinalLower)) ? basicLower : prevFinalLower;

            byte direction;
            if (prevDirection == 1 && bars[i].Close < finalLower)
            {
                direction = 0; // Bearish flip
            }
            else if (prevDirection == 0 && bars[i].Close > finalUpper)
            {
                direction = 1; // Bullish flip
            }
            else
            {
                direction = prevDirection;
            }

            var superTrend = direction == 1 ? finalLower : finalUpper;

            prevFinalUpper = finalUpper;
            prevFinalLower = finalLower;
            prevDirection = direction;

            result.Add(new SuperTrendPoint(Math.Round(superTrend, 4), direction));
        }

        return result;
    }

    // --- On-Balance Volume (OBV) ---
    public static List<decimal> CalculateObv(IReadOnlyList<PriceBar> bars)
    {
        var result = new List<decimal>(bars.Count);
        if (bars.Count == 0) return result;

        decimal currentObv = 0;
        result.Add(currentObv);

        for (int i = 1; i < bars.Count; i++)
        {
            if (bars[i].Close > bars[i - 1].Close)
            {
                currentObv += bars[i].Volume;
            }
            else if (bars[i].Close < bars[i - 1].Close)
            {
                currentObv -= bars[i].Volume;
            }
            result.Add(currentObv);
        }

        return result;
    }

    // --- Support & Resistance Levels (Local Extrema - Zero Lookahead) ---
    public record SupportResistanceResult(
        decimal? Support1,
        decimal? Support2,
        decimal? Resistance1,
        decimal? Resistance2,
        bool IsBreakout
    );

    public static SupportResistanceResult DetectSupportResistance(IReadOnlyList<PriceBar> bars, int lookback = 20)
    {
        if (bars.Count < lookback + 1)
        {
            return new SupportResistanceResult(null, null, null, null, false);
        }

        // Strictly evaluate prior bars (excluding current bar) to avoid lookahead bias
        var priorBars = bars.Take(bars.Count - 1).TakeLast(lookback).ToList();
        var currentClose = bars[^1].Close;

        // Find swing highs and swing lows on prior bars
        var swingHighs = new List<decimal>();
        var swingLows = new List<decimal>();

        for (int i = 2; i < priorBars.Count - 2; i++)
        {
            if (priorBars[i].High >= priorBars[i - 1].High &&
                priorBars[i].High >= priorBars[i - 2].High &&
                priorBars[i].High >= priorBars[i + 1].High &&
                priorBars[i].High >= priorBars[i + 2].High)
            {
                swingHighs.Add(priorBars[i].High);
            }

            if (priorBars[i].Low <= priorBars[i - 1].Low &&
                priorBars[i].Low <= priorBars[i - 2].Low &&
                priorBars[i].Low <= priorBars[i + 1].Low &&
                priorBars[i].Low <= priorBars[i + 2].Low)
            {
                swingLows.Add(priorBars[i].Low);
            }
        }

        decimal maxHigh = priorBars.Max(b => b.High);
        decimal minLow = priorBars.Min(b => b.Low);

        var resistancesAbove = swingHighs.Where(h => h > currentClose).OrderBy(h => h).ToList();
        var supportsBelow = swingLows.Where(l => l < currentClose).OrderByDescending(l => l).ToList();

        decimal res1 = resistancesAbove.Count > 0 ? resistancesAbove[0] : maxHigh;
        decimal res2 = resistancesAbove.Count > 1 ? resistancesAbove[1] : Math.Round(res1 * 1.05m, 2);

        decimal sup1 = supportsBelow.Count > 0 ? supportsBelow[0] : minLow;
        decimal sup2 = supportsBelow.Count > 1 ? supportsBelow[1] : Math.Round(sup1 * 0.95m, 2);

        // Breakout: Current Close > prior resistance level or prior 20-day high (excluding current bar)
        bool isBreakout = currentClose > res1 || currentClose > maxHigh;

        return new SupportResistanceResult(sup1, sup2, res1, res2, isBreakout);
    }

    /// <summary>
    /// Shared full indicator pipeline ensuring 100% parity between Live Scanner and Backtest Engine.
    /// Calculates EMA, SMA, RSI, MACD, Stochastic, ATR, ADX, SuperTrend, Bollinger Bands, OBV, Support/Resistance, and Breakout.
    /// </summary>
    public static List<IndicatorSnapshot> CalculateSnapshots(
        IReadOnlyList<PriceBar> bars,
        int symbolId = 0,
        BistQuant.Domain.Enums.Timeframe timeframe = BistQuant.Domain.Enums.Timeframe.Daily)
    {
        var result = new List<IndicatorSnapshot>(bars.Count);
        if (bars.Count == 0) return result;

        var closes = bars.Select(b => b.Close).ToList();

        var ema20 = CalculateEma(closes, 20);
        var ema50 = CalculateEma(closes, 50);
        var ema100 = CalculateEma(closes, 100);
        var ema200 = CalculateEma(closes, 200);

        var sma20 = CalculateSma(closes, 20);
        var sma50 = CalculateSma(closes, 50);
        var sma200 = CalculateSma(closes, 200);

        var rsi = CalculateRsi(closes, 14);
        var macd = CalculateMacd(closes);
        var stoch = CalculateStochastic(bars);
        var atr = CalculateAtr(bars, 14);
        var adx = CalculateAdx(bars, 14);
        var bb = CalculateBollingerBands(closes);
        var st = CalculateSuperTrend(bars);
        var obv = CalculateObv(bars);

        for (int i = 0; i < bars.Count; i++)
        {
            var bar = bars[i];
            var recentVolumes = bars.Take(i + 1).TakeLast(20).Select(b => b.Volume).ToList();
            var avgVol20 = recentVolumes.Count > 0 ? recentVolumes.Average() : 0m;
            var volRatio = avgVol20 > 0 ? Math.Round(bar.Volume / avgVol20, 2) : 1.0m;

            var sr = DetectSupportResistance(bars.Take(i + 1).ToList(), 20);

            result.Add(new IndicatorSnapshot
            {
                SymbolId = symbolId,
                Timeframe = timeframe,
                Timestamp = bar.Timestamp,

                EMA20 = ema20[i],
                EMA50 = ema50[i],
                EMA100 = ema100[i],
                EMA200 = ema200[i],

                SMA20 = sma20[i],
                SMA50 = sma50[i],
                SMA200 = sma200[i],

                RSI14 = rsi[i],
                MACD = macd[i].Macd,
                MACDSignal = macd[i].Signal,
                MACDHistogram = macd[i].Histogram,
                StochasticK = stoch[i].K,
                StochasticD = stoch[i].D,

                ATR14 = atr[i],
                ADX14 = adx[i],
                SuperTrend = st[i].Value,
                SuperTrendDirection = st[i].Direction,

                BollingerUpper = bb[i].Upper,
                BollingerMiddle = bb[i].Middle,
                BollingerLower = bb[i].Lower,

                AverageVolume20 = Math.Round(avgVol20, 2),
                VolumeRatio = volRatio,
                OBV = obv[i],

                Support1 = sr.Support1,
                Support2 = sr.Support2,
                Resistance1 = sr.Resistance1,
                Resistance2 = sr.Resistance2,
                IsBreakout = sr.IsBreakout
            });
        }

        return result;
    }

    // --- Average Directional Index (ADX 14) ---
    public static List<decimal?> CalculateAdx(IReadOnlyList<PriceBar> bars, int period = 14)
    {
        var result = new List<decimal?>(bars.Count);
        if (bars.Count < period * 2)
        {
            for (int i = 0; i < bars.Count; i++) result.Add(null);
            return result;
        }

        var trueRanges = new List<decimal>(bars.Count) { bars[0].High - bars[0].Low };
        var plusDms = new List<decimal>(bars.Count) { 0m };
        var minusDms = new List<decimal>(bars.Count) { 0m };

        for (int i = 1; i < bars.Count; i++)
        {
            var hl = bars[i].High - bars[i].Low;
            var hcp = Math.Abs(bars[i].High - bars[i - 1].Close);
            var lcp = Math.Abs(bars[i].Low - bars[i - 1].Close);
            trueRanges.Add(Math.Max(hl, Math.Max(hcp, lcp)));

            var upMove = bars[i].High - bars[i - 1].High;
            var downMove = bars[i - 1].Low - bars[i].Low;

            if (upMove > downMove && upMove > 0)
                plusDms.Add(upMove);
            else
                plusDms.Add(0m);

            if (downMove > upMove && downMove > 0)
                minusDms.Add(downMove);
            else
                minusDms.Add(0m);
        }

        // Initial sum for first `period` bars
        decimal trSum = 0, plusDmSum = 0, minusDmSum = 0;
        for (int i = 0; i < period; i++)
        {
            trSum += trueRanges[i];
            plusDmSum += plusDms[i];
            minusDmSum += minusDms[i];
        }

        var dxList = new List<decimal?>(bars.Count);
        for (int i = 0; i < period - 1; i++)
        {
            dxList.Add(null);
        }

        decimal trSmooth = trSum;
        decimal plusDmSmooth = plusDmSum;
        decimal minusDmSmooth = minusDmSum;

        decimal plusDi = trSmooth > 0 ? (plusDmSmooth / trSmooth) * 100m : 0m;
        decimal minusDi = trSmooth > 0 ? (minusDmSmooth / trSmooth) * 100m : 0m;
        decimal diSum = plusDi + minusDi;
        decimal dx = diSum > 0 ? Math.Abs(plusDi - minusDi) / diSum * 100m : 0m;
        dxList.Add(dx);

        for (int i = period; i < bars.Count; i++)
        {
            trSmooth = trSmooth - (trSmooth / period) + trueRanges[i];
            plusDmSmooth = plusDmSmooth - (plusDmSmooth / period) + plusDms[i];
            minusDmSmooth = minusDmSmooth - (minusDmSmooth / period) + minusDms[i];

            plusDi = trSmooth > 0 ? (plusDmSmooth / trSmooth) * 100m : 0m;
            minusDi = trSmooth > 0 ? (minusDmSmooth / trSmooth) * 100m : 0m;
            diSum = plusDi + minusDi;
            dx = diSum > 0 ? Math.Abs(plusDi - minusDi) / diSum * 100m : 0m;
            dxList.Add(dx);
        }

        // ADX is the smoothed average of DX over `period`
        for (int i = 0; i < period * 2 - 1; i++)
        {
            result.Add(null);
        }

        // Seed ADX is SMA of first `period` DX values
        var validDx = dxList.Where(d => d.HasValue).Select(d => d!.Value).ToList();
        decimal adxSum = validDx.Take(period).Sum();
        decimal adx = adxSum / period;
        result.Add(Math.Round(adx, 2));

        for (int i = period; i < validDx.Count; i++)
        {
            adx = ((adx * (period - 1)) + validDx[i]) / period;
            result.Add(Math.Round(adx, 2));
        }

        return result;
    }

    // --- Stochastic Oscillator (%K, %D) ---
    public record StochasticPoint(decimal? K, decimal? D);

    public static List<StochasticPoint> CalculateStochastic(IReadOnlyList<PriceBar> bars, int kPeriod = 14, int dPeriod = 3, int smoothK = 3)
    {
        var result = new List<StochasticPoint>(bars.Count);
        if (bars.Count < kPeriod)
        {
            for (int i = 0; i < bars.Count; i++) result.Add(new StochasticPoint(null, null));
            return result;
        }

        var rawK = new List<decimal?>(bars.Count);
        for (int i = 0; i < bars.Count; i++)
        {
            if (i < kPeriod - 1)
            {
                rawK.Add(null);
                continue;
            }

            decimal lowestLow = decimal.MaxValue;
            decimal highestHigh = decimal.MinValue;
            for (int j = i - kPeriod + 1; j <= i; j++)
            {
                if (bars[j].Low < lowestLow) lowestLow = bars[j].Low;
                if (bars[j].High > highestHigh) highestHigh = bars[j].High;
            }

            decimal range = highestHigh - lowestLow;
            decimal k = range > 0 ? ((bars[i].Close - lowestLow) / range) * 100m : 50m;
            rawK.Add(k);
        }

        // Slow %K is SMA of raw %K over smoothK
        var validRawK = rawK.Where(k => k.HasValue).Select(k => k!.Value).ToList();
        var smoothedKValues = CalculateSma(validRawK, smoothK);

        var smoothedKFull = new List<decimal?>(bars.Count);
        int valIdx = 0;
        for (int i = 0; i < bars.Count; i++)
        {
            if (!rawK[i].HasValue)
            {
                smoothedKFull.Add(null);
            }
            else
            {
                smoothedKFull.Add(valIdx < smoothedKValues.Count ? smoothedKValues[valIdx] : null);
                valIdx++;
            }
        }

        // %D is SMA of smoothed %K over dPeriod
        var validSlowK = smoothedKFull.Where(k => k.HasValue).Select(k => k!.Value).ToList();
        var dValues = CalculateSma(validSlowK, dPeriod);

        int dIdx = 0;
        for (int i = 0; i < bars.Count; i++)
        {
            if (!smoothedKFull[i].HasValue)
            {
                result.Add(new StochasticPoint(null, null));
            }
            else
            {
                var kVal = smoothedKFull[i];
                var dVal = dIdx < dValues.Count ? dValues[dIdx] : null;
                result.Add(new StochasticPoint(
                    kVal.HasValue ? Math.Round(kVal.Value, 2) : null,
                    dVal.HasValue ? Math.Round(dVal.Value, 2) : null));
                if (kVal.HasValue) dIdx++;
            }
        }

        return result;
    }
}
