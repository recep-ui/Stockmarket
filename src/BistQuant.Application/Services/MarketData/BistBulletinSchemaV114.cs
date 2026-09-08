namespace BistQuant.Application.Services.MarketData;

public static class BistBulletinSchemaV114
{
    public const string Version = "1.14";

    // Standard 0-based column constants per official BIST specification v1.14
    public const int Date = 0;
    public const int SeriesCode = 1;
    public const int InstrumentName = 2;
    public const int MarketSubSegment = 3;
    public const int MarketSegment = 4;
    public const int Market = 5;
    public const int InstrumentGroup = 6;
    public const int InstrumentType = 7;
    public const int InstrumentClass = 8;
    public const int TradingMethod = 9;
    public const int MarketMaker = 10;
    public const int Bist100Index = 11;
    public const int Bist30Index = 12;
    public const int GrossSettlement = 13;
    public const int CorporateAction = 14;
    public const int Suspended = 15;
    public const int PreviousLastPrice = 16;
    public const int Open = 17;
    public const int OpeningSessionPrice = 18;
    public const int Low = 19; // In official v1.14 thb format index 19 is LOWEST PRICE
    public const int High = 20; // In official v1.14 thb format index 20 is HIGHEST PRICE
    public const int Close = 21; // In official v1.14 thb format index 21 is CLOSING PRICE
    public const int ClosingSessionPrice = 22;
    public const int ChangePercent = 23;
    public const int RemainingBid = 24;
    public const int RemainingAsk = 25;
    public const int Vwap = 26;
    public const int TotalTradedValue = 27;
    public const int TotalTradedVolume = 28;
    public const int TotalNumberOfContracts = 29;
    public const int ReferencePrice = 30;

    public const int MinimumColumnCount = 31;

    // Alternate standard positions when MIDDAY PRICE exists (older 57-column variant)
    public const int AlternateLow = 20;
    public const int AlternateHigh = 21;
    public const int AlternateClose = 22;
    public const int AlternateClosingSessionPrice = 23;
    public const int AlternateChangePercent = 24;
    public const int AlternateVwap = 27;
    public const int AlternateTotalTradedValue = 28;
    public const int AlternateTotalTradedVolume = 29;
    public const int AlternateTotalNumberOfContracts = 30;

    // Expected required header concepts (English & Turkish)
    public static readonly string[] RequiredHeaderConcepts = new[]
    {
        "DATE",
        "SERIES CODE",
        "INSTRUMENT GROUP",
        "OPENING PRICE",
        "LOWEST PRICE",
        "HIGHEST PRICE",
        "CLOSING PRICE",
        "VOLUME"
    };

    public static readonly string[] RequiredHeaderConceptsTr = new[]
    {
        "TARIH",
        "KODU",
        "ENSTRUMAN GRUBU",
        "ACILIS FIYATI",
        "EN DUSUK FIYAT",
        "EN YUKSEK FIYAT",
        "KAPANIS FIYATI",
        "HACMI"
    };
}
