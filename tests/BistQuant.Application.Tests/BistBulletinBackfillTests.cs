using BistQuant.Application.Common.Interfaces;
using BistQuant.Application.DTOs.Backfill;
using BistQuant.Application.Services.MarketData;
using BistQuant.Domain.Entities;
using BistQuant.Domain.Enums;
using BistQuant.Infrastructure.Persistence;
using BistQuant.Infrastructure.Services.MarketData;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace BistQuant.Application.Tests;

public class BistBulletinBackfillTests
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
    public async Task StartBackfillAsync_InvalidDateRange_ThrowsArgumentException()
    {
        using var context = CreateDbContext();
        var mockCalendar = new Mock<IMarketSessionCalendar>();
        var mockProvider = new Mock<IBistDailyBulletinMarketDataProvider>();
        var mockScopeFactory = new Mock<IServiceScopeFactory>();

        var service = new BistBulletinBackfillService(
            mockScopeFactory.Object,
            context,
            mockProvider.Object,
            mockCalendar.Object,
            NullLogger<BistBulletinBackfillService>.Instance
        );

        // Start date after end date
        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.StartBackfillAsync(new DateOnly(2026, 3, 10), new DateOnly(2026, 3, 5)));

        // End date in future
        var futureDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(5));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.StartBackfillAsync(new DateOnly(2026, 1, 1), futureDate));
    }

    [Fact]
    public async Task StartBackfillAsync_ValidRange_CreatesJobWithPendingStatus()
    {
        using var context = CreateDbContext();
        var mockCalendar = new Mock<IMarketSessionCalendar>();
        mockCalendar.Setup(c => c.IsTradingDay(It.IsAny<DateOnly>())).Returns<DateOnly>(d => d.DayOfWeek != DayOfWeek.Saturday && d.DayOfWeek != DayOfWeek.Sunday);

        var mockProvider = new Mock<IBistDailyBulletinMarketDataProvider>();
        var mockScopeFactory = new Mock<IServiceScopeFactory>();

        var service = new BistBulletinBackfillService(
            mockScopeFactory.Object,
            context,
            mockProvider.Object,
            mockCalendar.Object,
            NullLogger<BistBulletinBackfillService>.Instance
        );

        var startDate = new DateOnly(2026, 2, 2); // Monday
        var endDate = new DateOnly(2026, 2, 6);   // Friday (5 trading days)

        var job = await service.StartBackfillAsync(startDate, endDate, forceRevisionCheck: false);

        Assert.NotNull(job);
        Assert.True(job.Id > 0);
        Assert.Equal(startDate, job.StartDate);
        Assert.Equal(endDate, job.EndDate);
        Assert.Equal(5, job.SessionsTotal);
        Assert.True(job.Status == BackfillJobStatus.Pending || job.Status == BackfillJobStatus.Running);

        var dbJob = await context.BackfillJobs.FindAsync(job.Id);
        Assert.NotNull(dbJob);
        Assert.Equal(5, dbJob.SessionsTotal);
    }

    [Fact]
    public async Task PauseAndResumeAndCancel_UpdatesJobStatusCorrectly()
    {
        using var context = CreateDbContext();
        var mockCalendar = new Mock<IMarketSessionCalendar>();
        var mockProvider = new Mock<IBistDailyBulletinMarketDataProvider>();
        var mockScopeFactory = new Mock<IServiceScopeFactory>();

        var job = new BackfillJob
        {
            StartDate = new DateOnly(2026, 1, 5),
            EndDate = new DateOnly(2026, 1, 9),
            Status = BackfillJobStatus.Running,
            SessionsTotal = 5,
            StartedAt = DateTime.UtcNow
        };
        context.BackfillJobs.Add(job);
        await context.SaveChangesAsync();

        var service = new BistBulletinBackfillService(
            mockScopeFactory.Object,
            context,
            mockProvider.Object,
            mockCalendar.Object,
            NullLogger<BistBulletinBackfillService>.Instance
        );

        // Pause
        var paused = await service.PauseBackfillAsync(job.Id);
        Assert.True(paused);
        var updatedJob = await context.BackfillJobs.FindAsync(job.Id);
        Assert.Equal(BackfillJobStatus.Paused, updatedJob!.Status);

        // Resume
        var resumed = await service.ResumeBackfillAsync(job.Id);
        Assert.True(resumed);
        updatedJob = await context.BackfillJobs.FindAsync(job.Id);
        Assert.Equal(BackfillJobStatus.Running, updatedJob!.Status);

        // Cancel
        var cancelled = await service.CancelBackfillAsync(job.Id);
        Assert.True(cancelled);
        updatedJob = await context.BackfillJobs.FindAsync(job.Id);
        Assert.Equal(BackfillJobStatus.Cancelled, updatedJob!.Status);
        Assert.NotNull(updatedJob.CompletedAt);
    }

    [Fact]
    public async Task MarketDataGapDetector_DetectsGaps_AndClassifiesMissingBulletin()
    {
        using var context = CreateDbContext();
        var symbol = new Symbol { Id = 1, Ticker = "ASELS", Name = "Aselsan", MarketId = 1, IsActive = true };
        context.Symbols.Add(symbol);

        // Price bar for Monday
        context.PriceBars.Add(new PriceBar
        {
            SymbolId = 1,
            Timeframe = Timeframe.Daily,
            Timestamp = new DateTime(2026, 2, 2, 18, 0, 0, DateTimeKind.Utc),
            Open = 100,
            High = 105,
            Low = 99,
            Close = 104,
            Volume = 1000000
        });
        await context.SaveChangesAsync();

        var mockCalendar = new Mock<IMarketSessionCalendar>();
        // Feb 2 (Mon), Feb 3 (Tue), Feb 4 (Wed) are trading days
        mockCalendar.Setup(c => c.IsTradingDay(It.IsAny<DateOnly>())).Returns<DateOnly>(d =>
            d == new DateOnly(2026, 2, 2) || d == new DateOnly(2026, 2, 3) || d == new DateOnly(2026, 2, 4));

        var detector = new MarketDataGapDetector(context, mockCalendar.Object, NullLogger<MarketDataGapDetector>.Instance);

        var gapsDto = await detector.DetectGapsForSymbolAsync("ASELS", new DateOnly(2026, 2, 2), new DateOnly(2026, 2, 4));

        Assert.Equal("ASELS", gapsDto.Ticker);
        Assert.Equal(2, gapsDto.TotalGaps); // Feb 3 and Feb 4 missing
        Assert.Contains(gapsDto.Gaps, g => g.SessionDate == new DateOnly(2026, 2, 2) == false && g.Reason == MarketDataGapReason.BulletinMissing);

        var gapDates = gapsDto.Gaps.Select(g => g.SessionDate).ToList();
        Assert.Contains(new DateOnly(2026, 2, 3), gapDates);
        Assert.Contains(new DateOnly(2026, 2, 4), gapDates);
    }

    [Fact]
    public async Task MarketDataGapDetector_CalculatesUniverseCoverage()
    {
        using var context = CreateDbContext();
        var sym1 = new Symbol { Id = 1, Ticker = "THYAO", Name = "Turk Hava Yollari", MarketId = 1, IsActive = true };
        var sym2 = new Symbol { Id = 2, Ticker = "GARAN", Name = "Garanti BBVA", MarketId = 1, IsActive = true };
        context.Symbols.AddRange(sym1, sym2);

        // THYAO has 250 bars
        for (int i = 0; i < 250; i++)
        {
            context.PriceBars.Add(new PriceBar
            {
                SymbolId = 1,
                Timeframe = Timeframe.Daily,
                Timestamp = new DateTime(2025, 1, 1, 18, 0, 0, DateTimeKind.Utc).AddDays(i),
                Open = 100, High = 105, Low = 99, Close = 104, Volume = 500000
            });
        }

        // GARAN has 50 bars
        for (int i = 0; i < 50; i++)
        {
            context.PriceBars.Add(new PriceBar
            {
                SymbolId = 2,
                Timeframe = Timeframe.Daily,
                Timestamp = new DateTime(2025, 1, 1, 18, 0, 0, DateTimeKind.Utc).AddDays(i),
                Open = 50, High = 52, Low = 49, Close = 51, Volume = 500000
            });
        }

        await context.SaveChangesAsync();

        var mockCalendar = new Mock<IMarketSessionCalendar>();
        mockCalendar.Setup(c => c.IsTradingDay(It.IsAny<DateOnly>())).Returns(true);

        var detector = new MarketDataGapDetector(context, mockCalendar.Object, NullLogger<MarketDataGapDetector>.Instance);

        var coverage = await detector.GetUniverseCoverageAsync();

        Assert.Equal(2, coverage.ActiveSymbols);
        var thyao = coverage.SymbolCoverages.FirstOrDefault(s => s.Ticker == "THYAO");
        Assert.NotNull(thyao);
        Assert.Equal(250, thyao.DailyBarCount);
        Assert.True(thyao.Ema200Ready); // >= 220 bars

        var garan = coverage.SymbolCoverages.FirstOrDefault(s => s.Ticker == "GARAN");
        Assert.NotNull(garan);
        Assert.Equal(50, garan.DailyBarCount);
        Assert.False(garan.Ema200Ready);
    }
}
