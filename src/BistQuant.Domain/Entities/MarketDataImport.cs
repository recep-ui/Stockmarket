using BistQuant.Domain.Common;
using BistQuant.Domain.Enums;

namespace BistQuant.Domain.Entities;

public class MarketDataImport : BaseEntity<long>
{
    public string Provider { get; set; } = string.Empty;
    public DateOnly SessionDate { get; set; }
    public string SourceFileName { get; set; } = string.Empty;
    public string? SourceUrl { get; set; }
    public DateTime DownloadedAt { get; set; } = DateTime.UtcNow;
    public string Sha256 { get; set; } = string.Empty;
    public long ContentLength { get; set; }
    public MarketDataImportStatus Status { get; set; } = MarketDataImportStatus.Pending;

    public int RowsRead { get; set; }
    public int RowsAccepted { get; set; }
    public int RowsRejected { get; set; }
    public int PriceBarsInserted { get; set; }
    public int PriceBarsUpdated { get; set; }

    public string SchemaVersion { get; set; } = "1.14";
    public string? ErrorMessage { get; set; }

    // Revision audit fields
    public int RevisionNumber { get; set; } = 1;
    public bool IsRevision { get; set; }
    public long? SupersedesImportId { get; set; }
    public MarketDataImport? SupersedesImport { get; set; }
    public bool IsCurrent { get; set; } = false;

    // Fetch and attempt metadata
    public DateTime? LastAttemptAt { get; set; }
    public DateTime? NextAttemptAt { get; set; }
    public int AttemptCount { get; set; }
    public int? LastHttpStatus { get; set; }
    public BulletinDownloadStatus? DownloadStatus { get; set; }
    public MarketDataOrigin DataOrigin { get; set; } = MarketDataOrigin.OfficialBistBulletin;
}

