namespace BistQuant.Application.Common.Interfaces;

public record BackfillProgress(
    DateOnly StartDate,
    DateOnly EndDate,
    int TotalDays,
    int ProcessedDays,
    int SuccessCount,
    int SkippedCount,
    int FailedCount,
    string? CurrentStage
);

public interface IBistBulletinBackfillService
{
    Task<BackfillProgress> BackfillRangeAsync(
        DateOnly startDate,
        DateOnly endDate,
        bool forceRefresh = false,
        IProgress<BackfillProgress>? progress = null,
        CancellationToken cancellationToken = default);
}
