using BistQuant.Domain.Common;

namespace BistQuant.Domain.Entities;

public class Market : BaseEntity<int>
{
    public string Code { get; set; } = string.Empty; // e.g. "BIST"
    public string Name { get; set; } = string.Empty; // e.g. "Borsa Istanbul"
    public string Country { get; set; } = "Turkey";
    public string Currency { get; set; } = "TRY";
    public string Timezone { get; set; } = "Europe/Istanbul";

    public ICollection<Symbol> Symbols { get; set; } = new List<Symbol>();
}

public class Symbol : BaseEntity<int>
{
    public int MarketId { get; set; }
    public Market Market { get; set; } = null!;

    public string Ticker { get; set; } = string.Empty; // e.g. "THYAO"
    public string Name { get; set; } = string.Empty;   // e.g. "Turk Hava Yollari"
    public string Sector { get; set; } = string.Empty; // e.g. "Transportation"
    public string Industry { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;

    public ICollection<PriceBar> PriceBars { get; set; } = new List<PriceBar>();
    public ICollection<IndicatorSnapshot> IndicatorSnapshots { get; set; } = new List<IndicatorSnapshot>();
    public ICollection<Signal> Signals { get; set; } = new List<Signal>();
}
