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
    Processing = 12
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
