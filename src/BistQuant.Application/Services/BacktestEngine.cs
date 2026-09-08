using System.Text.Json;
using BistQuant.Application.Common.Interfaces;
using BistQuant.Application.DTOs.Backtests;
using BistQuant.Application.Services.Indicators;
using BistQuant.Domain.Entities;
using BistQuant.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BistQuant.Application.Services;

public interface IBacktestEngine
{
    Task<BacktestRunDto> RunBacktestAsync(BacktestRunRequest request, CancellationToken cancellationToken = default);

    Task<BacktestRunDto?> GetBacktestByIdAsync(long id, CancellationToken cancellationToken = default);

    Task<List<BacktestTradeDto>> GetBacktestTradesAsync(long runId, CancellationToken cancellationToken = default);
}

public class BacktestEngine : IBacktestEngine
{
    private readonly IApplicationDbContext _context;
    private readonly IStrategyEngine _strategyEngine;
    private readonly ISignalEngine _signalEngine;
    private readonly IStrategyEvaluationPipeline _pipeline;
    private readonly ILogger<BacktestEngine> _logger;

    public BacktestEngine(
        IApplicationDbContext context,
        IStrategyEngine strategyEngine,
        ISignalEngine signalEngine,
        IStrategyEvaluationPipeline pipeline,
        ILogger<BacktestEngine> logger)
    {
        _context = context;
        _strategyEngine = strategyEngine;
        _signalEngine = signalEngine;
        _pipeline = pipeline;
        _logger = logger;
    }

