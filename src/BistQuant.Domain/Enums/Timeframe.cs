using System.Text.Json.Serialization;

namespace BistQuant.Domain.Enums;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum Timeframe : byte
{
    M1 = 1,
    M5 = 5,
    M15 = 15,
    M30 = 30,
    H1 = 60,
    H4 = 240,
    Daily = 250,
    Weekly = 251
}
