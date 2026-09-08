using BistQuant.Application.Common.Interfaces;
using BistQuant.Application.DTOs.PaperTrading;
using BistQuant.Domain.Entities;
using BistQuant.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BistQuant.Application.Services;

public interface IPaperTradingService
{
    Task<PaperPortfolioDto> GetOrCreateDefaultPortfolioAsync(long userId, CancellationToken cancellationToken = default);

    Task<List<PaperPositionDto>> GetPositionsAsync(long portfolioId, CancellationToken cancellationToken = default);

    Task<List<PaperTradeDto>> GetTradesAsync(long portfolioId, CancellationToken cancellationToken = default);

    Task<PaperTradeDto> ExecuteOrderAsync(CreatePaperOrderRequest request, CancellationToken cancellationToken = default);

    Task<int> ExecutePendingOrdersForSessionAsync(DateOnly sessionDate, CancellationToken cancellationToken = default);

    Task AutoTradeScanAsync(long portfolioId, CancellationToken cancellationToken = default);
}

public class PaperTradingService : IPaperTradingService
{
    private readonly IApplicationDbContext _context;
    private readonly ISignalEngine _signalEngine;
    private readonly IMarketDataFreshnessPolicy _freshnessPolicy;
    private readonly IMarketSessionCalendar? _sessionCalendar;
    private readonly ILogger<PaperTradingService> _logger;

    private const decimal CommissionRate = 0.0015m; // 0.15%

    public PaperTradingService(
        IApplicationDbContext context,
        ISignalEngine signalEngine,
        IMarketDataFreshnessPolicy freshnessPolicy,
        ILogger<PaperTradingService> logger,
        IMarketSessionCalendar? sessionCalendar = null)
    {
        _context = context;
        _signalEngine = signalEngine;
        _freshnessPolicy = freshnessPolicy;
        _logger = logger;
        _sessionCalendar = sessionCalendar;
    }

    public async Task<PaperPortfolioDto> GetOrCreateDefaultPortfolioAsync(long userId, CancellationToken cancellationToken = default)
    {
        var portfolio = await _context.PaperPortfolios
            .Include(p => p.Positions)
            .ThenInclude(pos => pos.Symbol)
            .FirstOrDefaultAsync(p => p.UserId == userId, cancellationToken);

        if (portfolio == null)
        {
            portfolio = new PaperPortfolio
            {
                UserId = userId,
                Name = "Sanal BIST Portfoyu",
                InitialBalance = 100000m,
                CashBalance = 100000m,
                IsAutoTradingEnabled = false,
                AutoTradingMinScore = 80,
                AutoTradingMaxAllocationPercent = 5.0m
            };

            _context.PaperPortfolios.Add(portfolio);
            await _context.SaveChangesAsync(cancellationToken);
        }

        // Calculate real-time portfolio market value
        decimal positionsValue = 0;
        foreach (var pos in portfolio.Positions)
        {
            var latestBar = await _context.PriceBars
                .AsNoTracking()
                .Where(p => p.SymbolId == pos.SymbolId && p.Timeframe == Timeframe.Daily)
                .OrderByDescending(p => p.Timestamp)
                .FirstOrDefaultAsync(cancellationToken);

            pos.CurrentPrice = latestBar?.Close ?? pos.AveragePrice;
            positionsValue += pos.Quantity * pos.CurrentPrice;
        }

        decimal totalValue = portfolio.CashBalance + positionsValue;
        decimal totalPnL = totalValue - portfolio.InitialBalance;
        decimal totalPnLPct = portfolio.InitialBalance > 0 ? (totalPnL / portfolio.InitialBalance) * 100m : 0m;

        return new PaperPortfolioDto(
            portfolio.Id,
            portfolio.UserId,
            portfolio.Name,
            portfolio.InitialBalance,
            Math.Round(portfolio.CashBalance, 2),
            Math.Round(totalValue, 2),
            Math.Round(totalPnL, 2),
            Math.Round(totalPnLPct, 2),
            portfolio.IsAutoTradingEnabled,
            portfolio.AutoTradingMinScore,
            portfolio.AutoTradingMaxAllocationPercent
        );
    }

