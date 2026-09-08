namespace BistQuant.Application.Services.MarketData;

/// <summary>
/// Explicit metadata definition for Borsa İstanbul bulletin schemas.
/// Facilitates strict schema validation and prevents silent misparsing when exchange layouts evolve.
/// </summary>
public record BistBulletinSchemaDefinition(
    string Version,
    IReadOnlyList<string> RequiredHeaders,
    IReadOnlyList<string> RequiredHeadersTr,
    IReadOnlyDictionary<string, int> ColumnMappings,
    int MinimumColumnCount,
    string Description
)
{
    public static readonly BistBulletinSchemaDefinition V114 = new(
        Version: "1.14",
        RequiredHeaders: new[]
        {
            "DATE",
            "SERIES CODE",
            "INSTRUMENT GROUP",
            "OPENING PRICE",
            "LOWEST PRICE",
            "HIGHEST PRICE",
            "CLOSING PRICE",
            "TOTAL TRADED VOLUME"
        },
        RequiredHeadersTr: new[]
        {
            "TARIH",
            "KODU",
            "ENSTRUMAN GRUBU",
            "ACILIS FIYATI",
            "EN DUSUK FIYAT",
            "EN YUKSEK FIYAT",
            "KAPANIS FIYATI",
            "TOPLAM ISLEM ADEDI"
        },
        ColumnMappings: new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["Date"] = BistBulletinSchemaV114.Date,
            ["SeriesCode"] = BistBulletinSchemaV114.SeriesCode,
            ["InstrumentName"] = BistBulletinSchemaV114.InstrumentName,
            ["MarketSubSegment"] = BistBulletinSchemaV114.MarketSubSegment,
            ["MarketSegment"] = BistBulletinSchemaV114.MarketSegment,
            ["Market"] = BistBulletinSchemaV114.Market,
            ["InstrumentGroup"] = BistBulletinSchemaV114.InstrumentGroup,
            ["InstrumentType"] = BistBulletinSchemaV114.InstrumentType,
            ["InstrumentClass"] = BistBulletinSchemaV114.InstrumentClass,
            ["TradingMethod"] = BistBulletinSchemaV114.TradingMethod,
            ["MarketMaker"] = BistBulletinSchemaV114.MarketMaker,
            ["Bist100Index"] = BistBulletinSchemaV114.Bist100Index,
            ["Bist30Index"] = BistBulletinSchemaV114.Bist30Index,
            ["GrossSettlement"] = BistBulletinSchemaV114.GrossSettlement,
            ["CorporateAction"] = BistBulletinSchemaV114.CorporateAction,
            ["Suspended"] = BistBulletinSchemaV114.Suspended,
            ["PreviousLastPrice"] = BistBulletinSchemaV114.PreviousLastPrice,
            ["Open"] = BistBulletinSchemaV114.Open,
            ["OpeningSessionPrice"] = BistBulletinSchemaV114.OpeningSessionPrice,
            ["MiddayPrice"] = BistBulletinSchemaV114.MiddayPrice,
            ["Low"] = BistBulletinSchemaV114.Low,
            ["High"] = BistBulletinSchemaV114.High,
            ["Close"] = BistBulletinSchemaV114.Close,
            ["ClosingSessionPrice"] = BistBulletinSchemaV114.ClosingSessionPrice,
            ["ChangePercent"] = BistBulletinSchemaV114.ChangePercent,
            ["RemainingBid"] = BistBulletinSchemaV114.RemainingBid,
            ["RemainingAsk"] = BistBulletinSchemaV114.RemainingAsk,
            ["Vwap"] = BistBulletinSchemaV114.Vwap,
            ["TotalTradedValue"] = BistBulletinSchemaV114.TotalTradedValue,
            ["TotalTradedVolume"] = BistBulletinSchemaV114.TotalTradedVolume,
            ["TotalNumberOfContracts"] = BistBulletinSchemaV114.TotalNumberOfContracts,
            ["ReferencePrice"] = BistBulletinSchemaV114.ReferencePrice
        },
        MinimumColumnCount: BistBulletinSchemaV114.MinimumColumnCount,
        Description: "Official Borsa İstanbul Equity Market Daily Bulletin v1.14 specification."
    );
}
