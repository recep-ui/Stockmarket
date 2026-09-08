using BistQuant.Domain.Common;

namespace BistQuant.Domain.Entities;

public class IndicatorContinuityWarning : BaseEntity<long>
{
    public int SymbolId { get; set; }
    public Symbol Symbol { get; set; } = null!;

    public DateOnly SessionDate { get; set; }
    public string CorporateActionRaw { get; set; } = string.Empty;
    public decimal PreviousCloseReported { get; set; }
    public decimal PreviousRawCloseInDb { get; set; }
    public string WarningMessage { get; set; } = string.Empty;
    public bool IsAcknowledged { get; set; }
}
