using BistQuant.Domain.Enums;

namespace BistQuant.Application.DTOs.Alerts;

public record AlertSubscriptionDto(
    long Id,
    long UserId,
    int? SymbolId,
    string? Symbol,
    string AlertType,
    string Condition,
    decimal Value,
    NotificationChannel Channel,
    bool IsActive,
    DateTime? LastTriggeredAt
);

public record CreateAlertRequest(
    string? Symbol,
    string AlertType = "ScoreThreshold",
    string Condition = ">=",
    decimal Value = 80m,
    NotificationChannel Channel = NotificationChannel.InApp
);

public record NotificationDto(
    long Id,
    string Title,
    string Message,
    bool IsDelivered,
    bool IsRead,
    DateTime CreatedAt
);
