using BistQuant.Domain.Enums;

namespace BistQuant.Application.DTOs.Signals;

public record SignalReasonDto(
    string Code,
    string Title,
    string Description,
    int Score,
    string Indicator,
    decimal? IndicatorValue
);

public record ScoresSummaryDto(
    int Trend,
    int Momentum,
    int Volume,
    int Structure
);

public record RiskSummaryDto(
    decimal StopLoss,
    decimal TakeProfit1,
    decimal TakeProfit2,
    decimal RiskReward
);

public record IndicatorsSummaryDto(
    decimal? Rsi,
    decimal? Adx,
    decimal? VolumeRatio
);

public record SignalDto(
    string Symbol,
    decimal Price,
    int Score,
    string Signal,
    ScoresSummaryDto Scores,
    IndicatorsSummaryDto Indicators,
    RiskSummaryDto Risk,
    List<SignalReasonDto> Reasons,
    DateTime CreatedAt
);
