using BistQuant.Application.Common.Interfaces;
using Microsoft.Extensions.Configuration;

namespace BistQuant.Application.Services.MarketData;

public class ConfigurableHolidayCalendar : IHolidayCalendar
{
    private readonly HashSet<DateOnly> _holidays = new();

    public ConfigurableHolidayCalendar(IConfiguration? configuration = null, IEnumerable<DateOnly>? explicitHolidays = null)
    {
        if (explicitHolidays != null)
        {
            foreach (var h in explicitHolidays)
            {
                _holidays.Add(h);
            }
        }

        if (configuration != null)
        {
            var configHolidays = configuration.GetSection("BistSession:Holidays").Get<string[]>();
            if (configHolidays != null)
            {
                foreach (var s in configHolidays)
                {
                    if (DateOnly.TryParse(s, out var d))
                    {
                        _holidays.Add(d);
                    }
                }
            }
        }
    }

    public bool IsHoliday(DateOnly date) => _holidays.Contains(date);

    public IReadOnlyCollection<DateOnly> GetHolidays() => _holidays;
}
