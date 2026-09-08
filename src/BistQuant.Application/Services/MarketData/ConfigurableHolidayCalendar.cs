using BistQuant.Application.Common.Interfaces;
using Microsoft.Extensions.Configuration;

namespace BistQuant.Application.Services.MarketData;

public class ConfigurableHolidayCalendar : IHolidayCalendar
{
    private readonly Dictionary<DateOnly, MarketSessionOverride> _overrides = new();

    public ConfigurableHolidayCalendar(
        IConfiguration? configuration = null,
        IEnumerable<DateOnly>? explicitHolidays = null,
        IEnumerable<MarketSessionOverride>? explicitOverrides = null)
    {
        // 1. Seed official BIST 2026 holiday calendar
        SeedOfficial2026BistCalendar();

        // 2. Add explicit overrides if supplied
        if (explicitOverrides != null)
        {
            foreach (var ov in explicitOverrides)
            {
                _overrides[ov.Date] = ov;
            }
        }

        // 3. Add explicit closed holiday dates if supplied
        if (explicitHolidays != null)
        {
            foreach (var h in explicitHolidays)
            {
                _overrides[h] = new MarketSessionOverride(h, false, null, null, "Explicit Closed Holiday");
            }
        }

        // 4. Configuration overrides if present
        if (configuration != null)
        {
            var configHolidays = configuration.GetSection("BistSession:Holidays").Get<string[]>();
            if (configHolidays != null)
            {
                foreach (var s in configHolidays)
                {
                    if (DateOnly.TryParse(s, out var d))
                    {
                        _overrides[d] = new MarketSessionOverride(d, false, null, null, "Configured Holiday");
                    }
                }
            }

            var configOverrides = configuration.GetSection("BistSession:Overrides").GetChildren();
            foreach (var sec in configOverrides)
            {
                var dateStr = sec["Date"];
                if (DateOnly.TryParse(dateStr, out var d))
                {
                    var isTrading = bool.TryParse(sec["IsTradingDay"], out var it) ? it : false;
                    TimeSpan? open = TimeSpan.TryParse(sec["OpenTime"], out var ot) ? ot : null;
                    TimeSpan? close = TimeSpan.TryParse(sec["CloseTime"], out var ct) ? ct : null;
                    var reason = sec["Reason"] ?? "Configured Override";

                    _overrides[d] = new MarketSessionOverride(d, isTrading, open, close, reason);
                }
            }
        }
    }

    private void SeedOfficial2026BistCalendar()
    {
        // Official Closed Weekdays (2026)
        AddClosed(new DateOnly(2026, 1, 1), "Yılbaşı");
        AddClosed(new DateOnly(2026, 3, 20), "Ramazan Bayramı 1. Gün");
        AddClosed(new DateOnly(2026, 4, 23), "Ulusal Egemenlik ve Çocuk Bayramı");
        AddClosed(new DateOnly(2026, 5, 1), "Emek ve Dayanışma Günü");
        AddClosed(new DateOnly(2026, 5, 19), "Atatürk'ü Anma, Gençlik ve Spor Bayramı");
        AddClosed(new DateOnly(2026, 5, 27), "Kurban Bayramı 1. Gün");
        AddClosed(new DateOnly(2026, 5, 28), "Kurban Bayramı 2. Gün");
        AddClosed(new DateOnly(2026, 5, 29), "Kurban Bayramı 3. Gün");
        AddClosed(new DateOnly(2026, 7, 15), "15 Temmuz Demokrasi ve Milli Birlik Günü");
        AddClosed(new DateOnly(2026, 10, 29), "Cumhuriyet Bayramı");

        // Official Half-Days (2026) - Close at 13:00 Europe/Istanbul
        AddHalfDay(new DateOnly(2026, 3, 19), "Ramazan Bayramı Arefesi");
        AddHalfDay(new DateOnly(2026, 5, 26), "Kurban Bayramı Arefesi");
        AddHalfDay(new DateOnly(2026, 10, 28), "Cumhuriyet Bayramı Arefesi");
    }

    private void AddClosed(DateOnly date, string reason)
    {
        _overrides[date] = new MarketSessionOverride(date, false, null, null, reason);
    }

    private void AddHalfDay(DateOnly date, string reason)
    {
        _overrides[date] = new MarketSessionOverride(date, true, new TimeSpan(10, 0, 0), new TimeSpan(13, 0, 0), reason);
    }

    public bool IsHoliday(DateOnly date)
    {
        return _overrides.TryGetValue(date, out var ov) && !ov.IsTradingDay;
    }

    public bool IsHalfDay(DateOnly date)
    {
        return _overrides.TryGetValue(date, out var ov) && ov.IsTradingDay && ov.CloseTime.HasValue && ov.CloseTime.Value < new TimeSpan(18, 0, 0);
    }

    public MarketSessionOverride? GetOverride(DateOnly date)
    {
        return _overrides.TryGetValue(date, out var ov) ? ov : null;
    }

    public IReadOnlyCollection<DateOnly> GetHolidays()
    {
        return _overrides.Where(kv => !kv.Value.IsTradingDay).Select(kv => kv.Key).ToList();
    }

    public IReadOnlyCollection<MarketSessionOverride> GetOverrides()
    {
        return _overrides.Values.ToList();
    }
}
