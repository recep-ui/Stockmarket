using BistQuant.Application.Common.Interfaces;
using BistQuant.Application.Services;
using BistQuant.Application.Services.MarketData;
using BistQuant.Domain.Enums;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace BistQuant.Application.Tests;

public class BistSessionCalendarTests
{
    private static (BistMarketSessionCalendar calendar, IHolidayCalendar holidays) CreateCalendar()
    {
        var dict = new Dictionary<string, string?>
        {
            ["BistSession:TimeZone"] = "Europe/Istanbul",
            ["BistSession:OpenTime"] = "10:00:00",
            ["BistSession:CloseTime"] = "18:00:00",
            ["BistSession:DailyFinalizationTime"] = "18:15:00",
            ["BistSession:Holidays:0"] = "2026-10-29",
            ["BistSession:Holidays:1"] = "2026-01-01"
        };
        var config = new ConfigurationBuilder().AddInMemoryCollection(dict).Build();
        var holidays = new ConfigurableHolidayCalendar(config);
        var calendar = new BistMarketSessionCalendar(config, holidays);
        return (calendar, holidays);
    }

    [Fact]
    public void IsTradingDay_NormalTradingDayAndWeekends_CorrectlyIdentified()
    {
        var (calendar, _) = CreateCalendar();

        // Wednesday 2026-09-09 10:00 UTC (13:00 Istanbul) -> Trading day
        var wednesdayUtc = new DateTime(2026, 9, 9, 10, 0, 0, DateTimeKind.Utc);
        Assert.True(calendar.IsTradingDay(wednesdayUtc));

        // Saturday 2026-09-12 -> Weekend (not trading day)
        var saturdayUtc = new DateTime(2026, 9, 12, 10, 0, 0, DateTimeKind.Utc);
        Assert.False(calendar.IsTradingDay(saturdayUtc));

        // Sunday 2026-09-13 -> Weekend (not trading day)
        var sundayUtc = new DateTime(2026, 9, 13, 10, 0, 0, DateTimeKind.Utc);
        Assert.False(calendar.IsTradingDay(sundayUtc));
    }

    [Fact]
    public void IsTradingDay_ConfiguredHoliday_IdentifiedAsNonTradingDay()
    {
        var (calendar, holidays) = CreateCalendar();

        // 2026-10-29 is a Thursday and configured Republic Day holiday
        var holidayDate = new DateOnly(2026, 10, 29);
        Assert.True(holidays.IsHoliday(holidayDate));

        var holidayUtc = new DateTime(2026, 10, 29, 10, 0, 0, DateTimeKind.Utc);
        Assert.False(calendar.IsTradingDay(holidayUtc), "Configured market holiday must not be a trading day.");
    }

    [Fact]
    public void IsMarketOpen_TradingHours_AccuratelyReflected()
    {
        var (calendar, _) = CreateCalendar();

        // Wednesday 2026-09-09:
        // 06:30 UTC = 09:30 Istanbul (before open at 10:00)
        var beforeOpenUtc = new DateTime(2026, 9, 9, 6, 30, 0, DateTimeKind.Utc);
        Assert.False(calendar.IsMarketOpen(beforeOpenUtc));

        // 08:30 UTC = 11:30 Istanbul (during session 10:00 - 18:00)
        var duringSessionUtc = new DateTime(2026, 9, 9, 8, 30, 0, DateTimeKind.Utc);
        Assert.True(calendar.IsMarketOpen(duringSessionUtc));

        // 15:30 UTC = 18:30 Istanbul (after close at 18:00)
        var afterCloseUtc = new DateTime(2026, 9, 9, 15, 30, 0, DateTimeKind.Utc);
        Assert.False(calendar.IsMarketOpen(afterCloseUtc));

        // Saturday 08:30 UTC -> Market closed
        var saturdayUtc = new DateTime(2026, 9, 12, 8, 30, 0, DateTimeKind.Utc);
        Assert.False(calendar.IsMarketOpen(saturdayUtc));
    }

