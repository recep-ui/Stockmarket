namespace BistQuant.Application.Common.Interfaces;

public interface IHolidayCalendar
{
    bool IsHoliday(DateOnly date);
    IReadOnlyCollection<DateOnly> GetHolidays();
}
