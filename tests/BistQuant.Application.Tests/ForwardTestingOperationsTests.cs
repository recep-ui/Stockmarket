using BistQuant.Application.Common.Interfaces;
using BistQuant.Application.DTOs.ForwardTesting;
using BistQuant.Application.Interfaces;
using BistQuant.Application.Services;
using BistQuant.Application.Services.MarketData;
using BistQuant.Domain.Entities;
using BistQuant.Domain.Enums;
using BistQuant.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace BistQuant.Application.Tests;

public class ForwardTestingOperationsTests
{
    private static BistQuantDbContext CreateDbContext()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
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

    [Fact]
    public async Task AutoTradeScan_UniverseCoverageUnder95Percent_BlocksOrderCreation()
    {
        using var context = CreateDbContext();
        var user = new User { Email = "ft@bistquant.com", DisplayName = "FT User" };
        var sym1 = new Symbol { Id = 1, Ticker = "THYAO", Name = "THY", MarketId = 1, IsActive = true };
        var sym2 = new Symbol { Id = 2, Ticker = "GARAN", Name = "Garanti", MarketId = 1, IsActive = true };
        context.Users.Add(user);
        context.Symbols.AddRange(sym1, sym2);
        await context.SaveChangesAsync();

        var sessionDate = new DateOnly(2026, 3, 2);
        var sessionTs = sessionDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);

        // 2 active symbols, but only 1 bar in session -> 50% coverage (< 95%)
        context.PriceBars.Add(new PriceBar
        {
            SymbolId = 1,
            Timeframe = Timeframe.Daily,
            Timestamp = sessionTs,
            Open = 250, High = 255, Low = 248, Close = 254, Volume = 5000000
        });

        // Market data import for session marked current & success
        context.MarketDataImports.Add(new MarketDataImport
        {
            Provider = "Borsa İstanbul Pay Piyasası Günlük Bülten",
            SessionDate = sessionDate,
            Status = MarketDataImportStatus.Success,
            IsCurrent = true
        });

        var portfolio = new PaperPortfolio
        {
            UserId = user.Id,
            Name = "FT Portfolio",
            InitialBalance = 100000m,
            CashBalance = 100000m,
            IsAutoTradingEnabled = true,
            IsForwardTest = true,
            AutoTradingMinScore = 75,
            AutoTradingMaxAllocationPercent = 10.0m
        };
        context.PaperPortfolios.Add(portfolio);

        // Signal for THYAO
        context.Signals.Add(new Signal
        {
            SymbolId = 1,
            Timeframe = Timeframe.Daily,
            SignalType = SignalType.StrongBuy,
            Score = 85,
            Price = 254,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddDays(1),
            SourceSessionDate = sessionDate
        });
        await context.SaveChangesAsync();

        var mockSignalEngine = new Mock<ISignalEngine>();
        var mockCalendar = new Mock<IMarketSessionCalendar>();
        mockCalendar.Setup(c => c.GetNextTradingDay(It.IsAny<DateOnly>())).Returns(sessionDate.AddDays(1));

        var freshnessPolicy = new MarketDataFreshnessPolicy(new ConfigurationBuilder().Build());
        var paperService = new PaperTradingService(context, mockSignalEngine.Object, freshnessPolicy, NullLogger<PaperTradingService>.Instance, mockCalendar.Object);

        await paperService.AutoTradeScanAsync(portfolio.Id);

