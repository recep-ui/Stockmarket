namespace BistQuant.Application.Services.MarketData;

public static class BistBulletinSchemaV114
{
    public const string Version = "1.14";

    // Standard 0-based column constants per official BIST specification v1.14:
    // 1 DATE 2 INSTRUMENT SERIES CODE ... 18 OPENING PRICE 19 OPENING SESSION PRICE 20 MIDDAY PRICE
    // 21 LOWEST PRICE 22 HIGHEST PRICE 23 CLOSING PRICE 24 CLOSING SESSION PRICE 25 CHANGE ...
    // 28 VWAP 29 TOTAL TRADED VALUE 30 TOTAL TRADED VOLUME 31 TOTAL NUMBER OF CONTRACTS 32 REFERENCE PRICE
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
    public const int MiddayPrice = 19;
    public const int Low = 20;
    public const int High = 21;
    public const int Close = 22;
    public const int ClosingSessionPrice = 23;
    public const int ChangePercent = 24;
    public const int RemainingBid = 25;
    public const int RemainingAsk = 26;
    public const int Vwap = 27;
    public const int TotalTradedValue = 28;
    public const int TotalTradedVolume = 29;
    public const int TotalNumberOfContracts = 30;
    public const int ReferencePrice = 31;

    public const int MinimumColumnCount = 31;

    // Expected required header concepts (English & Turkish)
    // Strict separation: Volume (shares/lots) is required and cannot be satisfied by Value (TL)
    public static readonly string[] RequiredHeaderConcepts = new[]
    {
        "DATE",
        "SERIES CODE",
        "INSTRUMENT GROUP",
        "OPENING PRICE",
        "LOWEST PRICE",
        "HIGHEST PRICE",
        "CLOSING PRICE",
        "TOTAL TRADED VOLUME"
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
        "TOPLAM ISLEM ADEDI"
    };
}
