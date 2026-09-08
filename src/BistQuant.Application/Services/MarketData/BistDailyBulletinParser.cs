using System.Globalization;
using System.Text;
using BistQuant.Domain.Enums;

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
    IReadOnlyList<string> ParseErrors,
    bool IsSchemaMismatch = false,
    bool IsDateMismatch = false
)
{
    public bool IsSuccess => !IsSchemaMismatch && !IsDateMismatch && (Records.Count > 0 || TotalRowsRead > 0);
    public IReadOnlyList<string> Errors => ParseErrors;
}

public class BistDailyBulletinParser
{
    private static readonly string[] DateFormats = { "yyyy-MM-dd", "yyyyMMdd", "dd/MM/yyyy", "dd.MM.yyyy", "yyyy/MM/dd" };

    /// <summary>
    /// When true, requires an official recognized header row before processing any data rows.
    /// Automatic official download mode requires this to fail closed rather than guess positional layouts.
    /// </summary>
    public bool RequireRecognizedHeader { get; set; } = true;

    public BistBulletinParseResult Parse(Stream stream, DateOnly? expectedDate = null, bool? requireRecognizedHeader = null)
    {
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
        return Parse(reader, expectedDate, requireRecognizedHeader);
    }

    public BistBulletinParseResult Parse(TextReader reader, DateOnly? expectedDate = null, bool? requireRecognizedHeader = null)
    {
        bool mustHaveHeader = requireRecognizedHeader ?? RequireRecognizedHeader;
        var records = new List<BistBulletinEquityRecord>();
        var errors = new List<string>();
        int totalRows = 0;
        int acceptedRows = 0;
        int rejectedRows = 0;
        DateOnly? detectedSessionDate = null;

        // Dynamic column index map initialized with standard v1.14 defaults
        var colMap = new ColumnIndexMap();
        bool headerProcessed = false;

        string? line;
        while ((line = reader.ReadLine()) != null)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            totalRows++;
            var parts = line.Split(';');

            // 1. Check minimum column count
            if (parts.Length < BistBulletinSchemaV114.MinimumColumnCount)
            {
                // If it's within the first 3 lines, it could be an invalid header or truncated header
                if (totalRows <= 3)
                {
                    // Check if it's an HTML tag or malformed document
                    var trimmed = line.TrimStart();
                    if (trimmed.StartsWith("<html", StringComparison.OrdinalIgnoreCase) ||
                        trimmed.StartsWith("<!doctype", StringComparison.OrdinalIgnoreCase))
                    {
                        return new BistBulletinParseResult(
                            SessionDate: null,
                            Records: Array.Empty<BistBulletinEquityRecord>(),
                            TotalRowsRead: totalRows,
                            AcceptedRows: 0,
                            RejectedRows: totalRows,
                            ParseErrors: new[] { "Input content appears to be an HTML page, not a valid BIST CSV bulletin." },
                            IsSchemaMismatch: true
                        );
                    }
                }

                errors.Add($"Line {totalRows}: Column count ({parts.Length}) is less than minimum required {BistBulletinSchemaV114.MinimumColumnCount}.");
                return new BistBulletinParseResult(
                    SessionDate: detectedSessionDate,
                    Records: Array.Empty<BistBulletinEquityRecord>(),
                    TotalRowsRead: totalRows,
                    AcceptedRows: 0,
                    RejectedRows: totalRows,
                    ParseErrors: errors,
                    IsSchemaMismatch: true
                );
            }

            // 2. Check if this line is a header row
            var firstCell = parts[0].Trim();
            if (!TryParseDate(firstCell, out var rowDate))
            {
                // It is a header line (e.g., Turkish or English)
                if (totalRows <= 3)
                {
                    var headerError = TryProcessHeader(parts, colMap);
                    if (headerError != null && !headerProcessed)
                    {
                        errors.Add($"Header validation failure on line {totalRows}: {headerError}");
                        if (LooksLikeHeader(parts))
                        {
                            return new BistBulletinParseResult(
                                SessionDate: null,
                                Records: Array.Empty<BistBulletinEquityRecord>(),
                                TotalRowsRead: totalRows,
                                AcceptedRows: 0,
                                RejectedRows: totalRows,
                                ParseErrors: errors,
                                IsSchemaMismatch: true
                            );
                        }
                    }
                    else if (headerError == null)
                    {
                        headerProcessed = true;
                    }
                    continue;
                }

                // Non-header row with unparseable date
                rejectedRows++;
                if (errors.Count < 50)
                {
                    errors.Add($"Line {totalRows}: Unable to parse session date '{firstCell}'.");
                }
                continue;
            }

            // If recognized header is mandatory for automatic ingestion, fail closed if no header was encountered
            if (mustHaveHeader && !headerProcessed)
            {
                errors.Add("Official recognized BIST bulletin header row is required in automatic download mode, but was not found. Headerless parsing is rejected to prevent schema guessing.");
                return new BistBulletinParseResult(
                    SessionDate: rowDate,
                    Records: Array.Empty<BistBulletinEquityRecord>(),
                    TotalRowsRead: totalRows,
                    AcceptedRows: 0,
                    RejectedRows: totalRows,
                    ParseErrors: errors,
                    IsSchemaMismatch: true
                );
            }

            // 3. Date Validation & Layout Detection
            if (detectedSessionDate == null)
            {
                detectedSessionDate = rowDate;

                // Validate target date matches bulletin date
                if (expectedDate.HasValue && rowDate != expectedDate.Value)
                {
                    errors.Add($"Date mismatch: Requested session date was {expectedDate.Value:yyyy-MM-dd}, but bulletin contains date {rowDate:yyyy-MM-dd}.");
                    return new BistBulletinParseResult(
                        SessionDate: rowDate,
                        Records: Array.Empty<BistBulletinEquityRecord>(),
                        TotalRowsRead: totalRows,
                        AcceptedRows: 0,
                        RejectedRows: totalRows,
                        ParseErrors: errors,
                        IsDateMismatch: true
                    );
                }
            }
            else if (rowDate != detectedSessionDate.Value)
            {
                // Multi-date contamination within a single daily bulletin
                errors.Add($"Line {totalRows}: Mixed session dates detected ({rowDate:yyyy-MM-dd} vs {detectedSessionDate.Value:yyyy-MM-dd}).");
                return new BistBulletinParseResult(
                    SessionDate: detectedSessionDate,
                    Records: Array.Empty<BistBulletinEquityRecord>(),
                    TotalRowsRead: totalRows,
                    AcceptedRows: 0,
                    RejectedRows: totalRows,
                    ParseErrors: errors,
                    IsDateMismatch: true
                );
            }

            // 4. Instrument Group Validation (must be EQT)
            var instrumentGroup = GetValue(parts, colMap.InstrumentGroup).ToUpperInvariant();
            if (!string.Equals(instrumentGroup, "EQT", StringComparison.OrdinalIgnoreCase))
            {
                // Warrants (WNT/ECW), indices, funds, certificates, debt are skipped
                rejectedRows++;
                continue;
            }

            // 5. Instrument Series Code & Ticker Normalization
            var seriesCode = GetValue(parts, colMap.SeriesCode).ToUpperInvariant();
            if (string.IsNullOrWhiteSpace(seriesCode))
            {
                rejectedRows++;
                continue;
            }

            // Strip trailing .E only after confirming EQT
            var ticker = seriesCode;
            if (ticker.EndsWith(".E", StringComparison.OrdinalIgnoreCase))
            {
                ticker = ticker[..^2];
            }

            if (string.IsNullOrWhiteSpace(ticker))
            {
                rejectedRows++;
                continue;
            }

            var instrumentName = GetValue(parts, colMap.InstrumentName);
            var marketSegment = GetOptionalValue(parts, colMap.MarketSegment);
            var instrumentType = GetOptionalValue(parts, colMap.InstrumentType);
            var tradingMethod = GetOptionalValue(parts, colMap.TradingMethod);

            var isBist100 = GetValue(parts, colMap.Bist100Index) == "1";
            var isBist30 = GetValue(parts, colMap.Bist30Index) == "1";

            var corporateAction = GetOptionalValue(parts, colMap.CorporateAction);
            var suspended = GetValue(parts, colMap.Suspended) == "1";

            var prevLastPrice = ParseDecimal(GetValue(parts, colMap.PreviousLastPrice));
            var open = ParseDecimal(GetValue(parts, colMap.Open));
            var low = ParseDecimal(GetValue(parts, colMap.Low));
            var high = ParseDecimal(GetValue(parts, colMap.High));
            var close = ParseDecimal(GetValue(parts, colMap.Close));
            var closingSessionPrice = ParseDecimal(GetValue(parts, colMap.ClosingSessionPrice));
            var changePercent = ParseDecimal(GetValue(parts, colMap.ChangePercent));
            var vwap = ParseDecimal(GetValue(parts, colMap.Vwap));
            var totalTradedValue = ParseDecimal(GetValue(parts, colMap.TotalTradedValue));
            var totalTradedVolume = ParseDecimal(GetValue(parts, colMap.TotalTradedVolume));
            var totalContracts = ParseLong(GetValue(parts, colMap.TotalNumberOfContracts));

            // Strict OHLC Integrity Check
            // A valid daily price bar requires: not suspended, positive prices, High >= Low, High >= Open, High >= Close, Low <= Open, Low <= Close, Volume >= 0
            // Volume is strictly mandatory and cannot be fabricated from null
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
                && totalTradedVolume.HasValue && totalTradedVolume.Value >= 0;

            records.Add(new BistBulletinEquityRecord(
                Date: rowDate,
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

    private static bool LooksLikeHeader(string[] parts)
    {
        var lineText = string.Join(" ", parts).ToUpperInvariant();
        return lineText.Contains("TARIH") || lineText.Contains("TRADE DATE") ||
               lineText.Contains("INSTRUMENT") || lineText.Contains("FIYAT") ||
               lineText.Contains("ISLEM");
    }

    private static string? TryProcessHeader(string[] headers, ColumnIndexMap map)
    {
        var upperHeaders = headers.Select(h => h.Trim().ToUpperInvariant()).ToArray();

        // Check required concepts
        bool hasDate = upperHeaders.Any(h => h.Contains("TARIH") || h.Contains("TRADE DATE") || h == "DATE");
        bool hasSeries = upperHeaders.Any(h => h.Contains("ISLEM  KODU") || h.Contains("ISLEM KODU") || h.Contains("SERIES CODE") || h.Contains("INSTRUMENT CODE") || h.Contains("KIYMET KODU") || h.Contains("MENKUL KODU"));
        bool hasGroup = upperHeaders.Any(h => h.Contains("ENSTRUMAN GRUBU") || h.Contains("MENKUL GRUBU") || h.Contains("INSTRUMENT GROUP") || h == "GRUP");
        bool hasOpen = upperHeaders.Any(h => h == "ACILIS FIYATI" || h == "OPENING PRICE" || h == "ACILIS");
        bool hasLow = upperHeaders.Any(h => h == "EN DUSUK FIYAT" || h == "LOWEST PRICE" || h == "EN DUSUK");
        bool hasHigh = upperHeaders.Any(h => h == "EN YUKSEK FIYAT" || h == "HIGHEST PRICE" || h == "EN YUKSEK");
        bool hasClose = upperHeaders.Any(h => h == "KAPANIS FIYATI" || h == "CLOSING PRICE" || h == "KAPANIS");
        // Strict Volume concept: Total traded volume (shares/lots) must NOT be satisfied by value (TL)
        bool hasVolume = upperHeaders.Any(h => h.Contains("TOPLAM ISLEM ADEDI") || h.Contains("TOTAL TRADED VOLUME") || h == "ISLEM ADEDI" || h == "VOLUME");

        if (!hasDate || !hasSeries || !hasGroup || !hasOpen || !hasLow || !hasHigh || !hasClose || !hasVolume)
        {
            return "Missing one or more required header concepts: DATE, SERIES CODE, INSTRUMENT GROUP, OPENING PRICE, LOWEST PRICE, HIGHEST PRICE, CLOSING PRICE, TOTAL TRADED VOLUME.";
        }

        // Dynamically resolve column positions from header
        for (int i = 0; i < upperHeaders.Length; i++)
        {
            var h = upperHeaders[i];
            if (h.Contains("TARIH") || h.Contains("TRADE DATE") || h == "DATE") map.Date = i;
            else if (h.Contains("ISLEM  KODU") || h.Contains("ISLEM KODU") || h.Contains("SERIES CODE") || h.Contains("KIYMET KODU") || h.Contains("MENKUL KODU")) map.SeriesCode = i;
            else if (h.Contains("BULTEN ADI") || h.Contains("INSTRUMENT NAME") || h.Contains("KIYMET ADI") || h.Contains("MENKUL ADI")) map.InstrumentName = i;
            else if (h == "PAZAR" || h == "MARKET SEGMENT") map.MarketSegment = i;
            else if (h == "ENSTRUMAN GRUBU" || h == "MENKUL GRUBU" || h == "INSTRUMENT GROUP") map.InstrumentGroup = i;
            else if (h == "ENSTRUMAN TIPI" || h == "MENKUL TURU" || h == "INSTRUMENT TYPE") map.InstrumentType = i;
            else if (h == "ISLEM YONTEMI" || h == "TRADING METHOD") map.TradingMethod = i;
            else if (h.Contains("BIST 100")) map.Bist100Index = i;
            else if (h.Contains("BIST 30")) map.Bist30Index = i;
            else if (h.Contains("OZSERMAYE") || h.Contains("CORPORATE ACTION") || h.Contains("SIRKET ISLEMI")) map.CorporateAction = i;
            else if (h.Contains("DURDURMA") || h == "SUSPENDED" || h.Contains("ISLEM GORMEYEN")) map.Suspended = i;
            else if (h.Contains("ONCEKI KAPANIS") || h.Contains("PREVIOUS LAST PRICE")) map.PreviousLastPrice = i;
            else if (h == "ACILIS FIYATI" || h == "OPENING PRICE" || h == "ACILIS") map.Open = i;
            else if (h.Contains("ACILIS SEANSI") || h.Contains("OPENING SESSION PRICE")) map.OpeningSessionPrice = i;
            else if (h.Contains("GUNORTASI") || h.Contains("MIDDAY PRICE")) map.MiddayPrice = i;
            else if (h == "EN DUSUK FIYAT" || h == "LOWEST PRICE" || h == "EN DUSUK") map.Low = i;
            else if (h == "EN YUKSEK FIYAT" || h == "HIGHEST PRICE" || h == "EN YUKSEK") map.High = i;
            else if (h == "KAPANIS FIYATI" || h == "CLOSING PRICE" || h == "KAPANIS") map.Close = i;
            else if (h.Contains("KAPANIS SEANSI") || h.Contains("CLOSING SESSION PRICE")) map.ClosingSessionPrice = i;
            else if (h.Contains("DEGISIM") || h.Contains("CHANGE") || h.Contains("FIYAT DEG")) map.ChangePercent = i;
            else if (h == "A.O.F" || h == "AOF" || h == "VWAP") map.Vwap = i;
            else if (h.Contains("TOPLAM ISLEM HACMI") || h.Contains("TOTAL TRADED VALUE")) map.TotalTradedValue = i;
            else if (h.Contains("TOPLAM ISLEM ADEDI") || h.Contains("TOTAL TRADED VOLUME") || h == "ISLEM ADEDI" || h == "VOLUME") map.TotalTradedVolume = i;
            else if (h.Contains("SOZLESME SAYISI") || h.Contains("NUMBER OF CONTRACTS") || h.Contains("TOPLAM SOZLESME")) map.TotalNumberOfContracts = i;
            else if (h.Contains("REFERANS") || h.Contains("REFERENCE PRICE")) map.ReferencePrice = i;
        }

        return null;
    }

    private static string GetValue(string[] parts, int index)
    {
        return index >= 0 && index < parts.Length ? parts[index].Trim() : string.Empty;
    }

    private static string? GetOptionalValue(string[] parts, int index)
    {
        if (index < 0 || index >= parts.Length) return null;
        var val = parts[index].Trim();
        return string.IsNullOrWhiteSpace(val) ? null : val;
    }

    public static bool TryParseDate(string input, out DateOnly date)
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

    public static decimal? ParseDecimal(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return null;

        var text = input.Trim();

        // 1. Isolated Turkish comma-decimal fallback:
        // When text contains a single comma, NO period, and digits after comma != 3 (e.g. "100,0", "100,50", "315,25", "2,5")
        // It is unambiguously a decimal separator (NOT thousands separator).
        // Crucial: "5,000" has exactly 3 digits after comma and is NOT treated as decimal.
        if (text.Contains(',') && !text.Contains('.'))
        {
            int firstComma = text.IndexOf(',');
            int lastComma = text.LastIndexOf(',');
            if (firstComma == lastComma)
            {
                int digitsAfterComma = text.Length - 1 - firstComma;
                if (digitsAfterComma > 0 && digitsAfterComma != 3)
                {
                    var normalized = text.Replace(',', '.');
                    if (decimal.TryParse(normalized, NumberStyles.Number | NumberStyles.AllowExponent, CultureInfo.InvariantCulture, out var decVal))
                    {
                        return decVal;
                    }
                }
            }
        }

        // 2. Primary BIST standard: Invariant culture (decimal separator = '.', thousands separator = ',')
        // Examples: "315.25", "1,234.56", "109,723.61", "5,000,000", "5,000", "1500000000"
        if (decimal.TryParse(text, NumberStyles.Number | NumberStyles.AllowExponent, CultureInfo.InvariantCulture, out var invVal))
        {
            return invVal;
        }

        return null;
    }

    public static long? ParseLong(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return null;

        var text = input.Trim();
        if (long.TryParse(text, NumberStyles.Integer | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var val))
        {
            return val;
        }

        return null;
    }

    private class ColumnIndexMap
    {
        public int Date { get; set; } = BistBulletinSchemaV114.Date;
        public int SeriesCode { get; set; } = BistBulletinSchemaV114.SeriesCode;
        public int InstrumentName { get; set; } = BistBulletinSchemaV114.InstrumentName;
        public int MarketSegment { get; set; } = BistBulletinSchemaV114.MarketSegment;
        public int InstrumentGroup { get; set; } = BistBulletinSchemaV114.InstrumentGroup;
        public int InstrumentType { get; set; } = BistBulletinSchemaV114.InstrumentType;
        public int TradingMethod { get; set; } = BistBulletinSchemaV114.TradingMethod;
        public int Bist100Index { get; set; } = BistBulletinSchemaV114.Bist100Index;
        public int Bist30Index { get; set; } = BistBulletinSchemaV114.Bist30Index;
        public int CorporateAction { get; set; } = BistBulletinSchemaV114.CorporateAction;
        public int Suspended { get; set; } = BistBulletinSchemaV114.Suspended;
        public int PreviousLastPrice { get; set; } = BistBulletinSchemaV114.PreviousLastPrice;
        public int Open { get; set; } = BistBulletinSchemaV114.Open;
        public int OpeningSessionPrice { get; set; } = BistBulletinSchemaV114.OpeningSessionPrice;
        public int MiddayPrice { get; set; } = BistBulletinSchemaV114.MiddayPrice;
        public int Low { get; set; } = BistBulletinSchemaV114.Low;
        public int High { get; set; } = BistBulletinSchemaV114.High;
        public int Close { get; set; } = BistBulletinSchemaV114.Close;
        public int ClosingSessionPrice { get; set; } = BistBulletinSchemaV114.ClosingSessionPrice;
        public int ChangePercent { get; set; } = BistBulletinSchemaV114.ChangePercent;
        public int RemainingBid { get; set; } = BistBulletinSchemaV114.RemainingBid;
        public int RemainingAsk { get; set; } = BistBulletinSchemaV114.RemainingAsk;
        public int Vwap { get; set; } = BistBulletinSchemaV114.Vwap;
        public int TotalTradedValue { get; set; } = BistBulletinSchemaV114.TotalTradedValue;
        public int TotalTradedVolume { get; set; } = BistBulletinSchemaV114.TotalTradedVolume;
        public int TotalNumberOfContracts { get; set; } = BistBulletinSchemaV114.TotalNumberOfContracts;
        public int ReferencePrice { get; set; } = BistBulletinSchemaV114.ReferencePrice;
    }
}