    [Fact]
    public void CandleBoundaries_M15_H1_Daily_AccuratelyComputed()
    {
        var (calendar, _) = CreateCalendar();

        // Wednesday 2026-09-09 at 10:20 Istanbul = 07:20 UTC:
        // First M15 candle (10:00 - 10:15) has closed. Open timestamp should be 10:00 Istanbul = 07:00 UTC.
        var at1020Utc = new DateTime(2026, 9, 9, 7, 20, 0, DateTimeKind.Utc);
        var lastM15 = calendar.GetLastClosedCandleTimeUtc(Timeframe.M15, at1020Utc);
        Assert.NotNull(lastM15);
        Assert.Equal(new DateTime(2026, 9, 9, 7, 0, 0, DateTimeKind.Utc), lastM15.Value);

        // Wednesday 2026-09-09 at 11:05 Istanbul = 08:05 UTC:
        // First H1 candle (10:00 - 11:00) has closed. Open timestamp should be 10:00 Istanbul = 07:00 UTC.
        var at1105Utc = new DateTime(2026, 9, 9, 8, 5, 0, DateTimeKind.Utc);
        var lastH1 = calendar.GetLastClosedCandleTimeUtc(Timeframe.H1, at1105Utc);
        Assert.NotNull(lastH1);
        Assert.Equal(new DateTime(2026, 9, 9, 7, 0, 0, DateTimeKind.Utc), lastH1.Value);

        // Wednesday 2026-09-09 at 18:20 Istanbul = 15:20 UTC (after daily finalization 18:15):
        var at1820Utc = new DateTime(2026, 9, 9, 15, 20, 0, DateTimeKind.Utc);
        var lastDailyFinalized = calendar.GetLastClosedCandleTimeUtc(Timeframe.Daily, at1820Utc);
        Assert.NotNull(lastDailyFinalized);
        // Closed daily candle date is Wednesday 2026-09-09
        Assert.Equal(new DateTime(2026, 9, 8, 21, 0, 0, DateTimeKind.Utc), lastDailyFinalized.Value); // 2026-09-09 00:00 Istanbul in UTC

        // Sunday 2026-09-13: Expected latest Daily bar is Friday 2026-09-11
        var sundayUtc = new DateTime(2026, 9, 13, 12, 0, 0, DateTimeKind.Utc);
        var expectedSundayDaily = calendar.ExpectedLatestBarTimeUtc(Timeframe.Daily, sundayUtc);
        Assert.Equal(new DateTime(2026, 9, 10, 21, 0, 0, DateTimeKind.Utc), expectedSundayDaily); // 2026-09-11 00:00 Istanbul in UTC
    }

    [Fact]
    public void AlertCooldown_DeliberateMappingForAllTimeframes()
    {
        Assert.Equal(TimeSpan.FromMinutes(1), AlertEngine.GetCooldownForTimeframe(Timeframe.M1));
        Assert.Equal(TimeSpan.FromMinutes(5), AlertEngine.GetCooldownForTimeframe(Timeframe.M5));
        Assert.Equal(TimeSpan.FromMinutes(15), AlertEngine.GetCooldownForTimeframe(Timeframe.M15));
        Assert.Equal(TimeSpan.FromMinutes(30), AlertEngine.GetCooldownForTimeframe(Timeframe.M30));
        Assert.Equal(TimeSpan.FromHours(1), AlertEngine.GetCooldownForTimeframe(Timeframe.H1));
        Assert.Equal(TimeSpan.FromHours(4), AlertEngine.GetCooldownForTimeframe(Timeframe.H4));
        Assert.Equal(TimeSpan.FromHours(8), AlertEngine.GetCooldownForTimeframe(Timeframe.Daily));
        Assert.Equal(TimeSpan.FromDays(2), AlertEngine.GetCooldownForTimeframe(Timeframe.Weekly));
    }

    [Fact]
    public void SignalExpiration_DeliberateMappingForAllTimeframes()
    {
        var now = new DateTime(2026, 9, 8, 12, 0, 0, DateTimeKind.Utc);

        Assert.Equal(now.AddMinutes(15), SignalEngine.CalculateExpiration(Timeframe.M1, now));
        Assert.Equal(now.AddMinutes(45), SignalEngine.CalculateExpiration(Timeframe.M5, now));
        Assert.Equal(now.AddHours(2), SignalEngine.CalculateExpiration(Timeframe.M15, now));
        Assert.Equal(now.AddHours(4), SignalEngine.CalculateExpiration(Timeframe.M30, now));
        Assert.Equal(now.AddHours(8), SignalEngine.CalculateExpiration(Timeframe.H1, now));
        Assert.Equal(now.AddHours(24), SignalEngine.CalculateExpiration(Timeframe.H4, now));
        Assert.Equal(now.AddDays(2), SignalEngine.CalculateExpiration(Timeframe.Daily, now));
        Assert.Equal(now.AddDays(7), SignalEngine.CalculateExpiration(Timeframe.Weekly, now));
    }
}
