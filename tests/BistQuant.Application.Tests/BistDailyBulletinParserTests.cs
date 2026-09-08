using System.Text;
using BistQuant.Application.Services.MarketData;
using Xunit;

namespace BistQuant.Application.Tests;

public class BistDailyBulletinParserTests
{
    private readonly BistDailyBulletinParser _parser = new();

    [Fact]
    public void Parse_ValidEquityRow_CorrectlyParsesAndStripsSuffix()
    {
        // Sample standard bulletin rows with Turkish header, English header, and 2 data rows
        var csv =
            "TARIH;MENKUL KIYMET KODU;MENKUL KIYMET ADI;GRUP;PAZAR;PIYASA;MENKUL GRUBU;MENKUL TURU;MENKUL SINIFI;ISLEM YONTEMI;PIYASA YAPICI;BIST 100;BIST 30;BRUT TAKAS;SIRKET ISLEMI;ISLEM GORMEYEN;ONCEKI KAPANIS;ACILIS;ACILIS SEANSI;GUNORTASI;EN DUSUK;EN YUKSEK;KAPANIS;KAPANIS SEANSI;FIYAT DEG;KALAN ALIS;KALAN SATIS;AOF;TOPLAM ISLEM HACMI;TOPLAM ISLEM ADEDI;TOPLAM SOZLESME\n" +
            "DATE;INSTRUMENT SERIES CODE;INSTRUMENT NAME;GROUP;MARKET SEGMENT;MARKET;INSTRUMENT GROUP;INSTRUMENT TYPE;INSTRUMENT CLASS;TRADING METHOD;MARKET MAKER;BIST 100;BIST 30;GROSS SETTLEMENT;CORPORATE ACTION;SUSPENDED;PREVIOUS LAST PRICE;OPENING PRICE;OPENING SESSION PRICE;MIDDAY PRICE;LOWEST PRICE;HIGHEST PRICE;CLOSING PRICE;CLOSING SESSION PRICE;CHANGE;REMAINING BID;REMAINING ASK;VWAP;TOTAL TRADED VALUE;TOTAL TRADED VOLUME;TOTAL NUMBER OF CONTRACTS\n" +
            "2026-03-18;THYAO.E;TURK HAVA YOLLARI;A;Z;MSPOT;EQT;MSPOTEQT;MSPOTEQTTHYAO;SI;0;1;1;0;;0;310.0;312.5;312.5;0;308.0;316.0;314.5;314.5;1.45;314.0;314.5;312.8;1564000000.0;5000000.0;45000\n" +
            "2026-03-18;ASELS.E;ASELSAN ELEKTRONIK;A;Z;MSPOT;EQT;MSPOTEQT;MSPOTEQTASELS;SI;0;1;1;0;;0;65.0;65.5;65.5;0;64.8;67.2;66.8;66.8;2.77;66.7;66.8;66.1;850000000.0;12800000.0;38000\n";

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
    public void Parse_NonEquityInstruments_StrictlyExcluded()
    {
        // Include Warrants (ECW) and Spot Index (SP) alongside an Equity (EQT)
        var csv =
            "2026-03-18;THYAO.E;TURK HAVA YOLLARI;A;Z;MSPOT;EQT;MSPOTEQT;MSPOTEQTTHYAO;SI;0;1;1;0;;0;310.0;312.0;312.0;0;310.0;315.0;314.0;314.0;1.29;0;0;312.0;1000000;3000;50\n" +
            "2026-03-18;THYAA.V;THYAO VARANT;A;Z;MSPOT;ECW;MSPOTECW;MSPOTECWTHYAA;SI;0;0;0;0;;0;1.5;1.6;0;0;1.4;1.7;1.6;0;6.67;0;0;1.55;15500;10000;10\n" +
            "2026-03-18;XU100;BIST 100 ENDEKS;A;Z;MSPOT;SP;SPOTINDEX;SPOTINDEXTURKEY;SI;0;0;0;0;;0;10000;10050;0;0;9980;10100;10080;0;0.80;0;0;0;0;0;0\n";

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));
        var result = _parser.Parse(stream);

        // Only 1 accepted row (the EQT equity), warrants and index rejected
        Assert.Single(result.Records);
        Assert.Equal("THYAO", result.Records[0].Ticker);
        Assert.Equal(2, result.RejectedRows);
    }

    [Fact]
    public void Parse_SuspendedStock_MarkedSuspendedAndInvalidOhlc()
    {
        // Column 15 (0-indexed) is SUSPENDED = 1
        var csv =
            "2026-03-18;SUSPD.E;SUSPENDED SIRKET;A;Z;MSPOT;EQT;MSPOTEQT;MSPOTEQTSUSPD;SI;0;0;0;0;;1;50.0;0;0;0;0;0;0;0;0;0;0;0;0;0;0\n";

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));
        var result = _parser.Parse(stream);

        Assert.Single(result.Records);
        var record = result.Records[0];
        Assert.Equal("SUSPD", record.Ticker);
        Assert.True(record.Suspended);
        Assert.False(record.HasValidOhlc, "Suspended stocks must not emit valid OHLC bars.");
    }

    [Fact]
    public void Parse_ZeroTradeOrAbnormalOhlc_DoesNotEmitValidOhlc()
    {
        // 1: Zero volume / zero price
        // 2: High < Low invalid bar
        var csv =
            "2026-03-18;NOTRD.E;NO TRADE STOCK;A;Z;MSPOT;EQT;MSPOTEQT;MSPOTEQTNOTRD;SI;0;0;0;0;;0;20.0;0;0;0;0;0;0;0;0;0;0;0;0;0;0\n" +
            "2026-03-18;BADBR.E;BAD BAR STOCK;A;Z;MSPOT;EQT;MSPOTEQT;MSPOTEQTBADBR;SI;0;0;0;0;;0;20.0;25.0;0;30.0;20.0;22.0;0;0;0;0;0;22.0;1000;100;1\n";

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));
        var result = _parser.Parse(stream);

        Assert.Equal(2, result.Records.Count);
        Assert.False(result.Records[0].HasValidOhlc, "Zero price bar must be marked invalid.");
        Assert.False(result.Records[1].HasValidOhlc, "High < Low bar must be marked invalid.");
    }

    [Fact]
    public void Parse_CorporateActionWithCommaDecimal_CorrectlyParsed()
    {
        // Corporate action BDL (Bedelli) and comma formatted prices
        var csv =
            "2026-03-18;SPLIT.E;SPLIT CORP;A;Z;MSPOT;EQT;MSPOTEQT;MSPOTEQTSPLIT;SI;0;0;0;0;BDL;0;100,0;50,0;0;48,0;52,0;51,0;51,0;2,0;0;0;50,5;505000;10000;20;0\n";

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
}
