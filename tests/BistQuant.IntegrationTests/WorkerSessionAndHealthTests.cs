using BistQuant.API.Health;
using BistQuant.Application.Common.Interfaces;
using BistQuant.Application.Services.MarketData;
using BistQuant.Domain.Entities;
using BistQuant.Domain.Enums;
using BistQuant.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Xunit;

namespace BistQuant.IntegrationTests;

public class WorkerSessionAndHealthTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public WorkerSessionAndHealthTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task WorkerScanHealthCheck_MultiTimeframe_EvaluatedCorrectly()
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<BistQuantDbContext>();
        var calendar = scope.ServiceProvider.GetRequiredService<IMarketSessionCalendar>();
        var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();

        // Insert recent successful heartbeats for Daily, H1, and M15
        var now = DateTime.UtcNow;
        var dailyHb = new WorkerHeartbeat
        {
            WorkerInstance = "TestWorker-01",
            ScanType = "UniverseScan",
            Timeframe = Timeframe.Daily,
            StartedAt = now.AddHours(-2),
            CompletedAt = now.AddHours(-1.8),
            Success = true,
            SymbolCount = 100
        };

        var h1Hb = new WorkerHeartbeat
        {
            WorkerInstance = "TestWorker-01",
            ScanType = "UniverseScan",
            Timeframe = Timeframe.H1,
            StartedAt = now.AddMinutes(-30),
            CompletedAt = now.AddMinutes(-25),
            Success = true,
            SymbolCount = 100
        };

        var m15Hb = new WorkerHeartbeat
        {
            WorkerInstance = "TestWorker-01",
            ScanType = "UniverseScan",
            Timeframe = Timeframe.M15,
            StartedAt = now.AddMinutes(-10),
            CompletedAt = now.AddMinutes(-8),
            Success = true,
            SymbolCount = 100
        };

        context.WorkerHeartbeats.AddRange(dailyHb, h1Hb, m15Hb);
        await context.SaveChangesAsync();

        var healthCheck = new WorkerScanHealthCheck(context, calendar, config);
        var result = await healthCheck.CheckHealthAsync(new HealthCheckContext());

        Assert.Equal(HealthStatus.Healthy, result.Status);
        Assert.NotNull(result.Data);
        Assert.True(result.Data.ContainsKey("Daily"));
        Assert.True(result.Data.ContainsKey("H1"));
        Assert.True(result.Data.ContainsKey("M15"));
    }

    [Fact]
    public async Task MarketDataFreshnessHealthCheck_MultiTimeframeAndSessionAware()
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<BistQuantDbContext>();
        var calendar = scope.ServiceProvider.GetRequiredService<IMarketSessionCalendar>();
        var freshnessPolicy = scope.ServiceProvider.GetRequiredService<IMarketDataFreshnessPolicy>();
        var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();

        var healthCheck = new MarketDataFreshnessHealthCheck(context, freshnessPolicy, calendar, config);
        var result = await healthCheck.CheckHealthAsync(new HealthCheckContext());

        // Should return structured data per timeframe
        Assert.NotNull(result.Data);
        Assert.True(result.Data.ContainsKey("Daily"));
        Assert.True(result.Data.ContainsKey("H1"));
        Assert.True(result.Data.ContainsKey("M15"));
    }

    [Fact]
    public void MarketDataFreshness_WeekendClosedState_FridayDailyBarNotStaleOnSunday()
    {
        var calendarConfig = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["BistSession:TimeZone"] = "Europe/Istanbul",
            ["BistSession:OpenTime"] = "10:00:00",
            ["BistSession:CloseTime"] = "18:00:00",
            ["BistSession:DailyFinalizationTime"] = "18:15:00"
        }).Build();

        var calendar = new BistMarketSessionCalendar(calendarConfig);

        // Sunday afternoon: 2026-09-13 14:00 Istanbul = 11:00 UTC
        var sundayUtc = new DateTime(2026, 9, 13, 11, 0, 0, DateTimeKind.Utc);

        // Expected latest closed daily candle on Sunday is Friday 2026-09-11
        var expectedBarTimeUtc = calendar.ExpectedLatestBarTimeUtc(Timeframe.Daily, sundayUtc);

        // Friday's daily bar date: 2026-09-11 (in UTC: 2026-09-10 21:00:00 UTC)
        var fridayBarDateUtc = new DateTime(2026, 9, 10, 21, 0, 0, DateTimeKind.Utc);

        Assert.Equal(fridayBarDateUtc, expectedBarTimeUtc);

        // A Friday bar evaluated on Sunday against the expected bar time has age difference = 0
        var freshnessPolicy = new MarketDataFreshnessPolicy();
        var check = freshnessPolicy.CheckFreshness(Timeframe.Daily, fridayBarDateUtc, asOf: expectedBarTimeUtc);
        Assert.True(check.IsFresh, "Friday Daily bar evaluated on Sunday against calendar expected bar time must be fresh!");
    }

    [Fact]
    public void MarketScanScheduler_PreventsWeekendAndHolidayScans()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["BistSession:TimeZone"] = "Europe/Istanbul",
            ["BistSession:OpenTime"] = "10:00:00",
            ["BistSession:CloseTime"] = "18:00:00",
            ["BistSession:DailyFinalizationTime"] = "18:15:00",
            ["BistSession:Holidays:0"] = "2026-10-29",
            ["ScannerSchedules:M15Enabled"] = "true",
            ["ScannerSchedules:H1Enabled"] = "true",
            ["ScannerSchedules:DailyEnabled"] = "true"
        }).Build();

        var holidays = new ConfigurableHolidayCalendar(config);
        var calendar = new BistMarketSessionCalendar(config, holidays);
        var scheduler = new BistQuant.Application.Services.MarketData.MarketScanScheduler(config, calendar, Microsoft.Extensions.Logging.Abstractions.NullLogger<BistQuant.Application.Services.MarketData.MarketScanScheduler>.Instance);
        var lastCompleted = new Dictionary<Timeframe, DateTime>();

        // Saturday -> Empty
        var saturdayUtc = new DateTime(2026, 9, 12, 10, 30, 0, DateTimeKind.Utc);
        var satDue = scheduler.GetDueTimeframes(saturdayUtc, lastCompleted);
        Assert.Empty(satDue);

        // Sunday -> Empty
        var sundayUtc = new DateTime(2026, 9, 13, 15, 20, 0, DateTimeKind.Utc);
        var sunDue = scheduler.GetDueTimeframes(sundayUtc, lastCompleted);
        Assert.Empty(sunDue);

        // Configured Holiday (2026-10-29) -> Empty
        var holidayUtc = new DateTime(2026, 10, 29, 12, 0, 0, DateTimeKind.Utc);
        var holDue = scheduler.GetDueTimeframes(holidayUtc, lastCompleted);
        Assert.Empty(holDue);

        // Weekday before market open (06:30 UTC = 09:30 Istanbul) -> Empty
        var beforeOpenUtc = new DateTime(2026, 9, 9, 6, 30, 0, DateTimeKind.Utc);
        var beforeOpenDue = scheduler.GetDueTimeframes(beforeOpenUtc, lastCompleted);
        Assert.Empty(beforeOpenDue);

        // Weekday after 18:15 Istanbul (15:20 UTC) -> Daily due
        var postCloseUtc = new DateTime(2026, 9, 9, 15, 20, 0, DateTimeKind.Utc);
        var postCloseDue = scheduler.GetDueTimeframes(postCloseUtc, lastCompleted);
        Assert.Contains(Timeframe.Daily, postCloseDue);
    }
}
