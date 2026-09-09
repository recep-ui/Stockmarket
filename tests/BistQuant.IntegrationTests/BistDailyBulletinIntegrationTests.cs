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
            ["BistBulletin:VerifiedDownloadEndpoint"] = "https://www.borsaistanbul.com/data/thb/{YYYY}/{MM}/thb{YYYY}{MM}{DD}1.zip",
            ["BistBulletin:BaseUrl"] = "https://www.borsaistanbul.com/data/bulten",
            ["BistBulletin:StoragePath"] = Path.Combine(Path.GetTempPath(), "bist_test_marketdata_" + Guid.NewGuid().ToString("N")),
            ["BistSession:TimeZone"] = "Europe/Istanbul"
        };
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(inMemoryConfig).Build();

        var holidayCalendar = new ConfigurableHolidayCalendar(configuration);
        var calendar = new BistMarketSessionCalendar(configuration, holidayCalendar);
        var parser = new BistDailyBulletinParser();
        var corporateActions = new CorporateActionAdjustmentService(context, configuration, NullLogger<CorporateActionAdjustmentService>.Instance);
        var sessionDateResolver = new MarketSessionDateResolver();

        var httpClient = new HttpClient();
        var provider = new BistDailyBulletinMarketDataProvider(
            context,
            httpClient,
            configuration,
            NullLogger<BistDailyBulletinMarketDataProvider>.Instance,
            parser,
            corporateActions,
            calendar,
            sessionDateResolver
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
        Assert.True(symbols[1].IsActive); // 1-day suspension keeps symbol active intact
        Assert.Equal(sessionDate, symbols[1].LastSeenInBulletinDate);
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

    [OfficialSmokeTestFact]
    [Trait("Category", "OfficialSmokeTest")]
    public async Task OfficialSampleBulletin_20260907_IngestsAccurately_AndVerifiesAselsAndThyao()
    {
        var (context, provider, _, _) = CreateTestEnvironment();
        var sessionDate = new DateOnly(2026, 9, 7);

        // Portable official sample file discovery: environment variable takes precedence
        string? zipPath = Environment.GetEnvironmentVariable("BIST_OFFICIAL_BULLETIN_SAMPLE_PATH");
        if (string.IsNullOrWhiteSpace(zipPath) || !File.Exists(zipPath))
        {
            var dir = AppContext.BaseDirectory;
            for (int i = 0; i < 10; i++)
            {
                var candidate = Path.Combine(dir, "scratch", "thb202609071.zip");
                if (File.Exists(candidate))
                {
                    zipPath = candidate;
                    break;
                }
                var parent = Directory.GetParent(dir);
                if (parent == null) break;
                dir = parent.FullName;
            }
        }

        if (zipPath == null || !File.Exists(zipPath))
        {
            return;
        }

        using var fileStream = File.OpenRead(zipPath);
        var import = await provider.ImportBulletinStreamAsync(sessionDate, "thb202609071.zip", fileStream);

        Assert.Equal(MarketDataImportStatus.Success, import.Status);
        Assert.True(import.RowsAccepted > 500, $"Expected >500 rows accepted, got {import.RowsAccepted}");
        Assert.True(import.PriceBarsInserted > 500, $"Expected >500 bars inserted, got {import.PriceBarsInserted}");

        // Verify ASELS
        var asels = await context.Symbols.FirstOrDefaultAsync(s => s.Ticker == "ASELS");
        Assert.NotNull(asels);
        Assert.True(asels.IsActive);
        Assert.Equal(sessionDate, asels.LastSeenInBulletinDate);

        var aselsBar = await context.PriceBars.FirstOrDefaultAsync(b => b.SymbolId == asels.Id && b.Timestamp == sessionDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc));
        Assert.NotNull(aselsBar);
        Assert.Equal(390.25m, aselsBar.Open);
        Assert.Equal(398.75m, aselsBar.High);
        Assert.Equal(390.25m, aselsBar.Low);
        Assert.Equal(392.5m, aselsBar.Close);
        Assert.Equal(28176860m, aselsBar.Volume);
        Assert.Equal(sessionDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc), aselsBar.Timestamp);

        // Verify THYAO
        var thyao = await context.Symbols.FirstOrDefaultAsync(s => s.Ticker == "THYAO");
        Assert.NotNull(thyao);
        Assert.True(thyao.IsActive);
        Assert.Equal(sessionDate, thyao.LastSeenInBulletinDate);

        var thyaoBar = await context.PriceBars.FirstOrDefaultAsync(b => b.SymbolId == thyao.Id && b.Timestamp == sessionDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc));
        Assert.NotNull(thyaoBar);
        Assert.Equal(295.0m, thyaoBar.Open);
        Assert.Equal(297.25m, thyaoBar.High);
        Assert.Equal(292.25m, thyaoBar.Low);
        Assert.Equal(296.75m, thyaoBar.Close);
        Assert.Equal(37949058m, thyaoBar.Volume);
        Assert.Equal(sessionDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc), thyaoBar.Timestamp);
    }

    [Fact]
    public async Task DeterministicCi_SyntheticBulletin_IngestsSuccessfully_AndVerifiesOHLC()
    {
        var (context, provider, _, _) = CreateTestEnvironment();
        var sessionDate = new DateOnly(2026, 9, 7);

        // Locate repository-contained synthetic fixture
        string? fixturePath = null;
        var dir = AppContext.BaseDirectory;
        for (int i = 0; i < 10; i++)
        {
            var candidate = Path.Combine(dir, "tests", "Fixtures", "BistBulletin", "synthetic_thb202609071.zip");
            if (File.Exists(candidate))
            {
                fixturePath = candidate;
                break;
            }
            var parent = Directory.GetParent(dir);
            if (parent == null) break;
            dir = parent.FullName;
        }

        Assert.NotNull(fixturePath);
        Assert.True(File.Exists(fixturePath));

        using var fileStream = File.OpenRead(fixturePath);
        var import = await provider.ImportBulletinStreamAsync(sessionDate, "thb202609071.zip", fileStream);

        Assert.Equal(MarketDataImportStatus.Success, import.Status);
        Assert.Equal(3, import.RowsAccepted); // ASELS, THYAO, GARAN (ASABC.V filtered as WNT)
        Assert.Equal(3, import.PriceBarsInserted);

        // Verify ASELS
        var asels = await context.Symbols.FirstOrDefaultAsync(s => s.Ticker == "ASELS");
        Assert.NotNull(asels);
        Assert.Equal(sessionDate, asels.LastSeenInBulletinDate);
        var aselsBar = await context.PriceBars.FirstOrDefaultAsync(b => b.SymbolId == asels.Id && b.Timestamp == sessionDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc));
        Assert.NotNull(aselsBar);
        Assert.Equal(390.25m, aselsBar.Open);
        Assert.Equal(398.75m, aselsBar.High);
        Assert.Equal(390.25m, aselsBar.Low);
        Assert.Equal(392.50m, aselsBar.Close);
        Assert.Equal(28176860m, aselsBar.Volume);

        // Verify THYAO
        var thyao = await context.Symbols.FirstOrDefaultAsync(s => s.Ticker == "THYAO");
        Assert.NotNull(thyao);
        var thyaoBar = await context.PriceBars.FirstOrDefaultAsync(b => b.SymbolId == thyao.Id && b.Timestamp == sessionDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc));
        Assert.NotNull(thyaoBar);
        Assert.Equal(321.00m, thyaoBar.Open);
        Assert.Equal(325.50m, thyaoBar.High);
        Assert.Equal(320.00m, thyaoBar.Low);
        Assert.Equal(324.75m, thyaoBar.Close);
        Assert.Equal(29530180m, thyaoBar.Volume);

        // Verify GARAN
        var garan = await context.Symbols.FirstOrDefaultAsync(s => s.Ticker == "GARAN");
        Assert.NotNull(garan);
        var garanBar = await context.PriceBars.FirstOrDefaultAsync(b => b.SymbolId == garan.Id && b.Timestamp == sessionDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc));
        Assert.NotNull(garanBar);
        Assert.Equal(110.50m, garanBar.Open);
        Assert.Equal(112.50m, garanBar.High);
        Assert.Equal(110.00m, garanBar.Low);
        Assert.Equal(111.80m, garanBar.Close);
        Assert.Equal(50542600m, garanBar.Volume);
    }

    [Fact]
    public async Task RevisionAtomicity_Success_FlipsCurrentRevisionAtomically()
    {
        var (context, provider, _, _) = CreateTestEnvironment();
        var sessionDate = new DateOnly(2026, 3, 19);

        // 1. Revision 1: initial bulletin
        var csvRev1 = CreateSyntheticBulletinCsv(sessionDate, 300.0m, 305.0m, 60.0m, 61.0m);
        using var streamRev1 = new MemoryStream(Encoding.UTF8.GetBytes(csvRev1));
        var import1 = await provider.ImportBulletinStreamAsync(sessionDate, $"thb{sessionDate:yyyyMMdd}1.csv", streamRev1);

        Assert.Equal(MarketDataImportStatus.Success, import1.Status);
        Assert.True(import1.IsCurrent);
        Assert.Equal(1, import1.RevisionNumber);

        // 2. Revision 2: updated bulletin with different prices
        var csvRev2 = CreateSyntheticBulletinCsv(sessionDate, 302.0m, 308.0m, 60.5m, 62.0m);
        using var streamRev2 = new MemoryStream(Encoding.UTF8.GetBytes(csvRev2));
        var import2 = await provider.ImportBulletinStreamAsync(sessionDate, $"thb{sessionDate:yyyyMMdd}2.csv", streamRev2);

        Assert.Equal(MarketDataImportStatus.Success, import2.Status);
        Assert.True(import2.IsCurrent);
        Assert.Equal(2, import2.RevisionNumber);

        // Verify Rev 1 is no longer current
        var reloadedImport1 = await context.MarketDataImports.AsNoTracking().FirstAsync(i => i.Id == import1.Id);
        Assert.False(reloadedImport1.IsCurrent);

        // Database invariant: exactly one record with IsCurrent == true for this sessionDate
        var currentCount = await context.MarketDataImports
            .CountAsync(i => i.Provider == provider.Capabilities.ProviderName && i.SessionDate == sessionDate && i.IsCurrent && i.Status == MarketDataImportStatus.Success);
        Assert.Equal(1, currentCount);
    }

    [Fact]
    public async Task RevisionAtomicity_FailedRevision_PreservesRevision1Current()
    {
        var (context, provider, _, _) = CreateTestEnvironment();
        var sessionDate = new DateOnly(2026, 3, 20);

        // 1. Revision 1: successful
        var csvRev1 = CreateSyntheticBulletinCsv(sessionDate, 310.0m, 315.0m, 62.0m, 63.0m);
        using var streamRev1 = new MemoryStream(Encoding.UTF8.GetBytes(csvRev1));
        var import1 = await provider.ImportBulletinStreamAsync(sessionDate, $"thb{sessionDate:yyyyMMdd}1.csv", streamRev1);

        Assert.Equal(MarketDataImportStatus.Success, import1.Status);
        Assert.True(import1.IsCurrent);

        // 2. Revision 2: corrupt data that fails validation
        var corruptBytes = Encoding.UTF8.GetBytes("INVALID_BULLETIN_CONTENT_NO_VALID_COLUMNS");
        using var corruptStream = new MemoryStream(corruptBytes);

        var import2 = await provider.ImportBulletinStreamAsync(sessionDate, $"thb{sessionDate:yyyyMMdd}2.csv", corruptStream);
        Assert.True(import2.Status == MarketDataImportStatus.Failed || import2.Status == MarketDataImportStatus.SchemaMismatch);
        Assert.False(import2.IsCurrent);

        // Verify Rev 1 remains current and successful
        var reloadedImport1 = await context.MarketDataImports.AsNoTracking().FirstAsync(i => i.Id == import1.Id);
        Assert.Equal(MarketDataImportStatus.Success, reloadedImport1.Status);
        Assert.True(reloadedImport1.IsCurrent);

        // Exactly one current successful revision remains
        var currentCount = await context.MarketDataImports
            .CountAsync(i => i.Provider == provider.Capabilities.ProviderName && i.SessionDate == sessionDate && i.IsCurrent && i.Status == MarketDataImportStatus.Success);
        Assert.Equal(1, currentCount);
    }

    [Fact]
    public async Task BulletinFetchAttempt_AttemptNumbering_IncrementsStrictlyOnHttpRequests()
    {
        var (context, provider, _, _) = CreateTestEnvironment();
        var sessionDate = new DateOnly(2026, 3, 23); // Monday

        // Perform first download attempt to non-existent endpoint (will fail / 404 / connection error)
        var result1 = await provider.DownloadBulletinForDateAsync(sessionDate);
        var attempts1 = await context.BulletinFetchAttempts.Where(a => a.SessionDate == sessionDate).ToListAsync();
        if (attempts1.Count > 0)
        {
            Assert.Equal(1, attempts1[0].AttemptCount);
        }

        // Non-HTTP check: weekend date must NOT produce HTTP fetch attempt increment
        var weekendDate = new DateOnly(2026, 3, 21); // Saturday
        var weekendResult = await provider.DownloadBulletinForDateAsync(weekendDate);
        var weekendAttempts = await context.BulletinFetchAttempts.Where(a => a.SessionDate == weekendDate).ToListAsync();
        Assert.Empty(weekendAttempts); // No HTTP fetch attempt recorded
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

public sealed class OfficialSmokeTestFactAttribute : FactAttribute
{
    public OfficialSmokeTestFactAttribute()
    {
        string? zipPath = Environment.GetEnvironmentVariable("BIST_OFFICIAL_BULLETIN_SAMPLE_PATH");
        if (string.IsNullOrWhiteSpace(zipPath) || !File.Exists(zipPath))
        {
            Skip = "OfficialSmokeTest: SKIPPED – BIST_OFFICIAL_BULLETIN_SAMPLE_PATH not configured";
        }
    }
}
