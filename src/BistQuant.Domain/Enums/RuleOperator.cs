using System.Text.Json.Serialization;

namespace BistQuant.Domain.Enums;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum RuleOperator : byte
{
    GreaterThan = 1,
    LessThan = 2,
    GreaterThanOrEqual = 3,
    LessThanOrEqual = 4,
    Equal = 5,
    CrossAbove = 6,
    CrossBelow = 7,
    Between = 8
}
