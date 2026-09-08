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
}
