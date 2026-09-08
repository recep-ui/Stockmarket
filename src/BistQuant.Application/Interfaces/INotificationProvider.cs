namespace BistQuant.Application.Interfaces;

public record NotificationMessage(
    string Title,
    string Body,
    string? Recipient,
    string? FormattedMarkdown = null
);

public record NotificationDeliveryResult(
    bool Success,
    string Provider,
    string? ExternalMessageId = null,
    string? Error = null,
    bool IsSimulated = false
);

public interface INotificationProvider
{
    Task<NotificationDeliveryResult> SendAsync(NotificationMessage message, CancellationToken cancellationToken = default);
}
