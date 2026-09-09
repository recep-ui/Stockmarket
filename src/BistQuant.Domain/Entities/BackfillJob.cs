using BistQuant.Domain.Common;
using BistQuant.Domain.Enums;

namespace BistQuant.Domain.Entities;

public class BackfillJob : BaseEntity<long>
{
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public DateOnly? CurrentDate { get; set; }
    public BackfillJobStatus Status { get; set; } = BackfillJobStatus.Pending;

    public int SessionsTotal { get; set; }
    public int SessionsCompleted { get; set; }
    public int SessionsSkipped { get; set; }
    public int SessionsFailed { get; set; }
    public int BarsInserted { get; set; }

    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
    public string? LastError { get; set; }
    public long? CreatedByUserId { get; set; }
}
