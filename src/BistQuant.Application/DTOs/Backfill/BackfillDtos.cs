using BistQuant.Domain.Enums;

namespace BistQuant.Application.DTOs.Backfill;

public record CreateBackfillJobRequest(
    DateOnly StartDate,
    DateOnly EndDate,
    bool ForceRevisionCheck = false
);

public record BackfillJobDto(
    long Id,
    DateOnly StartDate,
    DateOnly EndDate,
    DateOnly? CurrentDate,
    BackfillJobStatus Status,
    int SessionsTotal,
    int SessionsCompleted,
    int SessionsSkipped,
    int SessionsFailed,
    int BarsInserted,
    DateTime StartedAt,
    DateTime? CompletedAt,
    string? LastError,
    long? CreatedByUserId
);