    public async Task<BacktestRunDto> RunBacktestAsync(BacktestRunRequest request, CancellationToken cancellationToken = default)
    {
        var targetSymbol = request.Symbol.ToUpperInvariant();
        var symbolEntities = await _context.Symbols
            .AsNoTracking()
            .Where(s => s.IsActive && (targetSymbol == "ALL" || s.Ticker == targetSymbol))
            .ToListAsync(cancellationToken);

        if (symbolEntities.Count == 0)
        {
            throw new ArgumentException($"No active symbols found matching '{request.Symbol}'");
        }

        Strategy? strategy = null;
        if (request.StrategyId.HasValue)
        {
            strategy = await _context.Strategies
                .Include(s => s.Rules)
                .FirstOrDefaultAsync(s => s.Id == request.StrategyId.Value, cancellationToken);
        }
        else
        {
            strategy = await _context.Strategies
                .Include(s => s.Rules)
                .FirstOrDefaultAsync(cancellationToken);
        }

        if (strategy == null)
        {
            strategy = new Strategy
            {
                Name = "Default Trend Following",
                Description = "EMA20 > EMA50 trend continuation",
                Timeframe = request.Timeframe,
                IsActive = true
            };
            _context.Strategies.Add(strategy);
            await _context.SaveChangesAsync(cancellationToken);
        }

        var run = new BacktestRun
        {
            StrategyId = strategy.Id,
            SymbolList = request.Symbol,
            Timeframe = request.Timeframe,
            StartDate = request.StartDate ?? DateTime.UtcNow.AddMonths(-6),
            EndDate = request.EndDate ?? DateTime.UtcNow,
            InitialCapital = request.InitialCapital,
            CommissionRate = request.CommissionRate,
            SlippageRate = request.SlippageRate,
            Status = BacktestStatus.Running
        };

        _context.BacktestRuns.Add(run);
        await _context.SaveChangesAsync(cancellationToken);

        try
        {
            var trades = new List<BacktestTrade>();
            decimal cash = request.InitialCapital;
            decimal peakEquity = cash;
            decimal maxDrawdown = 0;

            // Load bars and precompute indicators per symbol
            var symbolDataList = new List<SymbolBacktestData>();
            foreach (var sym in symbolEntities)
            {
                var bars = await _context.PriceBars
                    .AsNoTracking()
                    .Where(p => p.SymbolId == sym.Id && p.Timeframe == request.Timeframe && p.Timestamp >= run.StartDate && p.Timestamp <= run.EndDate)
                    .OrderBy(p => p.Timestamp)
                    .ToListAsync(cancellationToken);

                if (bars.Count < 25) continue;

                var snapshots = IndicatorCalculators.CalculateSnapshots(bars, sym.Id, request.Timeframe);
                var dateMap = new Dictionary<DateTime, int>(bars.Count);

                for (int i = 0; i < bars.Count; i++)
                {
                    dateMap[bars[i].Timestamp] = i;
                }

                symbolDataList.Add(new SymbolBacktestData
                {
                    Symbol = sym,
                    Bars = bars,
                    DateToIndex = dateMap,
                    Snapshots = snapshots
                });
            }

            var equityCurve = new List<EquityPointDto>
            {
                new(run.StartDate.ToString("yyyy-MM-dd"), cash, 0m)
            };

            // Global chronological timeline
            var allTimestamps = symbolDataList
                .SelectMany(s => s.Bars.Select(b => b.Timestamp))
                .Distinct()
                .OrderBy(t => t)
                .ToList();

            var openPositions = new Dictionary<int, OpenBacktestPosition>();
            var pendingEntries = new Dictionary<int, PendingBacktestEntry>();
            var latestPrices = new Dictionary<int, decimal>();

            for (int tIdx = 0; tIdx < allTimestamps.Count; tIdx++)
            {
                var currentTimestamp = allTimestamps[tIdx];
                bool isLastTimestamp = (tIdx == allTimestamps.Count - 1);

                // 1. Update latest prices for symbols that traded at this timestamp
                foreach (var symData in symbolDataList)
                {
                    if (symData.DateToIndex.TryGetValue(currentTimestamp, out int barIdx))
                    {
                        latestPrices[symData.Symbol.Id] = symData.Bars[barIdx].Close;
                    }
                }

                // 2. Execute pending entries from previous candle close at current candle OPEN (Zero Look-Ahead)
                foreach (var (symId, pending) in pendingEntries.ToList())
                {
                    if (openPositions.ContainsKey(symId)) continue;

                    var symData = symbolDataList.FirstOrDefault(s => s.Symbol.Id == symId);
                    if (symData == null || !symData.DateToIndex.TryGetValue(currentTimestamp, out int barIdx))
                    {
                        continue; // Symbol didn't trade at this timestamp
                    }

                    var bar = symData.Bars[barIdx];
                    decimal entryPrice = Math.Round(bar.Open * (1m + request.SlippageRate), 2);
                    if (entryPrice <= 0) continue;

                    // Portfolio-level risk sizing: 20% of current equity, bounded by available cash
                    decimal portfolioEquity = cash + openPositions.Sum(p => p.Value.Quantity * latestPrices.GetValueOrDefault(p.Key, p.Value.EntryPrice));
                    decimal targetAllocation = Math.Min(cash, portfolioEquity * 0.20m);
                    decimal costPerShare = entryPrice * (1m + request.CommissionRate);
                    decimal quantity = costPerShare > 0 ? Math.Floor(targetAllocation / costPerShare) : 0;

                    if (quantity > 0)
                    {
                        decimal positionCost = quantity * entryPrice;
                        decimal entryComm = positionCost * request.CommissionRate;

                        if (cash >= positionCost + entryComm)
                        {
                            // Invariant: cash decreases by cost + commission
                            cash -= (positionCost + entryComm);

                            decimal curAtr = pending.Atr ?? (entryPrice * 0.02m);
                            decimal stopLoss = Math.Round(entryPrice - (curAtr * 2.0m), 2);
                            decimal takeProfit = Math.Round(entryPrice + (curAtr * 3.0m), 2);

                            openPositions[symId] = new OpenBacktestPosition
                            {
                                SymbolId = symId,
                                EntryPrice = entryPrice,
                                EntryDate = currentTimestamp,
                                Quantity = quantity,
                                StopLoss = stopLoss,
                                TakeProfit = takeProfit,
                                EntryCommission = entryComm
                            };
                        }
                    }
                }
                pendingEntries.Clear();

                // 3. Evaluate exit conditions on current bar for open positions
                foreach (var (symId, pos) in openPositions.ToList())
                {
                    var symData = symbolDataList.FirstOrDefault(s => s.Symbol.Id == symId);
                    if (symData == null || !symData.DateToIndex.TryGetValue(currentTimestamp, out int barIdx))
                    {
                        continue;
                    }

                    var bar = symData.Bars[barIdx];
                    bool exitTriggered = false;
                    decimal exitPrice = 0;
                    string exitReason = "";

                    if (bar.Low <= pos.StopLoss)
                    {
                        exitTriggered = true;
                        // Gap-through stop: if candle opens below stop loss, execution occurs at Open
                        decimal baseStop = bar.Open < pos.StopLoss ? bar.Open : pos.StopLoss;
                        exitPrice = Math.Round(baseStop * (1m - request.SlippageRate), 2);
                        exitReason = "StopLoss";
                    }
                    else if (bar.High >= pos.TakeProfit)
                    {
                        exitTriggered = true;
                        exitPrice = Math.Round(pos.TakeProfit * (1m - request.SlippageRate), 2);
                        exitReason = "TakeProfit";
                    }
                    else if (isLastTimestamp)
                    {
                        exitTriggered = true;
                        exitPrice = Math.Round(bar.Close * (1m - request.SlippageRate), 2);
                        exitReason = "EndOfData";
                    }

                    if (exitTriggered)
                    {
                        decimal proceeds = pos.Quantity * exitPrice;
                        decimal exitComm = proceeds * request.CommissionRate;
                        
                        // Invariant: cash increases by proceeds - exitComm
                        cash += (proceeds - exitComm);

                        decimal grossPnL = (exitPrice - pos.EntryPrice) * pos.Quantity;
                        decimal netPnL = grossPnL - pos.EntryCommission - exitComm;
                        decimal positionCost = pos.Quantity * pos.EntryPrice;
                        decimal retPct = positionCost > 0 ? (netPnL / positionCost) * 100m : 0m;

                        trades.Add(new BacktestTrade
                        {
                            BacktestRunId = run.Id,
                            SymbolId = symId,
                            EntryDate = pos.EntryDate,
                            EntryPrice = pos.EntryPrice,
                            ExitDate = currentTimestamp,
                            ExitPrice = exitPrice,
                            Quantity = pos.Quantity,
                            GrossPnL = Math.Round(grossPnL, 2),
                            NetPnL = Math.Round(netPnL, 2),
                            ReturnPercent = Math.Round(retPct, 2),
                            ExitReason = exitReason
                        });

                        openPositions.Remove(symId);
                    }
                }

                // 4. Evaluate buy signals on candle CLOSE for non-final timestamps
                if (!isLastTimestamp && cash > 1000)
                {
                    foreach (var symData in symbolDataList)
                    {
                        int symId = symData.Symbol.Id;
                        if (openPositions.ContainsKey(symId)) continue;

                        if (symData.DateToIndex.TryGetValue(currentTimestamp, out int barIdx) && barIdx >= 20)
                        {
                            var snapshot = symData.Snapshots[barIdx];
                            var bar = symData.Bars[barIdx];
                            var prevSnapshot = barIdx > 0 ? symData.Snapshots[barIdx - 1] : null;
                            var prevBar = barIdx > 0 ? symData.Bars[barIdx - 1] : null;
                            var history = symData.Bars.Take(barIdx + 1).TakeLast(25).ToList();

                            var eval = _pipeline.Evaluate(bar, snapshot, history, strategy, prevBar, prevSnapshot);
                            if (eval.IsSignalTriggered)
                            {
                                pendingEntries[symId] = new PendingBacktestEntry
                                {
                                    SymbolId = symId,
                                    Atr = snapshot.ATR14
                                };
                            }
                        }
                    }
                }

                // 5. Compute Portfolio Equity at current timestamp
                decimal currentPositionsValue = openPositions.Sum(p => p.Value.Quantity * latestPrices.GetValueOrDefault(p.Key, p.Value.EntryPrice));
                decimal totalEquity = cash + currentPositionsValue;

                if (totalEquity > peakEquity) peakEquity = totalEquity;
                decimal dd = peakEquity > 0 ? ((peakEquity - totalEquity) / peakEquity) * 100m : 0m;
                if (dd > maxDrawdown) maxDrawdown = dd;

                equityCurve.Add(new EquityPointDto(
                    currentTimestamp.ToString("yyyy-MM-dd"),
                    Math.Round(totalEquity, 2),
                    Math.Round(dd, 2)
                ));
            }

            // Calculate aggregate statistics
            int totalTrades = trades.Count;
            int winTrades = trades.Count(t => t.NetPnL > 0);
            int lossTrades = trades.Count(t => t.NetPnL <= 0);
            decimal winRate = totalTrades > 0 ? Math.Round((winTrades / (decimal)totalTrades) * 100m, 2) : 0m;

            decimal totalNetProfit = trades.Sum(t => t.NetPnL);
            decimal totalReturn = request.InitialCapital > 0 ? Math.Round((totalNetProfit / request.InitialCapital) * 100m, 2) : 0m;

            var totalDays = (decimal)(run.EndDate - run.StartDate).TotalDays;
            if (totalDays < 1) totalDays = 1;
            decimal finalEquity = cash + openPositions.Sum(p => p.Value.Quantity * latestPrices.GetValueOrDefault(p.Key, p.Value.EntryPrice));
            decimal annualizedReturn = 0;
            if (request.InitialCapital > 0 && finalEquity > 0)
            {
                double ratio = (double)(finalEquity / request.InitialCapital);
                double exponent = 365.0 / (double)totalDays;
                annualizedReturn = Math.Round(((decimal)Math.Pow(ratio, exponent) - 1m) * 100m, 2);
            }

            decimal netWins = trades.Where(t => t.NetPnL > 0).Sum(t => t.NetPnL);
            decimal netLosses = Math.Abs(trades.Where(t => t.NetPnL < 0).Sum(t => t.NetPnL));
            decimal profitFactor = netLosses > 0 ? Math.Round(netWins / netLosses, 2) : (netWins > 0 ? 10.0m : 1.0m);

            decimal avgWin = winTrades > 0 ? Math.Round(netWins / winTrades, 2) : 0m;
            decimal avgLoss = lossTrades > 0 ? Math.Round(netLosses / lossTrades, 2) : 0m;

            decimal expectancy = totalTrades > 0 ? Math.Round(totalNetProfit / totalTrades, 2) : 0m;

            // Sharpe and Sortino ratios computed from daily portfolio equity returns
            decimal? sharpeRatio = null;
            decimal? sortinoRatio = null;

            if (equityCurve.Count >= 3 && trades.Count >= 3)
            {
                var dailyReturns = new List<double>();
                for (int i = 1; i < equityCurve.Count; i++)
                {
                    decimal prev = equityCurve[i - 1].Equity;
                    if (prev > 0)
                    {
                        dailyReturns.Add((double)((equityCurve[i].Equity - prev) / prev));
                    }
                }

                if (dailyReturns.Count >= 2)
                {
                    double avgRet = dailyReturns.Average();
                    double variance = dailyReturns.Select(r => Math.Pow(r - avgRet, 2)).Average();
                    double stdDev = Math.Sqrt(variance);

                    if (stdDev > 1e-9)
                    {
                        sharpeRatio = Math.Round((decimal)((avgRet / stdDev) * Math.Sqrt(252)), 2);
                    }

                    var downReturns = dailyReturns.Where(r => r < 0).ToList();
                    if (downReturns.Count > 0)
                    {
                        double downVariance = downReturns.Select(r => Math.Pow(r, 2)).Average();
                        double downStdDev = Math.Sqrt(downVariance);
                        if (downStdDev > 1e-9)
                        {
                            sortinoRatio = Math.Round((decimal)((avgRet / downStdDev) * Math.Sqrt(252)), 2);
                        }
                    }
                }
            }

            var result = new BacktestResult
            {
                BacktestRunId = run.Id,
                TotalTrades = totalTrades,
                WinningTrades = winTrades,
                LosingTrades = lossTrades,
                WinRate = winRate,
                TotalReturn = totalReturn,
                AnnualizedReturn = annualizedReturn,
                AverageWin = avgWin,
                AverageLoss = avgLoss,
                ProfitFactor = profitFactor,
                MaxDrawdown = Math.Round(maxDrawdown, 2),
                SharpeRatio = sharpeRatio,
                SortinoRatio = sortinoRatio,
                Expectancy = expectancy,
                EquityCurveJson = JsonSerializer.Serialize(equityCurve)
            };

            run.Status = BacktestStatus.Completed;
            run.Result = result;
            _context.BacktestTrades.AddRange(trades);
            _context.BacktestResults.Add(result);
            await _context.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Backtest {RunId} completed successfully with {TradeCount} trades and Return {Return}%.", run.Id, totalTrades, totalReturn);

            return new BacktestRunDto(
                run.Id,
                run.StrategyId,
                strategy?.Name ?? "Predefined Quantitative Strategy",
                run.SymbolList,
                run.Timeframe,
                run.StartDate,
                run.EndDate,
                run.InitialCapital,
                run.Status,
                new BacktestResultDto(
                    run.Id,
                    totalTrades,
                    winTrades,
                    lossTrades,
                    winRate,
                    totalReturn,
                    result.AnnualizedReturn,
                    avgWin,
                    avgLoss,
                    profitFactor,
                    result.MaxDrawdown,
                    sharpeRatio,
                    sortinoRatio,
                    expectancy,
                    equityCurve
                )
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error running backtest {RunId}.", run.Id);
            run.Status = BacktestStatus.Failed;
            run.ErrorMessage = ex.Message;
            await _context.SaveChangesAsync(cancellationToken);
            throw;
        }
    }

    public async Task<BacktestRunDto?> GetBacktestByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        var run = await _context.BacktestRuns
            .AsNoTracking()
            .Include(r => r.Strategy)
            .Include(r => r.Result)
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

        if (run == null) return null;

        BacktestResultDto? resultDto = null;
        if (run.Result != null)
        {
            var equityCurve = !string.IsNullOrEmpty(run.Result.EquityCurveJson)
                ? JsonSerializer.Deserialize<List<EquityPointDto>>(run.Result.EquityCurveJson) ?? new List<EquityPointDto>()
                : new List<EquityPointDto>();

            resultDto = new BacktestResultDto(
                run.Id,
                run.Result.TotalTrades,
                run.Result.WinningTrades,
                run.Result.LosingTrades,
                run.Result.WinRate,
                run.Result.TotalReturn,
                run.Result.AnnualizedReturn,
                run.Result.AverageWin,
                run.Result.AverageLoss,
                run.Result.ProfitFactor,
                run.Result.MaxDrawdown,
                run.Result.SharpeRatio,
                run.Result.SortinoRatio,
                run.Result.Expectancy,
                equityCurve
            );
        }

        return new BacktestRunDto(
            run.Id,
            run.StrategyId,
            run.Strategy?.Name ?? "Predefined Quantitative Strategy",
            run.SymbolList,
            run.Timeframe,
            run.StartDate,
            run.EndDate,
            run.InitialCapital,
            run.Status,
            resultDto
        );
    }

