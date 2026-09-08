using System.Globalization;
using System.Text;

namespace BistQuant.Application.Services.MarketData;

public record BistBulletinEquityRecord(
    DateOnly Date,
    string SeriesCode,
    string Ticker,
    string InstrumentName,
    string? MarketSegment,
    string InstrumentGroup,
    string? InstrumentType,
    string? TradingMethod,
    bool IsBist100,
    bool IsBist30,
    string? CorporateAction,
    bool Suspended,
    decimal? PreviousLastPrice,
    decimal? Open,
    decimal? High,
    decimal? Low,
    decimal? Close,
    decimal? ClosingSessionPrice,
    decimal? ChangePercent,
    decimal? Vwap,
    decimal? TotalTradedValue,
    decimal? TotalTradedVolume,
    long? TotalNumberOfContracts,
    bool HasValidOhlc
);

public record BistBulletinParseResult(
    DateOnly? SessionDate,
    IReadOnlyList<BistBulletinEquityRecord> Records,
    int TotalRowsRead,
    int AcceptedRows,
    int RejectedRows,
    IReadOnlyList<string> ParseErrors
);

public class BistDailyBulletinParser
{
    private static readonly string[] DateFormats = { "yyyy-MM-dd", "yyyyMMdd", "dd/MM/yyyy", "dd.MM.yyyy", "yyyy/MM/dd" };

    public BistBulletinParseResult Parse(Stream stream)
    {
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
        return Parse(reader);
    }

