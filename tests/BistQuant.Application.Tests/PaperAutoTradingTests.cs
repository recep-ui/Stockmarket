using BistQuant.Application.Common.Interfaces;
using BistQuant.Application.DTOs.PaperTrading;
using BistQuant.Application.Services;
using BistQuant.Application.Services.MarketData;
using BistQuant.Domain.Entities;
using BistQuant.Domain.Enums;
using BistQuant.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace BistQuant.Application.Tests;

public class PaperAutoTradingTests
{
    private static BistQuantDbContext CreateDbContext()
    {
        var connection = new Microsoft.Data.Sqlite.SqliteConnection("DataSource=:memory:");
        connection.Open();
        var options = new DbContextOptionsBuilder<BistQuantDbContext>()
            .UseSqlite(connection)
            .Options;
        var context = new BistQuantDbContext(options);
        context.Database.EnsureCreated();
        context.Markets.Add(new Market { Id = 1, Code = "BIST", Name = "Borsa Istanbul", Country = "Turkey", Currency = "TRY", Timezone = "Europe/Istanbul" });
        context.SaveChanges();
        return context;
    }

    private static IMarketDataFreshnessPolicy CreateFreshnessPolicy()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection().Build();
        return new MarketDataFreshnessPolicy(config);
    }

    [Fact]
    public async Task AutoTradeScan_ExecutesOrder_WithDeterministicClientOrderId_AndPreventsDuplicates()
    {
        using var context = CreateDbContext();
        var freshnessPolicy = CreateFreshnessPolicy();
        var mockSignalEngine = new Mock<ISignalEngine>();

        var user = new User { Email = "trader@bistquant.com", DisplayName = "Trader" };
        var symbol = new Symbol { Ticker = "THYAO", Name = "Turk Hava Yollari", MarketId = 1, IsActive = true };
        context.Users.Add(user);
        context.Symbols.Add(symbol);
        await context.SaveChangesAsync();

        var portfolio = new PaperPortfolio
        {
            UserId = user.Id,
            Name = "Auto Portfolio",
            InitialBalance = 100000m,
            CashBalance = 100000m,
            IsAutoTradingEnabled = true,
            AutoTradingMinScore = 75,
            AutoTradingMaxAllocationPercent = 10.0m
        };
        context.PaperPortfolios.Add(portfolio);
        await context.SaveChangesAsync();

        // Fresh PriceBar
        var bar = new PriceBar
        {
            SymbolId = symbol.Id,
            Timeframe = Timeframe.Daily,
            Timestamp = DateTime.UtcNow.Date,
            Close = 200m,
            Open = 198m,
            High = 202m,
            Low = 197m,
            Volume = 1000000m
        };
        context.PriceBars.Add(bar);

        // Fresh Signal
        var signal = new Signal
        {
            SymbolId = symbol.Id,
            Timeframe = Timeframe.Daily,
            SignalType = SignalType.Buy,
            Score = 85,
            Price = 200m,
            StopLoss = 190m,
            TakeProfit1 = 215m,
            TakeProfit2 = 225m,
            RiskRewardRatio = 1.5m,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddDays(1) // Non-expired
        };
        context.Signals.Add(signal);
        await context.SaveChangesAsync();

        var paperService = new PaperTradingService(context, mockSignalEngine.Object, freshnessPolicy, NullLogger<PaperTradingService>.Instance);

        // Act 1: Initial auto trade scan (session T close) -> queues PendingNextSessionOpen order
        await paperService.AutoTradeScanAsync(portfolio.Id);

        // Assert 1: Order is created with PendingNextSessionOpen status and deterministic ClientOrderId
        var orders = await context.PaperOrders.Where(o => o.PortfolioId == portfolio.Id).ToListAsync();
        Assert.Single(orders);
        Assert.Equal(OrderStatus.PendingNextSessionOpen, orders[0].Status);
        Assert.Equal($"AUTO-{portfolio.Id}-{signal.Id}", orders[0].ClientOrderId);

        // Act 2: Run scan again (simulating repeated scanner cycle) -> Idempotency check
        await paperService.AutoTradeScanAsync(portfolio.Id);

        // Assert 2: Still strictly 1 order (no duplicate pending order!)
        var ordersAfter = await context.PaperOrders.Where(o => o.PortfolioId == portfolio.Id).ToListAsync();
        Assert.Single(ordersAfter);

        // Act 3: Next trading session T+1 arrives. New PriceBar with official OPEN price = 205m
        var nextSessionDate = DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(1));
        var nextBar = new PriceBar
        {
            SymbolId = symbol.Id,
            Timeframe = Timeframe.Daily,
            Timestamp = nextSessionDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
            Open = 205m,
            High = 210m,
            Low = 204m,
            Close = 208m,
            Volume = 1200000m
        };
        context.PriceBars.Add(nextBar);
        await context.SaveChangesAsync();

        int filled = await paperService.ExecutePendingOrdersForSessionAsync(nextSessionDate);

        // Assert 3: Order filled at official OPEN price 205m
        Assert.Equal(1, filled);
        var trades = await context.PaperTrades.Where(t => t.PortfolioId == portfolio.Id).ToListAsync();
        Assert.Single(trades);
        Assert.Equal(205m, trades[0].Price);

        var filledOrder = await context.PaperOrders.FirstAsync(o => o.Id == orders[0].Id);
        Assert.Equal(OrderStatus.Filled, filledOrder.Status);
        Assert.Equal(205m, filledOrder.FilledPrice);
    }

    [Fact]
    public async Task AutoTradeScan_RejectsExpiredSignal()
    {
        using var context = CreateDbContext();
        var freshnessPolicy = CreateFreshnessPolicy();
        var mockSignalEngine = new Mock<ISignalEngine>();

        var user = new User { Email = "trader2@bistquant.com", DisplayName = "Trader 2" };
        var symbol = new Symbol { Ticker = "ASELS", Name = "Aselsan", MarketId = 1, IsActive = true };
        context.Users.Add(user);
        context.Symbols.Add(symbol);
        await context.SaveChangesAsync();

        var portfolio = new PaperPortfolio
        {
            UserId = user.Id,
            Name = "Auto Portfolio",
            CashBalance = 100000m,
            IsAutoTradingEnabled = true,
            AutoTradingMinScore = 75,
            AutoTradingMaxAllocationPercent = 10.0m
        };
        context.PaperPortfolios.Add(portfolio);

        context.PriceBars.Add(new PriceBar
        {
            SymbolId = symbol.Id,
            Timeframe = Timeframe.Daily,
            Timestamp = DateTime.UtcNow.Date,
            Close = 50m
        });

        // Expired signal
        context.Signals.Add(new Signal
        {
            SymbolId = symbol.Id,
            Timeframe = Timeframe.Daily,
            SignalType = SignalType.StrongBuy,
            Score = 95,
            Price = 50m,
            StopLoss = 45m,
            CreatedAt = DateTime.UtcNow.AddDays(-2),
            ExpiresAt = DateTime.UtcNow.AddMinutes(-30) // Expired!
        });
        await context.SaveChangesAsync();

        var paperService = new PaperTradingService(context, mockSignalEngine.Object, freshnessPolicy, NullLogger<PaperTradingService>.Instance);

        await paperService.AutoTradeScanAsync(portfolio.Id);

        var trades = await context.PaperTrades.Where(t => t.PortfolioId == portfolio.Id).ToListAsync();
        Assert.Empty(trades);
    }

    [Fact]
    public async Task AutoTradeScan_RejectsInactiveSymbol()
    {
        using var context = CreateDbContext();
        var freshnessPolicy = CreateFreshnessPolicy();
        var mockSignalEngine = new Mock<ISignalEngine>();

        var user = new User { Email = "trader3@bistquant.com", DisplayName = "Trader 3" };
        var inactiveSymbol = new Symbol { Ticker = "DELISTED", Name = "Delisted Stock", MarketId = 1, IsActive = false };
        context.Users.Add(user);
        context.Symbols.Add(inactiveSymbol);
        await context.SaveChangesAsync();

        var portfolio = new PaperPortfolio
        {
            UserId = user.Id,
            Name = "Auto Portfolio",
            CashBalance = 100000m,
            IsAutoTradingEnabled = true,
            AutoTradingMinScore = 75,
            AutoTradingMaxAllocationPercent = 10.0m
        };
        context.PaperPortfolios.Add(portfolio);

        context.PriceBars.Add(new PriceBar
        {
            SymbolId = inactiveSymbol.Id,
            Timeframe = Timeframe.Daily,
            Timestamp = DateTime.UtcNow.Date,
            Close = 10m
        });

        context.Signals.Add(new Signal
        {
            SymbolId = inactiveSymbol.Id,
            Timeframe = Timeframe.Daily,
            SignalType = SignalType.StrongBuy,
            Score = 90,
            Price = 10m,
            StopLoss = 9m,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddHours(2)
        });
        await context.SaveChangesAsync();

        var paperService = new PaperTradingService(context, mockSignalEngine.Object, freshnessPolicy, NullLogger<PaperTradingService>.Instance);

        await paperService.AutoTradeScanAsync(portfolio.Id);

        var trades = await context.PaperTrades.Where(t => t.PortfolioId == portfolio.Id).ToListAsync();
        Assert.Empty(trades);
    }

    [Fact]
    public async Task TPlus1_Execution_FillsAt_ExactSessionOpenUtc_0700_AndPreventsDoubleFill()
    {
        using var context = CreateDbContext();
        var freshnessPolicy = CreateFreshnessPolicy();
        var mockSignalEngine = new Mock<ISignalEngine>();
        var calendar = new BistMarketSessionCalendar();

        var user = new User { Email = "tplus1@bistquant.com", DisplayName = "T+1 Trader" };
        var symbol = new Symbol { Ticker = "THYAO", Name = "Turk Hava Yollari", MarketId = 1, IsActive = true };
        context.Users.Add(user);
        context.Symbols.Add(symbol);
        await context.SaveChangesAsync();

        var portfolio = new PaperPortfolio
        {
            UserId = user.Id,
            Name = "T+1 Portfolio",
            InitialBalance = 100000m,
            CashBalance = 100000m,
            IsAutoTradingEnabled = true
        };
        context.PaperPortfolios.Add(portfolio);
        await context.SaveChangesAsync();

        var targetSession = new DateOnly(2026, 9, 8); // Tuesday
        var barTimestampUtc = targetSession.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);

        // Add market stats and price bar for target session
        context.DailyInstrumentMarketStats.Add(new DailyInstrumentMarketStats
        {
            SymbolId = symbol.Id,
            SessionDate = targetSession,
            Suspended = false,
            ClosingSessionPrice = 325.0m
        });

        context.PriceBars.Add(new PriceBar
        {
            SymbolId = symbol.Id,
            Timeframe = Timeframe.Daily,
            Timestamp = barTimestampUtc,
            Open = 322.0m, // Target session Open
            High = 328.0m,
            Low = 320.0m,
            Close = 325.0m,
            Volume = 10000000m
        });

        var pendingOrder = new PaperOrder
        {
            PortfolioId = portfolio.Id,
            SymbolId = symbol.Id,
            ClientOrderId = "TEST_ORDER_001",
            Side = OrderSide.Buy,
            Type = OrderType.Market,
            Status = OrderStatus.PendingNextSessionOpen,
            Quantity = 100,
            SignalSessionDate = new DateOnly(2026, 9, 7),
            TargetExecutionSessionDate = targetSession
        };
        context.PaperOrders.Add(pendingOrder);
        await context.SaveChangesAsync();

        var paperService = new PaperTradingService(context, mockSignalEngine.Object, freshnessPolicy, NullLogger<PaperTradingService>.Instance, calendar);

        // Execute orders for target session
        int filledCount = await paperService.ExecutePendingOrdersForSessionAsync(targetSession);
        Assert.Equal(1, filledCount);

        var filledOrder = await context.PaperOrders.FindAsync(pendingOrder.Id);
        Assert.NotNull(filledOrder);
        Assert.Equal(OrderStatus.Filled, filledOrder.Status);
        Assert.Equal(322.0m, filledOrder.FilledPrice);
        Assert.Equal(targetSession, filledOrder.ExecutedSessionDate);

        // Financial UTC timestamp verification: 10:00 Istanbul must equal 07:00 UTC
        Assert.NotNull(filledOrder.FilledAt);
        Assert.Equal(DateTimeKind.Utc, filledOrder.FilledAt.Value.Kind);
        Assert.Equal(new DateTime(2026, 9, 8, 7, 0, 0, DateTimeKind.Utc), filledOrder.FilledAt.Value);

        var trade = await context.PaperTrades.FirstOrDefaultAsync(t => t.PaperOrderId == filledOrder.Id);
        Assert.NotNull(trade);
        Assert.Equal(322.0m, trade.Price);
        Assert.Equal(new DateTime(2026, 9, 8, 7, 0, 0, DateTimeKind.Utc), trade.ExecutedAt);

        // Repeated execution must NOT double fill
        int secondExecutionCount = await paperService.ExecutePendingOrdersForSessionAsync(targetSession);
        Assert.Equal(0, secondExecutionCount);
    }

    [Fact]
    public async Task TPlus1_SuspendedStock_ExpiresWithoutTPlus2Carry()
    {
        using var context = CreateDbContext();
        var freshnessPolicy = CreateFreshnessPolicy();
        var mockSignalEngine = new Mock<ISignalEngine>();
        var calendar = new BistMarketSessionCalendar();

        var user = new User { Email = "susp@bistquant.com", DisplayName = "Susp Trader" };
        var symbol = new Symbol { Ticker = "SUSPD", Name = "Suspended Corp", MarketId = 1, IsActive = true };
        context.Users.Add(user);
        context.Symbols.Add(symbol);
        await context.SaveChangesAsync();

        var portfolio = new PaperPortfolio
        {
            UserId = user.Id,
            Name = "Susp Portfolio",
            CashBalance = 50000m
        };
        context.PaperPortfolios.Add(portfolio);
        await context.SaveChangesAsync();

        var targetSession = new DateOnly(2026, 9, 8);
        context.DailyInstrumentMarketStats.Add(new DailyInstrumentMarketStats
        {
            SymbolId = symbol.Id,
            SessionDate = targetSession,
            Suspended = true
        });

        var pendingOrder = new PaperOrder
        {
            PortfolioId = portfolio.Id,
            SymbolId = symbol.Id,
            ClientOrderId = "TEST_SUSP_001",
            Side = OrderSide.Buy,
            Type = OrderType.Market,
            Status = OrderStatus.PendingNextSessionOpen,
            Quantity = 100,
            TargetExecutionSessionDate = targetSession
        };
        context.PaperOrders.Add(pendingOrder);
        await context.SaveChangesAsync();

        var paperService = new PaperTradingService(context, mockSignalEngine.Object, freshnessPolicy, NullLogger<PaperTradingService>.Instance, calendar);
        int filled = await paperService.ExecutePendingOrdersForSessionAsync(targetSession);

        Assert.Equal(0, filled);
        var updated = await context.PaperOrders.FindAsync(pendingOrder.Id);
        Assert.NotNull(updated);
        Assert.Equal(OrderStatus.Expired, updated.Status);
        Assert.NotNull(updated.CancellationReason);
        Assert.Contains("suspended", updated.CancellationReason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TPlus1_ZeroOrMissingOpen_ExpiresWithoutTPlus2Carry()
    {
        using var context = CreateDbContext();
        var freshnessPolicy = CreateFreshnessPolicy();
        var mockSignalEngine = new Mock<ISignalEngine>();
        var calendar = new BistMarketSessionCalendar();

        var user = new User { Email = "noopen@bistquant.com", DisplayName = "NoOpen Trader" };
        var symbol = new Symbol { Ticker = "NOOPN", Name = "No Open Corp", MarketId = 1, IsActive = true };
        context.Users.Add(user);
        context.Symbols.Add(symbol);
        await context.SaveChangesAsync();

        var portfolio = new PaperPortfolio
        {
            UserId = user.Id,
            Name = "NoOpen Portfolio",
            CashBalance = 50000m
        };
        context.PaperPortfolios.Add(portfolio);
        await context.SaveChangesAsync();

        var targetSession = new DateOnly(2026, 9, 8);
        context.DailyInstrumentMarketStats.Add(new DailyInstrumentMarketStats
        {
            SymbolId = symbol.Id,
            SessionDate = targetSession,
            Suspended = false
        });

        // PriceBar has Open = 0
        context.PriceBars.Add(new PriceBar
        {
            SymbolId = symbol.Id,
            Timeframe = Timeframe.Daily,
            Timestamp = targetSession.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
            Open = 0m,
            High = 0m,
            Low = 0m,
            Close = 0m
        });

        var pendingOrder = new PaperOrder
        {
            PortfolioId = portfolio.Id,
            SymbolId = symbol.Id,
            ClientOrderId = "TEST_NOOPN_001",
            Side = OrderSide.Buy,
            Type = OrderType.Market,
            Status = OrderStatus.PendingNextSessionOpen,
            Quantity = 100,
            TargetExecutionSessionDate = targetSession
        };
        context.PaperOrders.Add(pendingOrder);
        await context.SaveChangesAsync();

        var paperService = new PaperTradingService(context, mockSignalEngine.Object, freshnessPolicy, NullLogger<PaperTradingService>.Instance, calendar);
        int filled = await paperService.ExecutePendingOrdersForSessionAsync(targetSession);

        Assert.Equal(0, filled);
        var updated = await context.PaperOrders.FindAsync(pendingOrder.Id);
        Assert.NotNull(updated);
        Assert.Equal(OrderStatus.Expired, updated.Status);
    }
}
