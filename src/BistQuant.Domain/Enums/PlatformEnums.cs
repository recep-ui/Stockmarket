using System.Text.Json.Serialization;

namespace BistQuant.Domain.Enums;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum OrderType : byte
{
    Market = 1,
    Limit = 2,
    StopLoss = 3
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum OrderSide : byte
{
    Buy = 1,
    Sell = 2
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum OrderStatus : byte
{
    Pending = 1,
    Filled = 2,
    Cancelled = 3,
    Rejected = 4,
    PendingNextSessionOpen = 5,
    Expired = 6
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum MarketDataImportStatus : byte
{
    Pending = 1,
    Downloaded = 2,
    Parsed = 3,
    Imported = 4,
    Success = 4,
    AlreadyImported = 5,
    NotPublishedYet = 6,
    InvalidSourceContent = 7,
    SchemaMismatch = 8,
    Failed = 9,
    BulletinRevisionDetected = 10,
    Skipped = 11,
    Processing = 12,
    DateMismatch = 13
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum BulletinDownloadStatus : byte
{
    Success = 1,
    NotPublishedYet = 2,
    RateLimited = 3,
    ProviderUnavailable = 4,
    InvalidSourceContent = 5,
    SchemaMismatch = 6,
    DateMismatch = 7,
    AutomaticDownloadUnavailable = 8,
    Cancelled = 9,
    Failed = 10
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum NotificationChannel : byte
{
    InApp = 1,
    Telegram = 2,
    WebPush = 3,
    Email = 4
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum BacktestStatus : byte
{
    Pending = 1,
    Running = 2,
    Completed = 3,
    Failed = 4
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum BackfillJobStatus : byte
{
    Pending = 1,
    Running = 2,
    Paused = 3,
    Completed = 4,
    CompletedWithErrors = 5,
    Failed = 6,
    Cancelled = 7
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum MarketDataOrigin : byte
{
    OfficialBistBulletin = 1,
    ManualOfficialBistBulletin = 2,
    Demo = 3
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum MarketDataGapReason : byte
{
    NoTrade = 1,
    Suspended = 2,
    BulletinMissing = 3,
    ImportFailed = 4,
    Unknown = 5
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum AnalysisStatus : byte
{
    Analyzed = 1,
    InsufficientHistory = 2,
    CorporateActionReview = 3,
    Suspended = 4,
    MissingData = 5
}
