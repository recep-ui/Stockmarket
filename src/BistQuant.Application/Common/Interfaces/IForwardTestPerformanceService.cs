using BistQuant.Application.DTOs.ForwardTesting;
using BistQuant.Domain.Entities;

namespace BistQuant.Application.Common.Interfaces;

public interface IForwardTestPerformanceService
{
    Task<ForwardTestPerformanceDto?> GetPerformanceAsync(long portfolioId, CancellationToken cancellationToken = default);
    Task<ForwardTestDailyReport> GenerateDailyReportAsync(long portfolioId, DateOnly sessionDate, int symbolsAnalyzed, int filledCount, CancellationToken cancellationToken = default);
    Task SendDailyTelegramSummaryAsync(ForwardTestDailyReport report, CancellationToken cancellationToken = default);
}
