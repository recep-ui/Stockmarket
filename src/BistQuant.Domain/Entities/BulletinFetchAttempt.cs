using BistQuant.Domain.Common;
using BistQuant.Domain.Enums;

namespace BistQuant.Domain.Entities;

public class BulletinFetchAttempt : BaseEntity<long>
{
    public DateOnly SessionDate { get; set; }
    public DateTime AttemptedAt { get; set; } = DateTime.UtcNow;
    public DateTime AttemptedAtUtc { get => AttemptedAt; set => AttemptedAt = value; }
    public DateTime? NextAttemptAt { get; set; }
    public int AttemptCount { get; set; }
    public BulletinDownloadStatus Status { get; set; }
    public int? HttpStatusCode { get; set; }
    public int? HttpStatus { get => HttpStatusCode; set => HttpStatusCode = value; }
    public string? SourceUrl { get; set; }
    public string? ErrorMessage { get; set; }
    public long? ElapsedMs { get; set; }
    public long? ContentLength { get; set; }
    public string? Sha256 { get; set; }
}
