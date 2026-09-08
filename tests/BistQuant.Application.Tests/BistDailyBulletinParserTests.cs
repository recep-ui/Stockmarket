using System.Text;
using BistQuant.Application.Services.MarketData;
using Xunit;

namespace BistQuant.Application.Tests;

public class BistDailyBulletinParserTests
{
    private readonly BistDailyBulletinParser _parser = new();

    private const string StandardHeader =
        "TARIH;ISLEM KODU;BULTEN ADI;PAZAR ALT SEGMENTI;PAZAR;PIYASA;ENSTRUMAN GRUBU;ENSTRUMAN TIPI;ENSTRUMAN SINIFI;ISLEM YONTEMI;PIYASA YAPICI;BIST 100;BIST 30;BRUT TAKAS;OZSERMAYE HALLERI;DURDURMA;ONCEKI KAPANIS FIYATI;ACILIS FIYATI;ACILIS SEANSI FIYATI;GUNORTASI FIYATI;EN DUSUK FIYAT;EN YUKSEK FIYAT;KAPANIS FIYATI;KAPANIS SEANSI FIYATI;FIYAT DEGISIMI (%);KALAN ALIS;KALAN SATIS;A.O.F;TOPLAM ISLEM HACMI;TOPLAM ISLEM ADEDI;SOZLESME SAYISI;REFERANS FIYAT\n";