        // Assert: 0 orders queued due to coverage gate
        var ordersCount = await context.PaperOrders.CountAsync(o => o.PortfolioId == portfolio.Id);
        Assert.Equal(0, ordersCount);
    }

    [Fact]
    public async Task AutoTradeScan_CorporateActionWarning_BlocksSymbolTrading()
    {
        using var context = CreateDbContext();
        var user = new User { Email = "ft2@bistquant.com", DisplayName = "FT User 2" };
        var sym1 = new Symbol { Id = 1, Ticker = "THYAO", Name = "THY", MarketId = 1, IsActive = true };
        context.Users.Add(user);
        context.Symbols.Add(sym1);
        await context.SaveChangesAsync();

        var sessionDate = new DateOnly(2026, 3, 2);
        var sessionTs = sessionDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);

        // Add 250 bars so history gate passes
        for (int i = 0; i < 250; i++)
        {
            context.PriceBars.Add(new PriceBar
            {
                SymbolId = 1,
                Timeframe = Timeframe.Daily,
                Timestamp = sessionTs.AddDays(-250 + i),
                Open = 250, High = 255, Low = 248, Close = 254, Volume = 5000000
            });
        }

        context.MarketDataImports.Add(new MarketDataImport
        {
            Provider = "Borsa İstanbul Pay Piyasası Günlük Bülten",
            SessionDate = sessionDate,
            Status = MarketDataImportStatus.Success,
            IsCurrent = true
        });

        // Add UNACKNOWLEDGED corporate action warning
        context.IndicatorContinuityWarnings.Add(new IndicatorContinuityWarning
        {
            SymbolId = 1,
            SessionDate = sessionDate,
            CorporateActionRaw = "DIVIDEND",
            IsAcknowledged = false,
            WarningMessage = "Dividend discontinuity"
        });

        var portfolio = new PaperPortfolio
        {
            UserId = user.Id,
            Name = "FT Portfolio",
            InitialBalance = 100000m,
            CashBalance = 100000m,
            IsAutoTradingEnabled = true,
            IsForwardTest = true,
            AutoTradingMinScore = 75,
            AutoTradingMaxAllocationPercent = 10.0m
        };
        context.PaperPortfolios.Add(portfolio);

        context.Signals.Add(new Signal
        {
            SymbolId = 1,
            Timeframe = Timeframe.Daily,
            SignalType = SignalType.StrongBuy,
            Score = 85,
            Price = 254,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddDays(1),
            SourceSessionDate = sessionDate
        });
        await context.SaveChangesAsync();

        var mockSignalEngine = new Mock<ISignalEngine>();
        var mockCalendar = new Mock<IMarketSessionCalendar>();
        var freshnessPolicy = new MarketDataFreshnessPolicy(new ConfigurationBuilder().Build());
        var paperService = new PaperTradingService(context, mockSignalEngine.Object, freshnessPolicy, NullLogger<PaperTradingService>.Instance, mockCalendar.Object);

        await paperService.AutoTradeScanAsync(portfolio.Id);

        var ordersCount = await context.PaperOrders.CountAsync(o => o.PortfolioId == portfolio.Id);
        Assert.Equal(0, ordersCount);
    }

    [Fact]
    public async Task AutoTradeScan_LiquidityFilter_BlocksLowVolumeOrLowValue()
    {
        using var context = CreateDbContext();
        var user = new User { Email = "ft3@bistquant.com", DisplayName = "FT User 3" };
        var sym1 = new Symbol { Id = 1, Ticker = "PENNY", Name = "Penny Stock", MarketId = 1, IsActive = true };
        context.Users.Add(user);
        context.Symbols.Add(sym1);
        await context.SaveChangesAsync();

        var sessionDate = new DateOnly(2026, 3, 2);
        var sessionTs = sessionDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);

        for (int i = 0; i < 250; i++)
        {
            context.PriceBars.Add(new PriceBar
            {
                SymbolId = 1,
                Timeframe = Timeframe.Daily,
                Timestamp = sessionTs.AddDays(-250 + i),
                Open = 10, High = 10, Low = 10, Close = 10,
                Volume = 50000 // 50k shares (< 100k shares)
            });
        }

        context.MarketDataImports.Add(new MarketDataImport
        {
            Provider = "Borsa İstanbul Pay Piyasası Günlük Bülten",
            SessionDate = sessionDate,
            Status = MarketDataImportStatus.Success,
            IsCurrent = true
        });

        var portfolio = new PaperPortfolio
        {
            UserId = user.Id,
            Name = "FT Portfolio",
            InitialBalance = 100000m,
            CashBalance = 100000m,
            IsAutoTradingEnabled = true,
            IsForwardTest = true,
            AutoTradingMinScore = 75,
            AutoTradingMaxAllocationPercent = 10.0m
        };
        context.PaperPortfolios.Add(portfolio);

        context.Signals.Add(new Signal
        {
            SymbolId = 1,
            Timeframe = Timeframe.Daily,
            SignalType = SignalType.StrongBuy,
            Score = 85,
            Price = 10,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddDays(1),
            SourceSessionDate = sessionDate
        });
        await context.SaveChangesAsync();

        var mockSignalEngine = new Mock<ISignalEngine>();
        var mockCalendar = new Mock<IMarketSessionCalendar>();
        var freshnessPolicy = new MarketDataFreshnessPolicy(new ConfigurationBuilder().Build());
        var paperService = new PaperTradingService(context, mockSignalEngine.Object, freshnessPolicy, NullLogger<PaperTradingService>.Instance, mockCalendar.Object);

        await paperService.AutoTradeScanAsync(portfolio.Id);

        var ordersCount = await context.PaperOrders.CountAsync(o => o.PortfolioId == portfolio.Id);
        Assert.Equal(0, ordersCount);
    }

    [Fact]
    public async Task PaperTrading_Traceability_And_Execution_PreservesSourceSignalId()
    {
        using var context = CreateDbContext();
        var user = new User { Email = "ft4@bistquant.com", DisplayName = "FT User 4" };
        var sym1 = new Symbol { Id = 1, Ticker = "THYAO", Name = "THY", MarketId = 1, IsActive = true };
        context.Users.Add(user);
        context.Symbols.Add(sym1);
        await context.SaveChangesAsync();

        var portfolio = new PaperPortfolio
        {
            UserId = user.Id,
            Name = "FT Portfolio",
            InitialBalance = 100000m,
            CashBalance = 100000m,
            IsAutoTradingEnabled = true,
            IsForwardTest = true
        };
        context.PaperPortfolios.Add(portfolio);
        await context.SaveChangesAsync();

        var sessionDate = new DateOnly(2026, 3, 3);
        var sessionTs = sessionDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);

        var signal = new Signal
        {
            Id = 99,
            SymbolId = sym1.Id,
            Timeframe = Timeframe.Daily,
            SignalType = SignalType.Buy,
            Score = 80,
            Price = 250m,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddDays(1)
        };
        context.Signals.Add(signal);

        var order = new PaperOrder
        {
            Portfolio = portfolio,
            Symbol = sym1,
            ClientOrderId = "AUTO-1-99",
            Side = OrderSide.Buy,
            Type = OrderType.Market,
            Status = OrderStatus.PendingNextSessionOpen,
            Quantity = 100,
            SourceSignalId = 99,
            SignalSessionDate = sessionDate.AddDays(-1),
            TargetExecutionSessionDate = sessionDate
        };
        context.PaperOrders.Add(order);

        // Price bar for session execution with open = 250
        context.PriceBars.Add(new PriceBar
        {
            SymbolId = 1,
            Timeframe = Timeframe.Daily,
            Timestamp = sessionTs,
            Open = 250, High = 255, Low = 249, Close = 253, Volume = 2000000
        });

        await context.SaveChangesAsync();

        var mockCalendar = new Mock<IMarketSessionCalendar>();
        mockCalendar.Setup(c => c.GetSessionOpenUtc(sessionDate)).Returns(sessionTs.AddHours(7));

        var freshnessPolicy = new MarketDataFreshnessPolicy(new ConfigurationBuilder().Build());
        var paperService = new PaperTradingService(context, new Mock<ISignalEngine>().Object, freshnessPolicy, NullLogger<PaperTradingService>.Instance, mockCalendar.Object);

        var filled = await paperService.ExecutePendingOrdersForSessionAsync(sessionDate);

        Assert.Equal(1, filled);
        var trade = await context.PaperTrades.FirstOrDefaultAsync(t => t.PaperOrderId == order.Id);
        Assert.NotNull(trade);
        Assert.Equal(99, trade.SourceSignalId); // Signal traceability verified!
        Assert.Equal(250m, trade.Price);
        Assert.Equal(100m, trade.Quantity);
    }

    [Fact]
    public async Task ForwardTestPerformanceService_GeneratesReport_AndCalculatesMetrics()
    {
        using var context = CreateDbContext();
        var user = new User { Email = "ft5@bistquant.com", DisplayName = "FT User 5" };
        var sym = new Symbol { Id = 1, Ticker = "ASELS", Name = "Aselsan", MarketId = 1, IsActive = true };
        context.Users.Add(user);
        context.Symbols.Add(sym);
        await context.SaveChangesAsync();

        var portfolio = new PaperPortfolio
        {
            UserId = user.Id,
            Name = "Performance Portfolio",
            InitialBalance = 100000m,
            CashBalance = 80000m,
            IsForwardTest = true,
            ForwardTestStartDate = new DateOnly(2026, 1, 1)
        };
        context.PaperPortfolios.Add(portfolio);
        await context.SaveChangesAsync();

        var pos = new PaperPosition
        {
            Portfolio = portfolio,
            Symbol = sym,
            Quantity = 100,
            AveragePrice = 200m,
            CurrentPrice = 250m // Unrealized PnL = +5000 TL
        };
        context.PaperPositions.Add(pos);

        var order = new PaperOrder
        {
            Portfolio = portfolio,
            Symbol = sym,
            ClientOrderId = "ORD-TEST-1",
            Side = OrderSide.Sell,
            Type = OrderType.Market,
            Status = OrderStatus.Filled,
            Quantity = 50,
            FilledPrice = 260m,
            FilledAt = new DateTime(2026, 3, 2, 12, 0, 0, DateTimeKind.Utc)
        };
        context.PaperOrders.Add(order);

        // Add a closed trade with realized profit
        var trade = new PaperTrade
        {
            Portfolio = portfolio,
            Symbol = sym,
            PaperOrder = order,
            Side = OrderSide.Sell,
            Quantity = 50,
            Price = 260m,
            RealizedPnL = 3000m,
            Commission = 20m,
            ExecutedAt = new DateTime(2026, 3, 2, 12, 0, 0, DateTimeKind.Utc)
        };
        context.PaperTrades.Add(trade);
        await context.SaveChangesAsync();

        var config = new ConfigurationBuilder().Build();
        var service = new ForwardTestPerformanceService(
            context,
            config,
            new List<INotificationProvider>(),
            NullLogger<ForwardTestPerformanceService>.Instance
        );

        var perf = await service.GetPerformanceAsync(portfolio.Id);

        Assert.NotNull(perf);
        Assert.Equal(100000m, perf.Metrics.InitialCapital);
        Assert.Equal(80000m + 25000m, perf.Metrics.Equity); // 80k cash + 25k position = 105,000 TL
        Assert.Equal(5.0m, perf.Metrics.TotalReturnPercent); // +5.0% return
        Assert.Equal(1, perf.Metrics.TotalTrades);
        Assert.Equal(1, perf.Metrics.WinningTrades);
        Assert.Equal(100m, perf.Metrics.WinRatePercent);

        // Daily report generation
        var report = await service.GenerateDailyReportAsync(portfolio.Id, new DateOnly(2026, 3, 2), 500, 1);
        Assert.NotNull(report);
        Assert.Equal(105000m, report.PortfolioEquity);
        Assert.Equal(1, report.OrdersFilled);
    }
}
