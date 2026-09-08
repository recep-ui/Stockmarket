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
}