    [Fact]
    public void Parse_ValidEquityRow_CorrectlyParsesAndStripsSuffix()
    {
        var csv = StandardHeader +
            "2026-03-18;THYAO.E;TURK HAVA YOLLARI;A;Z;MSPOT;EQT;MSPOTEQT;MSPOTEQTTHYAO;SI;0;1;1;0;;0;310.0;312.5;312.5;0;308.0;316.0;314.5;314.5;1.45;314.0;314.5;312.8;1564000000.0;5000000.0;45000;310.0\n" +
            "2026-03-18;ASELS.E;ASELSAN ELEKTRONIK;A;Z;MSPOT;EQT;MSPOTEQT;MSPOTEQTASELS;SI;0;1;1;0;;0;65.0;65.5;65.5;0;64.8;67.2;66.8;66.8;2.77;66.7;66.8;66.1;850000000.0;12800000.0;38000;65.0\n";

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));
        var result = _parser.Parse(stream);

        Assert.Equal(new DateOnly(2026, 3, 18), result.SessionDate);
        Assert.Equal(2, result.AcceptedRows);
        Assert.Equal(2, result.Records.Count);

        var thyao = result.Records[0];
        Assert.Equal("THYAO.E", thyao.SeriesCode);
        Assert.Equal("THYAO", thyao.Ticker);
        Assert.Equal("TURK HAVA YOLLARI", thyao.InstrumentName);
        Assert.Equal("EQT", thyao.InstrumentGroup);
        Assert.True(thyao.IsBist100);
        Assert.True(thyao.IsBist30);
        Assert.False(thyao.Suspended);
        Assert.Equal(310.0m, thyao.PreviousLastPrice);
        Assert.Equal(312.5m, thyao.Open);
        Assert.Equal(316.0m, thyao.High);
        Assert.Equal(308.0m, thyao.Low);
        Assert.Equal(314.5m, thyao.Close);
        Assert.Equal(5000000.0m, thyao.TotalTradedVolume);
        Assert.True(thyao.HasValidOhlc);

        var asels = result.Records[1];
        Assert.Equal("ASELS", asels.Ticker);
        Assert.Equal(65.5m, asels.Open);
        Assert.Equal(66.8m, asels.Close);
        Assert.True(asels.HasValidOhlc);
    }

    [Fact]
    public void Parse_HeaderlessCsv_ReturnsSchemaMismatch_WhenHeaderRequired()
    {
        var csv = "2026-03-18;THYAO.E;TURK HAVA YOLLARI;A;Z;MSPOT;EQT;MSPOTEQT;MSPOTEQTTHYAO;SI;0;1;1;0;;0;310.0;312.0;312.0;0;310.0;315.0;314.0;314.0;1.29;0;0;312.0;1000000;3000;50;310.0\n";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));
        var result = _parser.Parse(stream, requireRecognizedHeader: true);

        Assert.True(result.IsSchemaMismatch, "Automatic ingestion must fail closed with SchemaMismatch when header is missing.");
        Assert.Contains("Official recognized BIST bulletin header row is required", result.ParseErrors[0]);
    }

    [Fact]
    public void Parse_MissingVolumeHeader_ReturnsSchemaMismatch()
    {
        // Header that has TOTAL TRADED VALUE but missing TOTAL TRADED VOLUME
        var csv =
            "TARIH;ISLEM KODU;BULTEN ADI;PAZAR ALT SEGMENTI;PAZAR;PIYASA;ENSTRUMAN GRUBU;ENSTRUMAN TIPI;ENSTRUMAN SINIFI;ISLEM YONTEMI;PIYASA YAPICI;BIST 100;BIST 30;BRUT TAKAS;OZSERMAYE HALLERI;DURDURMA;ONCEKI KAPANIS FIYATI;ACILIS FIYATI;ACILIS SEANSI FIYATI;GUNORTASI FIYATI;EN DUSUK FIYAT;EN YUKSEK FIYAT;KAPANIS FIYATI;KAPANIS SEANSI FIYATI;FIYAT DEGISIMI (%);KALAN ALIS;KALAN SATIS;A.O.F;TOPLAM ISLEM HACMI;SOZLESME SAYISI;REFERANS FIYAT;EKSTRA\n" +
            "2026-03-18;THYAO.E;TURK HAVA YOLLARI;A;Z;MSPOT;EQT;MSPOTEQT;MSPOTEQTTHYAO;SI;0;1;1;0;;0;310.0;312.0;312.0;0;310.0;315.0;314.0;314.0;1.29;0;0;312.0;1000000;50;310.0;0\n";

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));
        var result = _parser.Parse(stream, requireRecognizedHeader: true);

        Assert.True(result.IsSchemaMismatch);
    }

    [Fact]
    public void Parse_NonEquityInstruments_StrictlyExcluded()
    {
        var csv = StandardHeader +
            "2026-03-18;THYAO.E;TURK HAVA YOLLARI;A;Z;MSPOT;EQT;MSPOTEQT;MSPOTEQTTHYAO;SI;0;1;1;0;;0;310.0;312.0;312.0;0;310.0;315.0;314.0;314.0;1.29;0;0;312.0;1000000;3000;50;310.0\n" +
            "2026-03-18;THYAA.V;THYAO VARANT;A;Z;MSPOT;ECW;MSPOTECW;MSPOTECWTHYAA;SI;0;0;0;0;;0;1.5;1.6;0;0;1.4;1.7;1.6;0;6.67;0;0;1.55;15500;10000;10;1.5\n" +
            "2026-03-18;XU100;BIST 100 ENDEKS;A;Z;MSPOT;SP;SPOTINDEX;SPOTINDEXTURKEY;SI;0;0;0;0;;0;10000;10050;0;0;9980;10100;10080;0;0.80;0;0;0;0;0;0;10000\n";

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));
        var result = _parser.Parse(stream);

        Assert.Single(result.Records);
        Assert.Equal("THYAO", result.Records[0].Ticker);
        Assert.Equal(2, result.RejectedRows);
    }

    [Fact]
    public void Parse_SuspendedStock_MarkedSuspendedAndInvalidOhlc()
    {
        var csv = StandardHeader +
            "2026-03-18;SUSPD.E;SUSPENDED SIRKET;A;Z;MSPOT;EQT;MSPOTEQT;MSPOTEQTSUSPD;SI;0;0;0;0;;1;50.0;0;0;0;0;0;0;0;0;0;0;0;0;0;0;50.0\n";

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));
        var result = _parser.Parse(stream);

        Assert.Single(result.Records);
        var record = result.Records[0];
        Assert.Equal("SUSPD", record.Ticker);
        Assert.True(record.Suspended);
        Assert.False(record.HasValidOhlc, "Suspended stocks must not emit valid OHLC bars.");
    }

    [Fact]
    public void Parse_NullOrMissingVolume_MarksHasValidOhlcFalse()
    {
        // TotalTradedVolume is empty (column 29)
        var csv = StandardHeader +
            "2026-03-18;NOVOL.E;NO VOLUME STOCK;A;Z;MSPOT;EQT;MSPOTEQT;MSPOTEQTNOVOL;SI;0;0;0;0;;0;100.0;101.0;101.0;0;100.0;102.0;101.5;101.5;1.5;0;0;101.2;500000;;20;100.0\n";

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));
        var result = _parser.Parse(stream);

        Assert.Single(result.Records);
        var record = result.Records[0];
        Assert.Equal("NOVOL", record.Ticker);
        Assert.Null(record.TotalTradedVolume);
        Assert.False(record.HasValidOhlc, "Missing volume must NOT fabricate 0 volume into a valid PriceBar.");
    }

    [Fact]
    public void Parse_ZeroTradeOrAbnormalOhlc_DoesNotEmitValidOhlc()
    {
        var csv = StandardHeader +
            "2026-03-18;NOTRD.E;NO TRADE STOCK;A;Z;MSPOT;EQT;MSPOTEQT;MSPOTEQTNOTRD;SI;0;0;0;0;;0;20.0;0;0;0;0;0;0;0;0;0;0;0;0;0;0;20.0\n" +
            "2026-03-18;BADBR.E;BAD BAR STOCK;A;Z;MSPOT;EQT;MSPOTEQT;MSPOTEQTBADBR;SI;0;0;0;0;;0;20.0;25.0;0;30.0;20.0;22.0;0;0;0;0;0;22.0;1000;100;1;20.0\n";

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));
        var result = _parser.Parse(stream);

        Assert.Equal(2, result.Records.Count);
        Assert.False(result.Records[0].HasValidOhlc, "Zero price bar must be marked invalid.");
        Assert.False(result.Records[1].HasValidOhlc, "High < Low bar must be marked invalid.");
    }

    [Fact]
    public void Parse_CorporateActionWithCommaDecimal_CorrectlyParsed()
    {
        var csv = StandardHeader +
            "2026-03-18;SPLIT.E;SPLIT CORP;A;Z;MSPOT;EQT;MSPOTEQT;MSPOTEQTSPLIT;SI;0;0;0;0;BDL;0;100,0;50,0;50,0;0;48,0;52,0;51,0;51,0;2,0;0;0;50,5;505000;10000;20;0\n";

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));
        var result = _parser.Parse(stream);

        Assert.Single(result.Records);
        var rec = result.Records[0];
        Assert.Equal("BDL", rec.CorporateAction);
        Assert.Equal(100.0m, rec.PreviousLastPrice);
        Assert.Equal(50.0m, rec.Open);
        Assert.Equal(51.0m, rec.Close);
        Assert.True(rec.HasValidOhlc);
    }

    [Theory]
    [InlineData("315.25", 315.25)]
    [InlineData("1,234.56", 1234.56)]
    [InlineData("109,723.61", 109723.61)]
    [InlineData("5,000,000", 5000000)]
    [InlineData("5,000", 5000)] // Crucial: must NOT be misinterpreted as 5.0
    [InlineData("100,50", 100.50)] // Turkish comma decimal fallback
    public void ParseDecimal_StrictNumericParsing_HandlesAllExpectedFormatsUnambiguously(string input, decimal expected)
    {
        var actual = BistDailyBulletinParser.ParseDecimal(input);
        Assert.NotNull(actual);
        Assert.Equal(expected, actual!.Value);
    }
}
