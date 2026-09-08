using BistQuant.Domain.Common;

namespace BistQuant.Domain.Entities;

public class DailyInstrumentMarketStats : BaseEntity<long>
{
    public int SymbolId { get; set; }
    public Symbol Symbol { get; set; } = null!;

    public DateOnly SessionDate { get; set; }

    public decimal? PreviousLastPrice { get; set; }
    public decimal? ClosingSessionPrice { get; set; }
    public decimal? ChangePercent { get; set; }
    public decimal? Vwap { get; set; }
    public decimal? TotalTradedValue { get; set; }
    public decimal? TotalTradedVolume { get; set; }
    public long? TotalNumberOfContracts { get; set; }

    public bool Suspended { get; set; }
    public string? CorporateActionRaw { get; set; }
    public string? MarketSegment { get; set; }
    public string? TradingMethod { get; set; }

    public long? SourceImportId { get; set; }
    public MarketDataImport? SourceImport { get; set; }
}