    public async Task<List<PaperPositionDto>> GetPositionsAsync(long portfolioId, CancellationToken cancellationToken = default)
    {
        var positions = await _context.PaperPositions
            .Include(p => p.Symbol)
            .Where(p => p.PortfolioId == portfolioId && p.Quantity > 0)
            .ToListAsync(cancellationToken);

        var result = new List<PaperPositionDto>();

        foreach (var pos in positions)
        {
            var latestBar = await _context.PriceBars
                .AsNoTracking()
                .Where(p => p.SymbolId == pos.SymbolId && p.Timeframe == Timeframe.Daily)
                .OrderByDescending(p => p.Timestamp)
                .FirstOrDefaultAsync(cancellationToken);

            decimal currentPrice = latestBar?.Close ?? pos.AveragePrice;
            decimal totalCost = pos.Quantity * pos.AveragePrice;
            decimal currentValue = pos.Quantity * currentPrice;
            decimal unrealizedPnL = currentValue - totalCost;
            decimal unrealizedPnLPct = totalCost > 0 ? (unrealizedPnL / totalCost) * 100m : 0m;

            result.Add(new PaperPositionDto(
                pos.Id,
                pos.SymbolId,
                pos.Symbol.Ticker,
                pos.Quantity,
                pos.AveragePrice,
                currentPrice,
                Math.Round(totalCost, 2),
                Math.Round(currentValue, 2),
                Math.Round(unrealizedPnL, 2),
                Math.Round(unrealizedPnLPct, 2)
            ));
        }

        return result;
    }

    public async Task<List<PaperTradeDto>> GetTradesAsync(long portfolioId, CancellationToken cancellationToken = default)
    {
        var trades = await _context.PaperTrades
            .AsNoTracking()
            .Include(t => t.Symbol)
            .Where(t => t.PortfolioId == portfolioId)
            .OrderByDescending(t => t.ExecutedAt)
            .ToListAsync(cancellationToken);

        return trades.Select(t => new PaperTradeDto(
            t.Id,
            t.SymbolId,
            t.Symbol.Ticker,
            t.Side,
            t.Quantity,
            t.Price,
            Math.Round(t.Quantity * t.Price, 2),
            t.RealizedPnL,
            t.Commission,
            t.ExecutedAt
        )).ToList();
    }

    public async Task<PaperTradeDto> ExecuteOrderAsync(CreatePaperOrderRequest request, CancellationToken cancellationToken = default)
    {
        // 1. Idempotency check with ClientOrderId
        if (!string.IsNullOrWhiteSpace(request.ClientOrderId))
        {
            var existingOrder = await _context.PaperOrders
                .FirstOrDefaultAsync(o => o.PortfolioId == request.PortfolioId && o.ClientOrderId == request.ClientOrderId, cancellationToken);

            if (existingOrder != null)
            {
                var existingTrade = await _context.PaperTrades
                    .Include(t => t.Symbol)
                    .FirstOrDefaultAsync(t => t.PaperOrderId == existingOrder.Id, cancellationToken);

                if (existingTrade == null)
                {
                    // Fallback for legacy orders created before PaperOrderId was introduced
                    existingTrade = await _context.PaperTrades
                        .Include(t => t.Symbol)
                        .Where(t => t.PortfolioId == request.PortfolioId && t.SymbolId == existingOrder.SymbolId)
                        .OrderByDescending(t => t.ExecutedAt)
                        .FirstOrDefaultAsync(cancellationToken);
                }

                if (existingTrade != null)
                {
                    _logger.LogInformation("Idempotent order request '{ClientOrderId}' returned exact linked trade {TradeId}.", request.ClientOrderId, existingTrade.Id);
                    return new PaperTradeDto(
                        existingTrade.Id,
                        existingTrade.SymbolId,
                        existingTrade.Symbol.Ticker,
                        existingTrade.Side,
                        existingTrade.Quantity,
                        existingTrade.Price,
                        Math.Round(existingTrade.Quantity * existingTrade.Price, 2),
                        existingTrade.RealizedPnL,
                        existingTrade.Commission,
                        existingTrade.ExecutedAt
                    );
                }
            }
        }

        var portfolio = await _context.PaperPortfolios
            .Include(p => p.Positions)
            .FirstOrDefaultAsync(p => p.Id == request.PortfolioId, cancellationToken);

        if (portfolio == null) throw new ArgumentException("Portfolio not found.");

        var symbol = await _context.Symbols
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Ticker == request.Symbol.ToUpper(), cancellationToken);

