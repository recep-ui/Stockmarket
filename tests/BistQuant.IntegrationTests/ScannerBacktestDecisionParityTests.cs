using BistQuant.Application.Common.Interfaces;
using BistQuant.Application.DTOs.Backtests;
using BistQuant.Application.Services;
using BistQuant.Domain.Entities;
using BistQuant.Domain.Enums;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BistQuant.IntegrationTests;

public class ScannerBacktestDecisionParityTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public ScannerBacktestDecisionParityTests(WebApplicationFactory<Program> factory)
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
            // Steady upward drift with clear trend
            var change = i >= count - 2 ? 6.0m : 0.5m;
            var close = Math.Max(10m, open + change);
            var high = close + 1.5m;
            var low = open - 0.5m;
            var volume = 1000000m + (i * 10000m);

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
    public async Task ScenarioA_RequiredRuleFail_NoScannerSignalAndNoBacktestEntry()
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        var scanner = scope.ServiceProvider.GetRequiredService<IMarketScannerService>();
        var backtestEngine = scope.ServiceProvider.GetRequiredService<IBacktestEngine>();

        // 1. Setup symbol and deterministic candles
        var market = await context.Markets.FirstOrDefaultAsync();
        if (market == null)
        {
            market = new Market { Code = "BIST", Name = "Borsa Istanbul" };
            context.Markets.Add(market);
            await context.SaveChangesAsync();
        }

        var ticker = ("FAIL_" + Guid.NewGuid().ToString("N")).Substring(0, 8).ToUpperInvariant();
        var symbol = new Symbol { MarketId = market.Id, Ticker = ticker, Name = $"{ticker} Test Inc", Sector = "Technology", IsActive = true };
        context.Symbols.Add(symbol);
        await context.SaveChangesAsync();

        var candles = GenerateDeterministicCandles(symbol.Id, 60, startPrice: 100m);
        context.PriceBars.AddRange(candles);
        await context.SaveChangesAsync();

        // 2. Create strategy with a required rule guaranteed to FAIL: Close < 10 TL (all candles are >= 100 TL)
        var failStrategy = new Strategy
        {
            Name = "Guaranteed Fail Strategy",
            Description = "Strategy where required rule fails on every candle",
            StrategyType = "Trend",
            Timeframe = Timeframe.Daily,
            IsActive = true,
            Rules = new List<StrategyRule>
            {
                new StrategyRule
                {
                    Indicator = "Close",
                    Operator = RuleOperator.LessThan,
                    Value = 10m,
                    Weight = 30,
                    RuleGroup = "Default",
                    IsRequired = true
                }
            }
        };
        context.Strategies.Add(failStrategy);
        await context.SaveChangesAsync();

        // 3. Run real MarketScannerService
        var scannerResults = await scanner.ScanUniverseAsync(Timeframe.Daily, failStrategy.Id);

        // Assert: Scanner returns NO strategy-triggered result for this symbol
        Assert.DoesNotContain(scannerResults, r => r.Symbol == ticker);

        // Assert: NO signal entity was persisted in the database for this strategy/symbol
        var persistedSignal = await context.Signals
            .FirstOrDefaultAsync(s => s.SymbolId == symbol.Id && s.StrategyId == failStrategy.Id);
        Assert.Null(persistedSignal);

        // 4. Run real BacktestEngine
        var backtestReq = new BacktestRunRequest(
            StrategyId: failStrategy.Id,
            Symbol: ticker,
            Timeframe: Timeframe.Daily,
            StartDate: candles.First().Timestamp,
            EndDate: candles.Last().Timestamp,
            InitialCapital: 100000m
        );

        var backtestRun = await backtestEngine.RunBacktestAsync(backtestReq);

        // Assert: BacktestEngine produces NO executed trades
        Assert.NotNull(backtestRun);
        Assert.True(backtestRun.Result == null || backtestRun.Result.TotalTrades == 0, "No trades should be generated when required rule fails.");
        var failTrades = await backtestEngine.GetBacktestTradesAsync(backtestRun.Id);
        Assert.Empty(failTrades);
    }

    [Fact]
    public async Task ScenarioB_RequiredRulePass_ExactParityBetweenScannerAndBacktest()
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        var scanner = scope.ServiceProvider.GetRequiredService<IMarketScannerService>();
        var backtestEngine = scope.ServiceProvider.GetRequiredService<IBacktestEngine>();
        var pipeline = scope.ServiceProvider.GetRequiredService<IStrategyEvaluationPipeline>();
        var technicalService = scope.ServiceProvider.GetRequiredService<ITechnicalAnalysisService>();

        // 1. Setup symbol and deterministic candles
        var market = await context.Markets.FirstOrDefaultAsync();
        if (market == null)
        {
            market = new Market { Code = "BIST", Name = "Borsa Istanbul" };
            context.Markets.Add(market);
            await context.SaveChangesAsync();
        }

        var ticker = ("PASS_" + Guid.NewGuid().ToString("N")).Substring(0, 8).ToUpperInvariant();
        var symbol = new Symbol { MarketId = market.Id, Ticker = ticker, Name = $"{ticker} Test Inc", Sector = "Technology", IsActive = true };
        context.Symbols.Add(symbol);
        await context.SaveChangesAsync();

        var candles = GenerateDeterministicCandles(symbol.Id, 60, startPrice: 100m);
        context.PriceBars.AddRange(candles);
        await context.SaveChangesAsync();

        // 2. Create strategy guaranteed to trigger: Close > 50 TL (required)
        var passStrategy = new Strategy
        {
            Name = "Guaranteed Pass Strategy",
            Description = "Strategy where rules pass on trending candles",
            StrategyType = "Trend",
            Timeframe = Timeframe.Daily,
            IsActive = true,
            Rules = new List<StrategyRule>
            {
                new StrategyRule
                {
                    Indicator = "Close",
                    Operator = RuleOperator.GreaterThan,
                    Value = 50m,
                    Weight = 30,
                    RuleGroup = "Default",
                    IsRequired = true
                }
            }
        };
        context.Strategies.Add(passStrategy);
        await context.SaveChangesAsync();

        // 3. Run real MarketScannerService
        var scannerResults = await scanner.ScanUniverseAsync(Timeframe.Daily, passStrategy.Id);

        // Assert: Scanner returns triggered result
        var scannerItem = scannerResults.FirstOrDefault(r => r.Symbol == ticker);
        Assert.NotNull(scannerItem);

        // Assert: Signal persisted in database
        var persistedSignal = await context.Signals
            .Include(s => s.Reasons)
            .FirstOrDefaultAsync(s => s.SymbolId == symbol.Id && s.StrategyId == passStrategy.Id);
        Assert.NotNull(persistedSignal);
        Assert.Equal(symbol.Id, persistedSignal.SymbolId);
        Assert.Equal(passStrategy.Id, persistedSignal.StrategyId);

        // 4. Run real BacktestEngine on the exact candles
        var backtestReq = new BacktestRunRequest(
            StrategyId: passStrategy.Id,
            Symbol: ticker,
            Timeframe: Timeframe.Daily,
            StartDate: candles.First().Timestamp,
            EndDate: candles.Last().Timestamp,
            InitialCapital: 100000m
        );

        var backtestRun = await backtestEngine.RunBacktestAsync(backtestReq);

        // Assert: BacktestEngine generates entry and trades
        Assert.NotNull(backtestRun);
        Assert.NotNull(backtestRun.Result);
        Assert.True(backtestRun.Result.TotalTrades > 0, "Backtest must generate trades when strategy triggers.");
        var passTrades = await backtestEngine.GetBacktestTradesAsync(backtestRun.Id);
        Assert.NotEmpty(passTrades);

        // 5. Compare Parity between Scanner Persisted Signal and Direct Pipeline Evaluation
        var latestBar = candles.Last();
        var prevBar = candles[candles.Count - 2];
        var snapshot = await technicalService.CalculateAndSaveSnapshotAsync(symbol.Id, Timeframe.Daily);
        var prevSnapshot = await context.IndicatorSnapshots
            .AsNoTracking()
            .Where(i => i.SymbolId == symbol.Id && i.Timeframe == Timeframe.Daily && i.Timestamp < latestBar.Timestamp)
            .OrderByDescending(i => i.Timestamp)
            .FirstOrDefaultAsync();

        var history = candles.TakeLast(25).ToList();
        var eval = pipeline.Evaluate(latestBar, snapshot!, history, passStrategy, prevBar, prevSnapshot);

        Assert.True(eval.IsSignalTriggered, "Strategy trigger decision must be true.");
        Assert.Equal(eval.SignalType, persistedSignal.SignalType);
        Assert.Equal(eval.TotalScore, persistedSignal.Score);
        Assert.Equal(eval.TrendScore, persistedSignal.TrendScore);
        Assert.Equal(eval.MomentumScore, persistedSignal.MomentumScore);
        Assert.Equal(eval.VolumeScore, persistedSignal.VolumeScore);
        Assert.Equal(eval.StructureScore, persistedSignal.StructureScore);

        var persistedReasons = persistedSignal.Reasons.Select(r => r.Description).ToList();
        Assert.Equal(eval.MatchedRules.Count, persistedReasons.Count);
        foreach (var rule in eval.MatchedRules)
        {
            Assert.Contains($"Rule matched: {rule}", persistedReasons);
        }
    }

    [Fact]
    public async Task ScenarioC_CrossoverParity_CrossAboveAndCrossBelow_EvaluatedAccurately()
    {
        using var scope = _factory.Services.CreateScope();
        var pipeline = scope.ServiceProvider.GetRequiredService<IStrategyEvaluationPipeline>();

        // 1. CrossAbove: prevBar.Close <= 100, currBar.Close > 100
        var crossAboveRule = new StrategyRule
        {
            Indicator = "Close",
            Operator = RuleOperator.CrossAbove,
            Value = 100m,
            Weight = 25,
            IsRequired = true
        };

        var crossAboveStrategy = new Strategy
        {
            Name = "CrossAbove Test",
            Rules = new List<StrategyRule> { crossAboveRule }
        };

        var prevBarBelow = new PriceBar { Close = 98m };
        var currBarAbove = new PriceBar { Close = 105m };
        var snapshot = new IndicatorSnapshot { RSI14 = 55m };

        var evalAbove = pipeline.Evaluate(currBarAbove, snapshot, new List<PriceBar>(), crossAboveStrategy, prevBarBelow, null);
        Assert.True(evalAbove.IsSignalTriggered, "CrossAbove must trigger when prev <= threshold and curr > threshold.");

        // Not crossing (both above)
        var prevBarAlreadyAbove = new PriceBar { Close = 102m };
        var evalNoCrossAbove = pipeline.Evaluate(currBarAbove, snapshot, new List<PriceBar>(), crossAboveStrategy, prevBarAlreadyAbove, null);
        Assert.False(evalNoCrossAbove.IsSignalTriggered, "CrossAbove must NOT trigger when already above on previous bar.");

        // 2. CrossBelow: prevBar.Close >= 100, currBar.Close < 100
        var crossBelowRule = new StrategyRule
        {
            Indicator = "Close",
            Operator = RuleOperator.CrossBelow,
            Value = 100m,
            Weight = 25,
            IsRequired = true
        };

        var crossBelowStrategy = new Strategy
        {
            Name = "CrossBelow Test",
            Rules = new List<StrategyRule> { crossBelowRule }
        };

        var prevBarAbove2 = new PriceBar { Close = 105m };
        var currBarBelow2 = new PriceBar { Close = 95m };

        var evalBelow = pipeline.Evaluate(currBarBelow2, snapshot, new List<PriceBar>(), crossBelowStrategy, prevBarAbove2, null);
        Assert.True(evalBelow.IsSignalTriggered, "CrossBelow must trigger when prev >= threshold and curr < threshold.");

        // Not crossing (both below)
        var prevBarAlreadyBelow = new PriceBar { Close = 92m };
        var evalNoCrossBelow = pipeline.Evaluate(currBarBelow2, snapshot, new List<PriceBar>(), crossBelowStrategy, prevBarAlreadyBelow, null);
        Assert.False(evalNoCrossBelow.IsSignalTriggered, "CrossBelow must NOT trigger when already below on previous bar.");
    }

    [Fact]
    public async Task ScenarioD_BreakoutParity_PriorResistanceOnly_NoLookAheadBias()
    {
        using var scope = _factory.Services.CreateScope();
        var pipeline = scope.ServiceProvider.GetRequiredService<IStrategyEvaluationPipeline>();

        // Breakout rule: Close > Resistance1
        var breakoutRule = new StrategyRule
        {
            Indicator = "Close",
            Operator = RuleOperator.GreaterThan,
            ComparisonIndicator = "Resistance1",
            Weight = 30,
            IsRequired = true
        };

        var breakoutStrategy = new Strategy
        {
            Name = "Breakout Test",
            Rules = new List<StrategyRule> { breakoutRule }
        };

        // Snapshot calculated strictly from prior candles has Resistance1 = 110
        var snapshot = new IndicatorSnapshot
        {
            Resistance1 = 110m,
            Support1 = 95m
        };

        // Current bar breaks out: Close = 115 > Resistance1 (110)
        var breakoutBar = new PriceBar { Close = 115m };
        var prevBar = new PriceBar { Close = 108m };

        var eval = pipeline.Evaluate(breakoutBar, snapshot, new List<PriceBar>(), breakoutStrategy, prevBar, null);
        Assert.True(eval.IsSignalTriggered, "Breakout must trigger when current close exceeds prior resistance.");
        Assert.Contains(eval.MatchedRules, r => r.Contains("Resistance1"));

        // Current bar fails breakout: Close = 109 <= Resistance1 (110)
        var noBreakoutBar = new PriceBar { Close = 109m };
        var evalNo = pipeline.Evaluate(noBreakoutBar, snapshot, new List<PriceBar>(), breakoutStrategy, prevBar, null);
        Assert.False(evalNo.IsSignalTriggered, "Breakout must not trigger when current close is under prior resistance.");
    }
}
