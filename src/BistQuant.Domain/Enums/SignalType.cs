using System.Text.Json.Serialization;

namespace BistQuant.Domain.Enums;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum SignalType : byte
{
    StrongSell = 1,
    Sell = 2,
    Weak = 3,
    Watch = 4,
    BuyCandidate = 5,
    Buy = 6,
    StrongBuy = 7
}
