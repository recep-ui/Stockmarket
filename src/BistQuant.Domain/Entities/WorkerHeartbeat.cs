using BistQuant.Domain.Common;
using BistQuant.Domain.Enums;

namespace BistQuant.Domain.Entities;

public class WorkerHeartbeat : BaseEntity<long>
{
    public string WorkerInstance { get; set; } = string.Empty;
    public string ScanType { get; set; } = "UniverseScan";
    public Timeframe Timeframe { get; set; } = Timeframe.Daily;
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
    public DateTime? ExpectedCandleClose { get; set; }
    public DateTime? DataTimestamp { get; set; }
    public bool Success { get; set; } = false;
    public int SymbolCount { get; set; } = 0;
    public string? ErrorMessage { get; set; }
}
