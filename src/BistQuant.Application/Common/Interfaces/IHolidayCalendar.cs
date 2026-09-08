namespace BistQuant.Application.Common.Interfaces;

public interface IHolidayCalendar
{
    bool IsHoliday(DateOnly date);
    bool IsHalfDay(DateOnly date);
    MarketSessionOverride? GetOverride(DateOnly date);
    IReadOnlyCollection<DateOnly> GetHolidays();
    IReadOnlyCollection<MarketSessionOverride> GetOverrides();
}