    public BistBulletinParseResult Parse(TextReader reader)
    {
        var records = new List<BistBulletinEquityRecord>();
        var errors = new List<string>();
        int totalRows = 0;
        int acceptedRows = 0;
        int rejectedRows = 0;
        DateOnly? detectedSessionDate = null;

        string? line;
        while ((line = reader.ReadLine()) != null)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            totalRows++;

            // Format is semicolon-separated
            var parts = line.Split(';');
            if (parts.Length < 24)
            {
                // Likely a header line or malformed footer
                if (totalRows <= 5)
                {
                    // Normal header line
                    continue;
                }

                rejectedRows++;
                if (errors.Count < 50)
                {
                    errors.Add($"Line {totalRows}: Insufficient columns ({parts.Length} < 24)");
                }
                continue;
            }

            // Attempt to parse Date in column 0
            var dateStr = parts[0].Trim();
            if (!TryParseDate(dateStr, out var date))
            {
                // If it's the first few lines, it's the header row (Turkish or English)
                if (totalRows <= 3)
                {
                    continue;
                }

                rejectedRows++;
                if (errors.Count < 50)
                {
                    errors.Add($"Line {totalRows}: Unable to parse date '{dateStr}'");
                }
                continue;
            }

            detectedSessionDate ??= date;

            // Column 6: INSTRUMENT GROUP (must be "EQT" for Equities)
            // Note: Section 2.1.4:
            // 0: DATE, 1: INSTRUMENT SERIES CODE, 2: INSTRUMENT NAME, 3: A,B,C,D GROUP,
            // 4: MARKET SEGMENT, 5: MARKET, 6: INSTRUMENT GROUP, 7: INSTRUMENT TYPE
            var instrumentGroup = parts.Length > 6 ? parts[6].Trim().ToUpperInvariant() : string.Empty;

            // Ignore non-equity instruments (e.g. warrants ECW, indices SP, funds, certificates, debt)
            if (!string.Equals(instrumentGroup, "EQT", StringComparison.OrdinalIgnoreCase))
            {
                rejectedRows++;
                continue;
            }

            // Column 1: INSTRUMENT SERIES CODE (e.g. "THYAO.E" or "GARAN.E")
            var seriesCode = parts[1].Trim().ToUpperInvariant();
            if (string.IsNullOrEmpty(seriesCode))
            {
                rejectedRows++;
                continue;
            }

            // Normalize ticker: only after confirming EQT, strip trailing .E
            var ticker = seriesCode;
            if (ticker.EndsWith(".E", StringComparison.OrdinalIgnoreCase))
            {
                ticker = ticker[..^2];
            }

            // Reject invalid or synthetic test tickers
            if (string.IsNullOrWhiteSpace(ticker))
            {
                rejectedRows++;
                continue;
            }

            var instrumentName = parts.Length > 2 ? parts[2].Trim() : string.Empty;
            var marketSegment = parts.Length > 4 ? parts[4].Trim() : null;
            var instrumentType = parts.Length > 7 ? parts[7].Trim() : null;
            var tradingMethod = parts.Length > 9 ? parts[9].Trim() : null;

            var isBist100 = parts.Length > 11 && parts[11].Trim() == "1";
            var isBist30 = parts.Length > 12 && parts[12].Trim() == "1";

            var corporateAction = parts.Length > 14 && !string.IsNullOrWhiteSpace(parts[14]) ? parts[14].Trim() : null;
            var suspended = parts.Length > 15 && parts[15].Trim() == "1";

            var prevLastPrice = parts.Length > 16 ? ParseDecimal(parts[16]) : null;
            var open = parts.Length > 17 ? ParseDecimal(parts[17]) : null;
            var low = parts.Length > 20 ? ParseDecimal(parts[20]) : null;
            var high = parts.Length > 21 ? ParseDecimal(parts[21]) : null;
            var close = parts.Length > 22 ? ParseDecimal(parts[22]) : null;
            var closingSessionPrice = parts.Length > 23 ? ParseDecimal(parts[23]) : null;

            var changePercent = parts.Length > 24 ? ParseDecimal(parts[24]) : null;
            var vwap = parts.Length > 27 ? ParseDecimal(parts[27]) : null;
            var totalTradedValue = parts.Length > 28 ? ParseDecimal(parts[28]) : null;
            var totalTradedVolume = parts.Length > 29 ? ParseDecimal(parts[29]) : null;
            var totalContracts = parts.Length > 30 ? ParseLong(parts[30]) : null;

            // Strict OHLC validation:
            // Valid bar must have Open > 0, High > 0, Low > 0, Close > 0, High >= Low, High >= Open, High >= Close, Low <= Open, Low <= Close
            bool hasValidOhlc = !suspended
                && open.HasValue && open.Value > 0
                && high.HasValue && high.Value > 0
                && low.HasValue && low.Value > 0
                && close.HasValue && close.Value > 0
                && high.Value >= low.Value
                && high.Value >= open.Value
                && high.Value >= close.Value
                && low.Value <= open.Value
                && low.Value <= close.Value
                && (totalTradedVolume ?? 0) >= 0;

            records.Add(new BistBulletinEquityRecord(
                Date: date,
                SeriesCode: seriesCode,
                Ticker: ticker,
                InstrumentName: instrumentName,
                MarketSegment: marketSegment,
                InstrumentGroup: instrumentGroup,
                InstrumentType: instrumentType,
                TradingMethod: tradingMethod,
                IsBist100: isBist100,
                IsBist30: isBist30,
                CorporateAction: corporateAction,
                Suspended: suspended,
                PreviousLastPrice: prevLastPrice,
                Open: open,
                High: high,
                Low: low,
                Close: close,
                ClosingSessionPrice: closingSessionPrice,
                ChangePercent: changePercent,
                Vwap: vwap,
                TotalTradedValue: totalTradedValue,
                TotalTradedVolume: totalTradedVolume,
                TotalNumberOfContracts: totalContracts,
                HasValidOhlc: hasValidOhlc
            ));

            acceptedRows++;
        }

        return new BistBulletinParseResult(
            SessionDate: detectedSessionDate,
            Records: records,
            TotalRowsRead: totalRows,
            AcceptedRows: acceptedRows,
            RejectedRows: rejectedRows,
            ParseErrors: errors
        );
    }

    private static bool TryParseDate(string input, out DateOnly date)
    {
        if (DateOnly.TryParseExact(input, DateFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out date))
        {
            return true;
        }

        if (DateTime.TryParse(input, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
        {
            date = DateOnly.FromDateTime(dt);
            return true;
        }

        date = default;
        return false;
    }

    private static decimal? ParseDecimal(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return null;

        var clean = input.Trim().Replace(',', '.');
        if (decimal.TryParse(clean, NumberStyles.Any, CultureInfo.InvariantCulture, out var val))
        {
            return val;
        }

        return null;
    }

    private static long? ParseLong(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return null;

        var clean = input.Trim();
        if (long.TryParse(clean, NumberStyles.Any, CultureInfo.InvariantCulture, out var val))
        {
            return val;
        }

        return null;
    }
}
