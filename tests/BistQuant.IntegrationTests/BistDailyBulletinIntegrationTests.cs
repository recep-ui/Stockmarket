using System.Text;
using BistQuant.Application.Common.Interfaces;
using BistQuant.Application.DTOs.PaperTrading;
using BistQuant.Application.Services;
using BistQuant.Application.Services.MarketData;
using BistQuant.Domain.Entities;
using BistQuant.Domain.Enums;
using BistQuant.Infrastructure.Persistence;
using BistQuant.Infrastructure.Providers.MarketData;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BistQuant.IntegrationTests;

public class BistDailyBulletinIntegrationTests
{
    private static (BistQuantDbContext context, BistDailyBulletinMarketDataProvider provider, BistMarketSessionCalendar calendar, CorporateActionAdjustmentService corporateActions) CreateTestEnvironment()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        var options = new DbContextOptionsBuilder<BistQuantDbContext>()
            .UseSqlite(connection)
            .Options;
        var context = new BistQuantDbContext(options);
        context.Database.EnsureCreated();

        context.Markets.Add(new Market
        {
            Id = 1,
            Code = "BIST",
            Name = "Borsa Istanbul",
            Country = "Turkey",
            Currency = "TRY",
            Timezone = "Europe/Istanbul"
        });
        context.SaveChanges();

        var inMemoryConfig = new Dictionary<string, string?>
        {
            ["BistBulletin:BaseUrl"] = "https://www.borsaistanbul.com/data/bulten",
            ["BistBulletin:StoragePath"] = Path.Combine(Path.GetTempPath(), "bist_test_marketdata_" + Guid.NewGuid().ToString("N")),
            ["BistSession:TimeZone"] = "Europe/Istanbul"
        };
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(inMemoryConfig).Build();

        var holidayCalendar = new ConfigurableHolidayCalendar(configuration);
        var calendar = new BistMarketSessionCalendar(configuration, holidayCalendar);
        var parser = new BistDailyBulletinParser();
        var corporateActions = new CorporateActionAdjustmentService(context, NullLogger<CorporateActionAdjustmentService>.Instance);

        var httpClient = new HttpClient();
        var provider = new BistDailyBulletinMarketDataProvider(
            context,
            httpClient,
            configuration,
            NullLogger<BistDailyBulletinMarketDataProvider>.Instance,
            parser,
            corporateActions,
            calendar
        );