    public async Task<List<BacktestTradeDto>> GetBacktestTradesAsync(long runId, CancellationToken cancellationToken = default)
    {
        var trades = await _context.BacktestTrades
            .AsNoTracking()
            .Include(t => t.Symbol)
            .Where(t => t.BacktestRunId == runId)
            .OrderBy(t => t.EntryDate)
            .ToListAsync(cancellationToken);

        return trades.Select(t => new BacktestTradeDto(
            t.Id,
            t.Symbol.Ticker,
            t.EntryDate,
            t.EntryPrice,
            t.ExitDate,
            t.ExitPrice,
            t.Quantity,
            t.GrossPnL,
            t.NetPnL,
            t.ReturnPercent,
            t.ExitReason
        )).ToList();
    }
}

internal class SymbolBacktestData
{
    public Symbol Symbol { get; set; } = null!;
    public List<PriceBar> Bars { get; set; } = new();
    public Dictionary<DateTime, int> DateToIndex { get; set; } = new();
    public List<IndicatorSnapshot> Snapshots { get; set; } = new();
}

internal class OpenBacktestPosition
{
    public int SymbolId { get; set; }
    public decimal EntryPrice { get; set; }
    public DateTime EntryDate { get; set; }
    public decimal Quantity { get; set; }
    public decimal StopLoss { get; set; }
    public decimal TakeProfit { get; set; }
    public decimal EntryCommission { get; set; }
}

internal class PendingBacktestEntry
{
    public int SymbolId { get; set; }
    public decimal? Atr { get; set; }
}

