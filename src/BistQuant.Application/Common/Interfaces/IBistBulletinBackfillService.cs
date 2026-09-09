using BistQuant.Application.DTOs.Backfill;

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
    Task<BackfillJobDto> StartBackfillAsync(DateOnly startDate, DateOnly endDate, bool forceRevisionCheck = false, long? userId = null, CancellationToken cancellationToken = default);
    Task<BackfillJobDto?> GetBackfillJobAsync(long jobId, CancellationToken cancellationToken = default);
    Task<List<BackfillJobDto>> GetBackfillJobsAsync(CancellationToken cancellationToken = default);
    Task<bool> PauseBackfillAsync(long jobId, CancellationToken cancellationToken = default);
    Task<bool> ResumeBackfillAsync(long jobId, CancellationToken cancellationToken = default);
    Task<bool> CancelBackfillAsync(long jobId, CancellationToken cancellationToken = default);
    Task<BackfillProgress> BackfillRangeAsync(
        DateOnly startDate,
        DateOnly endDate,
        bool forceRefresh = false,
        IProgress<BackfillProgress>? progress = null,
        CancellationToken cancellationToken = default);
}