        return (context, provider, calendar, corporateActions);
    }

    private static string CreateSyntheticBulletinCsv(DateOnly date, decimal thyaoOpen, decimal thyaoClose, decimal aselsOpen, decimal aselsClose)
    {
        var sb = new StringBuilder();
        sb.AppendLine("TARIH;MENKUL KIYMET KODU;MENKUL KIYMET ADI;GRUP;PAZAR;PIYASA;MENKUL GRUBU;MENKUL TURU;MENKUL SINIFI;ISLEM YONTEMI;PIYASA YAPICI;BIST 100;BIST 30;BRUT TAKAS;SIRKET ISLEMI;ISLEM GORMEYEN;ONCEKI KAPANIS;ACILIS;ACILIS SEANSI;GUNORTASI;EN DUSUK;EN YUKSEK;KAPANIS;KAPANIS SEANSI;FIYAT DEG;KALAN ALIS;KALAN SATIS;AOF;TOPLAM ISLEM HACMI;TOPLAM ISLEM ADEDI;TOPLAM SOZLESME");
        sb.AppendLine("DATE;INSTRUMENT SERIES CODE;INSTRUMENT NAME;GROUP;MARKET SEGMENT;MARKET;INSTRUMENT GROUP;INSTRUMENT TYPE;INSTRUMENT CLASS;TRADING METHOD;MARKET MAKER;BIST 100;BIST 30;GROSS SETTLEMENT;CORPORATE ACTION;SUSPENDED;PREVIOUS LAST PRICE;OPENING PRICE;OPENING SESSION PRICE;MIDDAY PRICE;LOWEST PRICE;HIGHEST PRICE;CLOSING PRICE;CLOSING SESSION PRICE;CHANGE;REMAINING BID;REMAINING ASK;VWAP;TOTAL TRADED VALUE;TOTAL TRADED VOLUME;TOTAL NUMBER OF CONTRACTS");
        sb.AppendLine($"{date:yyyy-MM-dd};THYAO.E;TURK HAVA YOLLARI;A;Z;MSPOT;EQT;MSPOTEQT;MSPOTEQTTHYAO;SI;0;1;1;0;;0;300.0;{thyaoOpen};{thyaoOpen};0;{Math.Min(thyaoOpen, thyaoClose) - 2};{Math.Max(thyaoOpen, thyaoClose) + 2};{thyaoClose};{thyaoClose};1.5;300;300;305;1500000000;5000000;40000");
        sb.AppendLine($"{date:yyyy-MM-dd};ASELS.E;ASELSAN ELEKTRONIK;A;Z;MSPOT;EQT;MSPOTEQT;MSPOTEQTASELS;SI;0;1;1;0;;0;60.0;{aselsOpen};{aselsOpen};0;{Math.Min(aselsOpen, aselsClose) - 1};{Math.Max(aselsOpen, aselsClose) + 1};{aselsClose};{aselsClose};2.0;60;60;61;800000000;13000000;35000");
        sb.AppendLine($"{date:yyyy-MM-dd};SUSPD.E;SUSPENDED CO;A;Z;MSPOT;EQT;MSPOTEQT;MSPOTEQTSUSPD;SI;0;0;0;0;;1;20.0;0;0;0;0;0;0;0;0;0;0;0;0;0;0");
        return sb.ToString();
    }

    [Fact]
    public async Task IngestBulletin_CreatesAudit_PopulatesSymbols_InsertsPriceBarsAndStats()
    {
        var (context, provider, _, _) = CreateTestEnvironment();
        var sessionDate = new DateOnly(2026, 3, 18);
        var csv = CreateSyntheticBulletinCsv(sessionDate, 310.0m, 315.0m, 62.0m, 64.0m);

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));
        var import = await provider.ImportBulletinStreamAsync(sessionDate, $"BUL_{sessionDate:yyyyMMdd}.csv", stream);

        Assert.Equal(MarketDataImportStatus.Success, import.Status);
        Assert.Equal(2, import.PriceBarsInserted);
        Assert.Equal(0, import.PriceBarsUpdated);
        Assert.Equal(3, import.RowsAccepted);

        // Verify Symbols created
        var symbols = await context.Symbols.OrderBy(s => s.Ticker).ToListAsync();
        Assert.Equal(3, symbols.Count);
        Assert.Equal("ASELS", symbols[0].Ticker);
        Assert.Equal("SUSPD", symbols[1].Ticker);
        Assert.False(symbols[1].IsActive); // Suspended is marked inactive
        Assert.Equal("THYAO", symbols[2].Ticker);
        Assert.True(symbols[2].IsActive);

        // Verify PriceBars: only THYAO and ASELS have valid OHLC bars, SUSPD has none
        var bars = await context.PriceBars.ToListAsync();
        Assert.Equal(2, bars.Count);

        var thyaoBar = bars.First(b => b.SymbolId == symbols[2].Id);
        Assert.Equal(310.0m, thyaoBar.Open);
        Assert.Equal(315.0m, thyaoBar.Close);

        // Verify DailyInstrumentMarketStats: all 3 recorded
        var stats = await context.DailyInstrumentMarketStats.ToListAsync();
        Assert.Equal(3, stats.Count);
        var suspdStats = stats.First(s => s.SymbolId == symbols[1].Id);
        Assert.True(suspdStats.Suspended);
    }

    [Fact]
    public async Task IngestBulletin_Idempotency_SameSha256CausesZeroDuplicateBars()
    {
        var (context, provider, _, _) = CreateTestEnvironment();
        var sessionDate = new DateOnly(2026, 3, 18);
        var csv = CreateSyntheticBulletinCsv(sessionDate, 310.0m, 315.0m, 62.0m, 64.0m);

        // Ingestion 1
        using (var stream1 = new MemoryStream(Encoding.UTF8.GetBytes(csv)))
        {
            var import1 = await provider.ImportBulletinStreamAsync(sessionDate, $"BUL_{sessionDate:yyyyMMdd}.csv", stream1);
            Assert.Equal(2, import1.PriceBarsInserted);
        }

        // Ingestion 2: Identical CSV stream (same date, same SHA256)
        using (var stream2 = new MemoryStream(Encoding.UTF8.GetBytes(csv)))
        {
            var import2 = await provider.ImportBulletinStreamAsync(sessionDate, $"BUL_{sessionDate:yyyyMMdd}.csv", stream2);
            // Must return the existing import with 0 duplicate bars
            Assert.Equal(MarketDataImportStatus.Success, import2.Status);
        }

        // Total price bars in DB must remain exactly 2
        var totalBars = await context.PriceBars.CountAsync();
        Assert.Equal(2, totalBars);
    }

    [Fact]
    public async Task IngestBulletin_RevisionHandling_UpdatesExistingBarsWithoutDuplication()
    {
        var (context, provider, _, _) = CreateTestEnvironment();
        var sessionDate = new DateOnly(2026, 3, 18);
        var initialCsv = CreateSyntheticBulletinCsv(sessionDate, 310.0m, 315.0m, 62.0m, 64.0m);
        var revisedCsv = CreateSyntheticBulletinCsv(sessionDate, 310.0m, 318.0m, 62.0m, 65.0m);

        using (var stream1 = new MemoryStream(Encoding.UTF8.GetBytes(initialCsv)))
        {
            await provider.ImportBulletinStreamAsync(sessionDate, $"BUL_{sessionDate:yyyyMMdd}.csv", stream1);
        }

        // Revision with updated close prices
        using (var stream2 = new MemoryStream(Encoding.UTF8.GetBytes(revisedCsv)))
        {
            var revisedImport = await provider.ImportBulletinStreamAsync(sessionDate, $"BUL_{sessionDate:yyyyMMdd}.csv", stream2);
            Assert.Equal(2, revisedImport.PriceBarsUpdated);
            Assert.Equal(0, revisedImport.PriceBarsInserted);
        }

        var totalBars = await context.PriceBars.CountAsync();
        Assert.Equal(2, totalBars);

        var thyaoBar = await context.PriceBars
            .Include(b => b.Symbol)
            .FirstAsync(b => b.Symbol.Ticker == "THYAO");
        Assert.Equal(318.0m, thyaoBar.Close);
    }

    [Fact]
    public async Task TPlus1PaperTrading_ExecutesAtNextSessionOfficialOpenPrice()
    {
        var (context, provider, calendar, _) = CreateTestEnvironment();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var sessionDateT = today.AddDays(-1);
        var sessionDateTPlus1 = today;

        // Day T bulletin ingestion
        var csvT = CreateSyntheticBulletinCsv(sessionDateT, 310.0m, 315.0m, 62.0m, 64.0m);
        using (var streamT = new MemoryStream(Encoding.UTF8.GetBytes(csvT)))
        {
            await provider.ImportBulletinStreamAsync(sessionDateT, $"BUL_{sessionDateT:yyyyMMdd}.csv", streamT);
        }

        var user = new User { Email = "quant@bist.com", DisplayName = "Quant" };
        context.Users.Add(user);
        await context.SaveChangesAsync();

        var portfolio = new PaperPortfolio
        {
            UserId = user.Id,
            Name = "T+1 Forward Testing Portfolio",
            CashBalance = 100000m,
            InitialBalance = 100000m,
            IsAutoTradingEnabled = true,
            AutoTradingMinScore = 80,
            AutoTradingMaxAllocationPercent = 10.0m
        };
        context.PaperPortfolios.Add(portfolio);

        var thyao = await context.Symbols.FirstAsync(s => s.Ticker == "THYAO");

        // Day T close signal generated
        var signal = new Signal
        {
            SymbolId = thyao.Id,
            Timeframe = Timeframe.Daily,
            SourceSessionDate = sessionDateT,
            SignalType = SignalType.Buy,
            Score = 88,
            Price = 315.0m,
            StopLoss = 295.0m,
            TakeProfit1 = 345.0m,
            Confidence = 0.88m,
            ExpiresAt = DateTime.UtcNow.AddDays(2)
        };
        context.Signals.Add(signal);
        await context.SaveChangesAsync();

        var freshnessPolicy = new MarketDataFreshnessPolicy();
        var paperService = new PaperTradingService(context, new DummySignalEngine(), freshnessPolicy, NullLogger<PaperTradingService>.Instance, calendar);

        // Day T: auto trade scan creates PendingNextSessionOpen order
        await paperService.AutoTradeScanAsync(portfolio.Id);

        var order = await context.PaperOrders.FirstOrDefaultAsync(o => o.PortfolioId == portfolio.Id);
        Assert.NotNull(order);
        Assert.Equal(OrderStatus.PendingNextSessionOpen, order.Status);
        Assert.Null(order.FilledPrice);

        // Verify order is NOT filled at Day T close
        var initialTrades = await context.PaperTrades.Where(t => t.PortfolioId == portfolio.Id).ToListAsync();
        Assert.Empty(initialTrades);

        // Day T+1 bulletin arrives with OPEN = 322.0m
        var csvTPlus1 = CreateSyntheticBulletinCsv(sessionDateTPlus1, 322.0m, 325.0m, 64.5m, 65.0m);
        using (var streamTPlus1 = new MemoryStream(Encoding.UTF8.GetBytes(csvTPlus1)))
        {
            await provider.ImportBulletinStreamAsync(sessionDateTPlus1, $"BUL_{sessionDateTPlus1:yyyyMMdd}.csv", streamTPlus1);
        }

        // Execute pending orders for session T+1
        int filledCount = await paperService.ExecutePendingOrdersForSessionAsync(sessionDateTPlus1);
        Assert.Equal(1, filledCount);

        var filledOrder = await context.PaperOrders.FirstAsync(o => o.Id == order.Id);
        Assert.Equal(OrderStatus.Filled, filledOrder.Status);
        Assert.Equal(322.0m, filledOrder.FilledPrice); // Executed at Day T+1 Open!

        var trades = await context.PaperTrades.Where(t => t.PortfolioId == portfolio.Id).ToListAsync();
        Assert.Single(trades);
        Assert.Equal(322.0m, trades[0].Price);
    }

    private class DummySignalEngine : ISignalEngine
    {
        public SignalType ClassifySignal(int score) => SignalType.Buy;
        public SignalType ClassifySignal(int score, IndicatorSnapshot? snapshot, IReadOnlyList<PriceBar>? history) => SignalType.Buy;
        public Domain.Models.RiskParameters CalculateRiskParameters(decimal currentPrice, IndicatorSnapshot snapshot) =>
            new(currentPrice, currentPrice * 0.95m, currentPrice * 1.10m, currentPrice * 1.20m, 2.0m);
        public Task<Signal?> GenerateAndSaveSignalAsync(int symbolId, Timeframe timeframe, Strategy? strategy = null, CancellationToken cancellationToken = default) =>
            Task.FromResult<Signal?>(null);
        public Task<Signal?> GetLatestSignalAsync(string symbol, Timeframe timeframe, CancellationToken cancellationToken = default) =>
            Task.FromResult<Signal?>(null);
    }
}
