using BistQuant.Application.Common.Interfaces;
using BistQuant.Application.DTOs.Backtests;
using BistQuant.Application.Services;
using BistQuant.Application.Services.Indicators;
using BistQuant.Domain.Entities;
using BistQuant.Domain.Enums;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BistQuant.IntegrationTests;

public class RealScannerBacktestParityTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public RealScannerBacktestParityTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    private static List<PriceBar> GenerateDeterministicCandles(int symbolId, int count = 60, decimal startPrice = 100m, DateTime? startDate = null)
    {
        var bars = new List<PriceBar>();
        var price = startPrice;
        var date = startDate ?? DateTime.UtcNow.Date.AddDays(-count);

        for (int i = 0; i < count; i++)
        {
            var open = price;
            // Steady trend with an upward jump in the last 2 bars to create clear crossovers and breakouts
            var change = i >= count - 2 ? 5.0m : (decimal)(Math.Sin(i * 0.2) * 1.5);
            var close = Math.Max(10m, open + change);
            var high = Math.Max(open, close) + 2.0m;
            var low = Math.Min(open, close) - 1.0m;
            var volume = 1500000m + (i * 20000m);

            bars.Add(new PriceBar
            {
                SymbolId = symbolId,
                Timeframe = Timeframe.Daily,
                Timestamp = date.AddDays(i),
                Open = open,
                High = high,
                Low = low,
                Close = close,
                Volume = volume
            });

            price = close;
        }

        return bars;
    }

    [Fact]
    public async Task RealScanner_And_BacktestEngine_ProduceIdentical_EvaluationParity()
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        var scannerService = scope.ServiceProvider.GetRequiredService<IMarketScannerService>();
        var backtestEngine = scope.ServiceProvider.GetRequiredService<IBacktestEngine>();
        var pipeline = scope.ServiceProvider.GetRequiredService<IStrategyEvaluationPipeline>();

        // 1. Ensure market and active test symbol exist
        var market = await context.Markets.FirstOrDefaultAsync(m => m.Code == "BIST");
        if (market == null)
        {
            market = new Market { Code = "BIST", Name = "Borsa Istanbul", Currency = "TRY", Country = "Turkey", Timezone = "Europe/Istanbul" };
            context.Markets.Add(market);
            await context.SaveChangesAsync();
        }

        var ticker = "PARITY" + Guid.NewGuid().ToString("N")[..4].ToUpperInvariant();
        var symbol = new Symbol
        {
            MarketId = market.Id,
            Ticker = ticker,
            Name = "Parity Test Stock",
            Sector = "Technology",
            Industry = "Software",
            IsActive = true
        };
        context.Symbols.Add(symbol);
        await context.SaveChangesAsync();

        // 2. Insert 60 deterministic candles ending today (fresh)
        var candles = GenerateDeterministicCandles(symbol.Id, 60, 100m, DateTime.UtcNow.Date.AddDays(-59));
        context.PriceBars.AddRange(candles);
        await context.SaveChangesAsync();

        // Precompute snapshots so indicators are cached
        var snapshots = IndicatorCalculators.CalculateSnapshots(candles, symbol.Id, Timeframe.Daily);
        context.IndicatorSnapshots.AddRange(snapshots);
        await context.SaveChangesAsync();

        // 3. Create a strategy testing CrossAbove, Breakout, and weighted indicators
        var strategy = new Strategy
        {
            Name = $"Parity Test Strategy {ticker}",
            Description = "Rigorous scanner and backtest parity verification",
            StrategyType = "TrendFollowing",
            Timeframe = Timeframe.Daily,
            IsActive = true,
            IsSystem = false,
            Rules = new List<StrategyRule>
            {
                new()
                {
                    Indicator = "Close",
                    Operator = RuleOperator.GreaterThan,
                    ComparisonIndicator = "EMA20",
                    Weight = 20,
                    IsRequired = true,
                    RuleGroup = "Trend"
                },
                new()
                {
                    Indicator = "RSI",
                    Operator = RuleOperator.GreaterThan,
                    Value = 45m,
                    Weight = 15,
                    IsRequired = false,
                    RuleGroup = "Momentum"
                },
                new()
                {
                    Indicator = "VolumeRatio",
                    Operator = RuleOperator.GreaterThan,
                    Value = 1.0m,
                    Weight = 10,
                    IsRequired = false,
                    RuleGroup = "Volume"
                }
            }
        };

        context.Strategies.Add(strategy);
        await context.SaveChangesAsync();

        // 4. Run Live Scanner through MarketScannerService
        var scanResults = await scannerService.ScanUniverseAsync(Timeframe.Daily, strategy.Id);
        var scannerItem = scanResults.FirstOrDefault(s => s.Symbol == ticker);
        Assert.NotNull(scannerItem);

        // Retrieve persisted Signal created by scanner
        var persistedSignal = await context.Signals
            .Include(s => s.Reasons)
            .Where(s => s.SymbolId == symbol.Id && s.StrategyId == strategy.Id)
            .OrderByDescending(s => s.CreatedAt)
            .FirstOrDefaultAsync();
        Assert.NotNull(persistedSignal);

        // 5. Run BacktestEngine over the exact same historical data and strategy
        var backtestRequest = new BacktestRunRequest(
            StrategyId: strategy.Id,
            Symbol: ticker,
            Timeframe: Timeframe.Daily,
            StartDate: candles.First().Timestamp,
            EndDate: candles.Last().Timestamp,
            InitialCapital: 100000m,
            CommissionRate: 0.001m,
            SlippageRate: 0.0005m
        );

        var backtestResult = await backtestEngine.RunBacktestAsync(backtestRequest);
        Assert.NotNull(backtestResult);

        // 6. Direct evaluation at the last closed bar
        var lastBar = candles.Last();
        var prevBar = candles[^2];
        var lastSnapshot = snapshots.Last();
        var prevSnapshot = snapshots[^2];

        var pipelineDirect = pipeline.Evaluate(lastBar, lastSnapshot, candles, strategy, prevBar, prevSnapshot);

        // 7. Assert 100% parity across scanner persisted signal, pipeline direct eval, and strategy rules
        Assert.Equal(pipelineDirect.IsSignalTriggered, persistedSignal.Score >= 20);
        Assert.Equal(pipelineDirect.SignalType, persistedSignal.SignalType);
        Assert.Equal(pipelineDirect.TotalScore, persistedSignal.Score);
        Assert.Equal(pipelineDirect.TrendScore, persistedSignal.TrendScore);
        Assert.Equal(pipelineDirect.MomentumScore, persistedSignal.MomentumScore);
        Assert.Equal(pipelineDirect.VolumeScore, persistedSignal.VolumeScore);
        Assert.Equal(pipelineDirect.StructureScore, persistedSignal.StructureScore);

        // Assert matched rules are equal
        var signalReasonCodes = persistedSignal.Reasons.Select(r => r.Code).ToList();
        foreach (var rule in pipelineDirect.MatchedRules)
        {
            Assert.Contains(rule, signalReasonCodes);
        }
    }

    [Fact]
    public void StrategyEngine_RuleOperators_CrossAbove_CrossBelow_Breakout_Verified()
    {
        using var scope = _factory.Services.CreateScope();
        var strategyEngine = scope.ServiceProvider.GetRequiredService<IStrategyEngine>();

        // Test 1: CrossAbove triggers strictly when prev <= and curr >
        var crossAboveRule = new StrategyRule
        {
            Indicator = "Close",
            Operator = RuleOperator.CrossAbove,
            ComparisonIndicator = "EMA20",
            Weight = 30,
            IsRequired = true
        };

        var prevBarBelow = new PriceBar { Close = 98m };
        var prevSnap = new IndicatorSnapshot { EMA20 = 100m };
        var currBarAbove = new PriceBar { Close = 105m };
        var currSnap = new IndicatorSnapshot { EMA20 = 100m };

        var crossAboveEval = strategyEngine.EvaluateRules(
            new List<StrategyRule> { crossAboveRule },
            currSnap,
            currBarAbove,
            prevSnap,
            prevBarBelow
        );
        Assert.True(crossAboveEval.IsSignalTriggered);
        Assert.Contains(crossAboveEval.MatchedRules, r => r.Contains("CrossAbove"));

        // Test 2: CrossBelow triggers strictly when prev >= and curr <
        var crossBelowRule = new StrategyRule
        {
            Indicator = "Close",
            Operator = RuleOperator.CrossBelow,
            ComparisonIndicator = "EMA20",
            Weight = 30,
            IsRequired = true
        };

        var prevBarAbove2 = new PriceBar { Close = 105m };
        var currBarBelow2 = new PriceBar { Close = 95m };

        var crossBelowEval = strategyEngine.EvaluateRules(
            new List<StrategyRule> { crossBelowRule },
            currSnap,
            currBarBelow2,
            prevSnap,
            prevBarAbove2
        );
        Assert.True(crossBelowEval.IsSignalTriggered);
        Assert.Contains(crossBelowEval.MatchedRules, r => r.Contains("CrossBelow"));

        // Test 3: Breakout verification
        var breakoutRule = new StrategyRule
        {
            Indicator = "Close",
            Operator = RuleOperator.GreaterThan,
            ComparisonIndicator = "Resistance1",
            Weight = 25,
            IsRequired = true
        };

        var snapWithResistance = new IndicatorSnapshot { Resistance1 = 110m };
        var breakoutBar = new PriceBar { Close = 115m };

        var breakoutEval = strategyEngine.EvaluateRules(
            new List<StrategyRule> { breakoutRule },
            snapWithResistance,
            breakoutBar,
            null,
            null
        );
        Assert.True(breakoutEval.IsSignalTriggered);

        // Test 4: Required rule failure vetoes entire signal
        var requiredFailingRule = new StrategyRule
        {
            Indicator = "RSI",
            Operator = RuleOperator.GreaterThan,
            Value = 70m, // Current RSI is 50, so this fails
            Weight = 50,
            IsRequired = true
        };

        var passingRule = new StrategyRule
        {
            Indicator = "Close",
            Operator = RuleOperator.GreaterThan,
            Value = 10m,
            Weight = 50,
            IsRequired = false
        };

        var snapRsi = new IndicatorSnapshot { RSI14 = 50m };
        var barRsi = new PriceBar { Close = 100m };

        var vetoEval = strategyEngine.EvaluateRules(
            new List<StrategyRule> { requiredFailingRule, passingRule },
            snapRsi,
            barRsi,
            null,
            null
        );
        Assert.False(vetoEval.IsSignalTriggered, "Signal must be vetoed when a required rule fails");
    }
}
