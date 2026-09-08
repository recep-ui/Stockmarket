namespace BistQuant.Domain.Models;

public record TechnicalScores(
    int TotalScore,
    int TrendScore,
    int MomentumScore,
    int VolumeScore,
    int StructureScore
);

public record RiskParameters(
    decimal EntryPrice,
    decimal StopLoss,
    decimal TakeProfit1,
    decimal TakeProfit2,
    decimal RiskRewardRatio
);