        if (symbol == null) throw new ArgumentException($"Symbol '{request.Symbol}' not found.");

        // 2. Market price check and freshness guard (No fallback to 100)
        var latestBar = await _context.PriceBars
            .AsNoTracking()
            .Where(p => p.SymbolId == symbol.Id && p.Timeframe == Timeframe.Daily)
            .OrderByDescending(p => p.Timestamp)
            .FirstOrDefaultAsync(cancellationToken);

        if (latestBar == null)
        {
            throw new InvalidOperationException($"Market price for '{request.Symbol}' is unavailable. Order rejected.");
        }

        if (!_freshnessPolicy.IsFresh(latestBar))
        {
            throw new InvalidOperationException($"Market price for '{request.Symbol}' is stale ({latestBar.Timestamp:yyyy-MM-dd}). Order rejected.");
        }

        // 3. Price determination and limit order condition validation
        decimal marketPrice = latestBar.Close;
        decimal executionPrice;

        if (request.Type == OrderType.Limit)
        {
            if (!request.LimitPrice.HasValue || request.LimitPrice.Value <= 0)
            {
                throw new ArgumentException("Limit orders must specify a positive LimitPrice.");
            }

            decimal limitPrice = request.LimitPrice.Value;

            if (request.Side == OrderSide.Buy && marketPrice > limitPrice)
            {
                throw new InvalidOperationException($"Limit Buy rejected: current market price ({marketPrice:F2}) is higher than limit price ({limitPrice:F2}).");
            }

            if (request.Side == OrderSide.Sell && marketPrice < limitPrice)
            {
                throw new InvalidOperationException($"Limit Sell rejected: current market price ({marketPrice:F2}) is lower than limit price ({limitPrice:F2}).");
            }

            // Limit execution fills at marketPrice when condition is satisfied
            executionPrice = marketPrice;
        }
        else
        {
            executionPrice = marketPrice;
        }

        decimal totalOrderValue = executionPrice * request.Quantity;
        decimal commission = totalOrderValue * CommissionRate;
        decimal realizedPnL = 0;

        if (request.Side == OrderSide.Buy)
        {
            var requiredCash = totalOrderValue + commission;
            if (portfolio.CashBalance < requiredCash)
            {
                throw new InvalidOperationException($"Insufficient cash balance ({portfolio.CashBalance:F2} TRY). Required: {requiredCash:F2} TRY.");
            }

            portfolio.CashBalance -= requiredCash;

            var existingPos = portfolio.Positions.FirstOrDefault(p => p.SymbolId == symbol.Id);
            if (existingPos != null)
            {
                var newQty = existingPos.Quantity + request.Quantity;
                // Entry commission is included in cost basis so it is not lost in realized PnL
                var newCost = (existingPos.Quantity * existingPos.AveragePrice) + totalOrderValue + commission;
                existingPos.AveragePrice = Math.Round(newCost / newQty, 4);
                existingPos.Quantity = newQty;
                existingPos.CurrentPrice = executionPrice;
            }
            else
            {
                portfolio.Positions.Add(new PaperPosition
                {
                    PortfolioId = portfolio.Id,
                    SymbolId = symbol.Id,
                    Quantity = request.Quantity,
                    AveragePrice = Math.Round((totalOrderValue + commission) / request.Quantity, 4),
                    CurrentPrice = executionPrice
                });
            }
        }
        else // Sell
        {
            var existingPos = portfolio.Positions.FirstOrDefault(p => p.SymbolId == symbol.Id);
            if (existingPos == null || existingPos.Quantity < request.Quantity)
            {
                throw new InvalidOperationException($"Insufficient position to sell. Available: {existingPos?.Quantity ?? 0} shares.");
            }

            realizedPnL = (executionPrice - existingPos.AveragePrice) * request.Quantity - commission;
            portfolio.CashBalance += (totalOrderValue - commission);
            existingPos.Quantity -= request.Quantity;
        }

