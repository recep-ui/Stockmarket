using BistQuant.Application.Services.Indicators;
using BistQuant.Domain.Entities;
using BistQuant.Domain.Enums;
using BistQuant.Domain.Models;
using Microsoft.Extensions.Configuration;

namespace BistQuant.Application.Services;

public record ScoringEvaluationResult(
    TechnicalScores Scores,
    List<SignalReason> Reasons
);

public interface IScoringEngine
{
    ScoringEvaluationResult Evaluate(PriceBar currentBar, IndicatorSnapshot snapshot, IReadOnlyList<PriceBar>? history = null);
}

public class ScoringEngine : IScoringEngine
{
    private readonly IConfiguration _configuration;

    public ScoringEngine(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public ScoringEvaluationResult Evaluate(PriceBar currentBar, IndicatorSnapshot snapshot, IReadOnlyList<PriceBar>? history = null)
    {
        var reasons = new List<SignalReason>();

        int trendScore = 0;
        int momentumScore = 0;
        int volumeScore = 0;
        int structureScore = 0;

        var close = currentBar.Close;

        // 1. TREND SCORING (Max 40)
        if (snapshot.EMA20.HasValue && close > snapshot.EMA20.Value)
        {
            var pts = _configuration.GetValue("Scoring:Trend:CloseAboveEma20", 5);
            trendScore += pts;
            reasons.Add(new SignalReason
            {
                Code = "CLOSE_ABOVE_EMA20",
                Title = "Fiyat EMA20 Uzerinde",
                Description = $"Kapanis ({close:F2} TL) 20 gunluk EMA ({snapshot.EMA20.Value:F2} TL) uzerinde seyrediyor.",
                ScoreContribution = pts,
                Indicator = "EMA20",
                IndicatorValue = snapshot.EMA20.Value
            });
        }

        if (snapshot.EMA20.HasValue && snapshot.EMA50.HasValue && snapshot.EMA20.Value > snapshot.EMA50.Value)
        {
            var pts = _configuration.GetValue("Scoring:Trend:Ema20AboveEma50", 10);
            trendScore += pts;
            reasons.Add(new SignalReason
            {
                Code = "EMA20_ABOVE_EMA50",
                Title = "Kisa Vadeli Trend Pozitif",
                Description = $"EMA20 ({snapshot.EMA20.Value:F2} TL), EMA50 ({snapshot.EMA50.Value:F2} TL) uzerinde kisa vadeli yukselis trendini koruyor.",
                ScoreContribution = pts,
                Indicator = "EMA",
                IndicatorValue = snapshot.EMA20.Value
            });
        }

        if (snapshot.EMA50.HasValue && snapshot.EMA200.HasValue && snapshot.EMA50.Value > snapshot.EMA200.Value)
        {
            var pts = _configuration.GetValue("Scoring:Trend:Ema50AboveEma200", 10);
            trendScore += pts;
            reasons.Add(new SignalReason
            {
                Code = "GOLDEN_STACK",
                Title = "Orta/Uzun Vade Golden Stack",
                Description = $"EMA50 ({snapshot.EMA50.Value:F2} TL), EMA200 ({snapshot.EMA200.Value:F2} TL) uzerinde guclu ana trendi teyit ediyor.",
                ScoreContribution = pts,
                Indicator = "EMA200",
                IndicatorValue = snapshot.EMA200.Value
            });
        }

        if (snapshot.SuperTrendDirection == 1)
        {
            var pts = _configuration.GetValue("Scoring:Trend:SuperTrendBuy", 10);
            trendScore += pts;
            reasons.Add(new SignalReason
            {
                Code = "SUPERTREND_BUY",
                Title = "SuperTrend AL Pozisyonunda",
                Description = $"SuperTrend ({snapshot.SuperTrend:F2} TL) altinda destek bandi ile AL sinyalini surduruyor.",
                ScoreContribution = pts,
                Indicator = "SuperTrend",
                IndicatorValue = snapshot.SuperTrend
            });
        }

        var adxThreshold = _configuration.GetValue("Scoring:Trend:AdxThreshold", 20m);
        if (snapshot.ADX14.HasValue && snapshot.ADX14.Value >= adxThreshold)
        {
            var pts = _configuration.GetValue("Scoring:Trend:AdxScore", 5);
            trendScore += pts;
            reasons.Add(new SignalReason
            {
                Code = "ADX_TREND_STRONG",
                Title = "Guclu Trend Gucu (ADX >= 20)",
                Description = $"ADX ({snapshot.ADX14.Value:F1}) piyasada guclu bir yonlu trend oldugunu gosteriyor.",
                ScoreContribution = pts,
                Indicator = "ADX14",
                IndicatorValue = snapshot.ADX14.Value
            });
        }

        trendScore = Math.Min(40, trendScore);

        // 2. MOMENTUM SCORING (Max 30)
        var rsiMin = _configuration.GetValue("Scoring:Momentum:RsiMin", 50m);
        var rsiMax = _configuration.GetValue("Scoring:Momentum:RsiMax", 65m);
        if (snapshot.RSI14.HasValue)
        {
            var rsi = snapshot.RSI14.Value;
            if (rsi >= rsiMin && rsi <= rsiMax)
            {
                var pts = _configuration.GetValue("Scoring:Momentum:RsiScore", 10);
                momentumScore += pts;
                reasons.Add(new SignalReason
                {
                    Code = "RSI_HEALTHY_BULLISH",
                    Title = "RSI Saglikli Alim Bolgesinde",
                    Description = $"RSI ({rsi:F1}) {rsiMin}-{rsiMax} bandinda asiri alima girmeden saglikli yukselis momentumu sergiliyor.",
                    ScoreContribution = pts,
                    Indicator = "RSI14",
                    IndicatorValue = rsi
                });
            }
        }

        if (snapshot.MACD.HasValue && snapshot.MACDSignal.HasValue && snapshot.MACD.Value > snapshot.MACDSignal.Value)
        {
            var pts = _configuration.GetValue("Scoring:Momentum:MacdAboveSignal", 10);
            momentumScore += pts;
            reasons.Add(new SignalReason
            {
                Code = "MACD_BULLISH",
                Title = "MACD Sinyal Cizgisi Uzerinde",
                Description = $"MACD ({snapshot.MACD.Value:F2}) pozitif bolgede sinyal cizgisinin uzerinde hareket ediyor.",
                ScoreContribution = pts,
                Indicator = "MACD",
                IndicatorValue = snapshot.MACD.Value
            });
        }

        if (snapshot.MACDHistogram.HasValue && snapshot.MACDHistogram.Value > 0)
        {
            var pts = _configuration.GetValue("Scoring:Momentum:MacdBullishCross", 5);
            momentumScore += pts;
            reasons.Add(new SignalReason
            {
                Code = "MACD_HISTOGRAM_EXPANDING",
                Title = "Pozitif Histogram Genislemesi",
                Description = $"MACD Histogram ({snapshot.MACDHistogram.Value:F2}) yukari ivmeyi destekliyor.",
                ScoreContribution = pts,
                Indicator = "MACDHistogram",
                IndicatorValue = snapshot.MACDHistogram.Value
            });
        }

        if (snapshot.StochasticK.HasValue && snapshot.StochasticD.HasValue && snapshot.StochasticK.Value > snapshot.StochasticD.Value && snapshot.StochasticK.Value < 80)
        {
            var pts = _configuration.GetValue("Scoring:Momentum:StochasticBullish", 5);
            momentumScore += pts;
            reasons.Add(new SignalReason
            {
                Code = "STOCHASTIC_BULLISH",
                Title = "Stokastik Alim Sinyali",
                Description = $"Stokastik %K ({snapshot.StochasticK.Value:F1}), %D ({snapshot.StochasticD.Value:F1}) uzerinde pozitif ivmeyi destekliyor.",
                ScoreContribution = pts,
                Indicator = "Stochastic",
                IndicatorValue = snapshot.StochasticK.Value
            });
        }

        momentumScore = Math.Min(30, momentumScore);

        // 3. VOLUME SCORING (Max 15)
        var volThresh2 = (decimal)_configuration.GetValue("Scoring:Volume:VolumeRatioThreshold2", 1.5);
        var volThresh1 = (decimal)_configuration.GetValue("Scoring:Volume:VolumeRatioThreshold1", 1.2);
        if (snapshot.VolumeRatio.HasValue)
        {
            if (snapshot.VolumeRatio.Value >= volThresh2)
            {
                var pts = _configuration.GetValue("Scoring:Volume:VolumeRatioScore2", 10);
                volumeScore += pts;
                reasons.Add(new SignalReason
                {
                    Code = "VOLUME_SURGE_HIGH",
                    Title = "Yuksek Hacim Patlamasi",
                    Description = $"Hacim son 20 gunluk ortalamanin {snapshot.VolumeRatio.Value:F2} kati.",
                    ScoreContribution = pts,
                    Indicator = "VolumeRatio",
                    IndicatorValue = snapshot.VolumeRatio.Value
                });
            }
            else if (snapshot.VolumeRatio.Value >= volThresh1)
            {
                var pts = _configuration.GetValue("Scoring:Volume:VolumeRatioScore1", 5);
                volumeScore += pts;
                reasons.Add(new SignalReason
                {
                    Code = "VOLUME_SURGE_MODERATE",
                    Title = "Ortalama Ustu Hacim",
                    Description = $"Hacim son 20 gunluk ortalamanin {snapshot.VolumeRatio.Value:F2} kati.",
                    ScoreContribution = pts,
                    Indicator = "VolumeRatio",
                    IndicatorValue = snapshot.VolumeRatio.Value
                });
            }
        }

        // OBV Rising: compare with previous bar's OBV
        bool obvIsRising = false;
        if (history != null && history.Count >= 2 && snapshot.OBV.HasValue)
        {
            var prevBars = history.Take(history.Count - 1).ToList();
            var prevObvs = IndicatorCalculators.CalculateObv(prevBars);
            if (prevObvs.Count > 0 && snapshot.OBV.Value > prevObvs[^1])
            {
                obvIsRising = true;
            }
        }

        if (obvIsRising)
        {
            var pts = _configuration.GetValue("Scoring:Volume:ObvRisingScore", 5);
            volumeScore += pts;
            reasons.Add(new SignalReason
            {
                Code = "OBV_RISING",
                Title = "Para Girisi (OBV Yukseliyor)",
                Description = "Denge Islem Hacmi (OBV) onceki seansa gore artarak kurumsal alim baskisini teyit ediyor.",
                ScoreContribution = pts,
                Indicator = "OBV",
                IndicatorValue = snapshot.OBV ?? 0m
            });
        }

        volumeScore = Math.Min(15, volumeScore);

        // 4. PRICE STRUCTURE SCORING (Max 15)
        if (snapshot.IsBreakout == true)
        {
            var pts = _configuration.GetValue("Scoring:Structure:ResistanceBreakout", 5);
            structureScore += pts;
            reasons.Add(new SignalReason
            {
                Code = "RESISTANCE_BREAKOUT",
                Title = "20 Gunluk Direnc Kirilimi",
                Description = $"Fiyat son 20 gunun en yuksek seviyesini ({snapshot.Resistance1:F2} TL) kirarak yukselis baslatti.",
                ScoreContribution = pts,
                Indicator = "Resistance",
                IndicatorValue = snapshot.Resistance1
            });
        }

        if (history != null && history.Count >= 2)
        {
            var prevBar = history[^2];
            if (currentBar.High > prevBar.High)
            {
                var pts = _configuration.GetValue("Scoring:Structure:HigherHigh", 5);
                structureScore += pts;
                reasons.Add(new SignalReason
                {
                    Code = "HIGHER_HIGH",
                    Title = "Daha Yuksek Zirve (Higher High)",
                    Description = "Mevcut mum onceki seansin tepe noktasini asti.",
                    ScoreContribution = pts,
                    Indicator = "High",
                    IndicatorValue = currentBar.High
                });
            }

            if (currentBar.Low > prevBar.Low)
            {
                var pts = _configuration.GetValue("Scoring:Structure:HigherLow", 5);
                structureScore += pts;
                reasons.Add(new SignalReason
                {
                    Code = "HIGHER_LOW",
                    Title = "Daha Yuksek Dip (Higher Low)",
                    Description = "Mevcut mum onceki seansin dip noktasinin uzerinde kalmayi basardi.",
                    ScoreContribution = pts,
                    Indicator = "Low",
                    IndicatorValue = currentBar.Low
                });
            }
        }
        // NOTE: No free points if history is insufficient. Missing data earns 0 points.

        structureScore = Math.Min(15, structureScore);

        var total = trendScore + momentumScore + volumeScore + structureScore;
        total = Math.Clamp(total, 0, 100);

        var scores = new TechnicalScores(total, trendScore, momentumScore, volumeScore, structureScore);
        return new ScoringEvaluationResult(scores, reasons);
    }
}