        // 4. Persist PaperOrder
        var paperOrder = new PaperOrder
        {
            PortfolioId = portfolio.Id,
            SymbolId = symbol.Id,
            ClientOrderId = request.ClientOrderId,
            Side = request.Side,
            Type = request.Type,
            Status = OrderStatus.Filled,
            Quantity = request.Quantity,
            LimitPrice = request.LimitPrice,
            FilledPrice = executionPrice,
            FilledAt = DateTime.UtcNow
        };
        _context.PaperOrders.Add(paperOrder);

        // 5. Persist PaperTrade
        var trade = new PaperTrade
        {
            PortfolioId = portfolio.Id,
            SymbolId = symbol.Id,
            PaperOrder = paperOrder,
            Side = request.Side,
            Quantity = request.Quantity,
            Price = executionPrice,
            RealizedPnL = Math.Round(realizedPnL, 2),
            Commission = Math.Round(commission, 2),
            ExecutedAt = DateTime.UtcNow
        };

        _context.PaperTrades.Add(trade);
        await _context.SaveChangesAsync(cancellationToken);

        return new PaperTradeDto(
            trade.Id,
            symbol.Id,
            symbol.Ticker,
            trade.Side,
            trade.Quantity,
            trade.Price,
            Math.Round(totalOrderValue, 2),
            trade.RealizedPnL,
            trade.Commission,
            trade.ExecutedAt
        );
    }

    public async Task AutoTradeScanAsync(long portfolioId, CancellationToken cancellationToken = default)
    {
        var portfolio = await _context.PaperPortfolios
            .Include(p => p.Positions)
            .FirstOrDefaultAsync(p => p.Id == portfolioId, cancellationToken);

        if (portfolio == null || !portfolio.IsAutoTradingEnabled) return;
        if (portfolio.CashBalance <= 0) return;

        var now = DateTime.UtcNow;

        var candidateSignals = await _context.Signals
            .Include(s => s.Symbol)
            .Where(s => s.Score >= portfolio.AutoTradingMinScore 
                     && s.Timeframe == Timeframe.Daily 
                     && s.ExpiresAt > now 
                     && s.Symbol.IsActive)
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync(cancellationToken);

        // Deduplicate: select strictly the latest signal per symbol
        var latestSignalsPerSymbol = candidateSignals
            .GroupBy(s => s.SymbolId)
            .Select(g => g.First())
            .Take(5)
            .ToList();

        foreach (var sig in latestSignalsPerSymbol)
        {
            // Position rule: skip if already holding a position in this symbol
            if (portfolio.Positions.Any(p => p.SymbolId == sig.SymbolId && p.Quantity > 0))
            {
                continue;
            }

            // Market data freshness check
            var latestBar = await _context.PriceBars
                .AsNoTracking()
                .Where(p => p.SymbolId == sig.SymbolId && p.Timeframe == Timeframe.Daily)
                .OrderByDescending(p => p.Timestamp)
                .FirstOrDefaultAsync(cancellationToken);

            if (latestBar == null || !_freshnessPolicy.IsFresh(latestBar))
            {
                _logger.LogWarning("Auto paper trade skipped for {Ticker}: underlying market data is stale.", sig.Symbol.Ticker);
                continue;
            }

            // Sizing based on portfolio equity and allocation limits
            decimal portfolioEquity = portfolio.CashBalance + portfolio.Positions.Sum(p => p.Quantity * p.CurrentPrice);
            decimal maxAllocation = Math.Min(portfolio.CashBalance, portfolioEquity * (portfolio.AutoTradingMaxAllocationPercent / 100.0m));
            if (maxAllocation <= 0 || sig.Price <= 0) continue;

            decimal shares = Math.Floor(maxAllocation / sig.Price);
            if (shares <= 0) continue;

            // Deterministic ClientOrderId: AUTO-{PortfolioId}-{SignalId} to guarantee idempotency across scan cycles
            var clientOrderId = $"AUTO-{portfolio.Id}-{sig.Id}";

            // Idempotency: check if pending or filled order already exists for this clientOrderId
            var existingOrder = await _context.PaperOrders
                .FirstOrDefaultAsync(o => o.PortfolioId == portfolio.Id && o.ClientOrderId == clientOrderId, cancellationToken);

            if (existingOrder != null)
            {
                continue;
            }

            try
            {
                var sigSessionDate = sig.SourceSessionDate ?? (latestBar != null ? DateOnly.FromDateTime(latestBar.Timestamp) : DateOnly.FromDateTime(sig.CreatedAt));
                var targetExecutionDate = _sessionCalendar?.GetNextTradingDay(sigSessionDate) ?? sigSessionDate.AddDays(1);

                // In T+1 EOD forward testing, market orders are submitted for execution at the NEXT trading session's open.
                var pendingOrder = new PaperOrder
                {
                    PortfolioId = portfolio.Id,
                    SymbolId = sig.SymbolId,
                    ClientOrderId = clientOrderId,
                    Side = OrderSide.Buy,
                    Type = OrderType.Market,
                    Status = OrderStatus.PendingNextSessionOpen,
                    Quantity = shares,
                    TargetPrice = sig.TakeProfit1,
                    StopLossPrice = sig.StopLoss,
                    SourceSignalId = sig.Id,
                    SignalSessionDate = sigSessionDate,
                    TargetExecutionSessionDate = targetExecutionDate
                };

                _context.PaperOrders.Add(pendingOrder);
                await _context.SaveChangesAsync(cancellationToken);

                _logger.LogInformation("Auto Paper Trade queued (PendingNextSessionOpen) for {Ticker} ({Shares} shares, SignalSession: {SignalSession}, TargetSession: {TargetSession}, ClientOrderId: {ClientOrderId})",
                    sig.Symbol.Ticker, shares, sigSessionDate, targetExecutionDate, clientOrderId);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Auto paper trade queueing failed for {Ticker} (ClientOrderId: {ClientOrderId})", sig.Symbol.Ticker, clientOrderId);
            }
        }
    }

    public async Task<int> ExecutePendingOrdersForSessionAsync(DateOnly sessionDate, CancellationToken cancellationToken = default)
    {
        // Strictly execute only orders targeted for this specific session date
        var pendingOrders = await _context.PaperOrders
            .Include(o => o.Portfolio)
            .ThenInclude(p => p.Positions)
            .Include(o => o.Symbol)
            .Where(o => o.Status == OrderStatus.PendingNextSessionOpen && o.TargetExecutionSessionDate == sessionDate)
            .ToListAsync(cancellationToken);

        if (!pendingOrders.Any()) return 0;

        int filledCount = 0;
        var barTimestampUtc = sessionDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var marketOpenTime = _sessionCalendar?.GetMarketOpenTime(sessionDate) ?? new TimeSpan(10, 0, 0);
        var fillTimestampUtc = sessionDate.ToDateTime(TimeOnly.FromTimeSpan(marketOpenTime), DateTimeKind.Utc);

        foreach (var order in pendingOrders)
        {
            var bar = await _context.PriceBars
                .AsNoTracking()
                .FirstOrDefaultAsync(b => b.SymbolId == order.SymbolId && b.Timeframe == Timeframe.Daily && b.Timestamp == barTimestampUtc, cancellationToken);

            var stats = await _context.DailyInstrumentMarketStats
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.SymbolId == order.SymbolId && s.SessionDate == sessionDate, cancellationToken);

            if (stats?.Suspended == true || bar == null || bar.Open <= 0)
            {
                _logger.LogInformation("Order {OrderId} for {Ticker} cannot fill on target session {SessionDate}: suspended or no valid open price. Order expired (no T+2 carry).",
                    order.Id, order.Symbol.Ticker, sessionDate);
                order.Status = OrderStatus.Expired;
                order.CancellationReason = "No valid opening price on target execution session.";
                continue;
            }

            decimal fillPrice = bar.Open;
            decimal totalCost = (order.Quantity * fillPrice) + Math.Round(order.Quantity * fillPrice * CommissionRate, 2);

            if (order.Portfolio.CashBalance < totalCost)
            {
                _logger.LogWarning("Order {OrderId} cancelled due to insufficient cash balance (Required: {Cost}, Available: {Balance}).",
                    order.Id, totalCost, order.Portfolio.CashBalance);
                order.Status = OrderStatus.Cancelled;
                order.CancellationReason = $"Insufficient cash balance (Required: {totalCost:N2}, Available: {order.Portfolio.CashBalance:N2}).";
                continue;
            }

            // Deduct cash
            order.Portfolio.CashBalance -= totalCost;

            // Upsert position
            var position = order.Portfolio.Positions.FirstOrDefault(p => p.SymbolId == order.SymbolId);
            if (position == null)
            {
                position = new PaperPosition
                {
                    PortfolioId = order.PortfolioId,
                    SymbolId = order.SymbolId,
                    Quantity = order.Quantity,
                    AveragePrice = fillPrice,
                    CurrentPrice = bar.Close
                };
                _context.PaperPositions.Add(position);
                order.Portfolio.Positions.Add(position);
            }
            else
            {
                var oldQty = position.Quantity;
                var newQty = oldQty + order.Quantity;
                position.AveragePrice = Math.Round(((oldQty * position.AveragePrice) + (order.Quantity * fillPrice)) / newQty, 4);
                position.Quantity = newQty;
                position.CurrentPrice = bar.Close;
            }

            // Create trade
            var trade = new PaperTrade
            {
                PortfolioId = order.PortfolioId,
                SymbolId = order.SymbolId,
                PaperOrderId = order.Id,
                Side = order.Side,
                Quantity = order.Quantity,
                Price = fillPrice,
                RealizedPnL = 0,
                Commission = Math.Round(order.Quantity * fillPrice * CommissionRate, 2),
                ExecutedAt = fillTimestampUtc
            };
            _context.PaperTrades.Add(trade);

            order.Status = OrderStatus.Filled;
            order.FilledPrice = fillPrice;
            order.FilledAt = fillTimestampUtc;
            order.ExecutedSessionDate = sessionDate;
            filledCount++;

            _logger.LogInformation("Filled pending T+1 paper order {OrderId} for {Ticker} ({Shares} shares at official OPEN {Price} TL on session {SessionDate})",
                order.Id, order.Symbol.Ticker, order.Quantity, fillPrice, sessionDate);
        }

        // Also update current prices for all open positions of active portfolios to session close
        var activePositions = await _context.PaperPositions
            .Include(p => p.Portfolio)
            .Where(p => p.Quantity > 0)
            .ToListAsync(cancellationToken);

        foreach (var pos in activePositions)
        {
            var bar = await _context.PriceBars
                .AsNoTracking()
                .FirstOrDefaultAsync(b => b.SymbolId == pos.SymbolId && b.Timeframe == Timeframe.Daily && b.Timestamp == barTimestampUtc, cancellationToken);

            if (bar != null && bar.Close > 0)
            {
                pos.CurrentPrice = bar.Close;
            }
        }

        await _context.SaveChangesAsync(cancellationToken);
        return filledCount;
    }
}
