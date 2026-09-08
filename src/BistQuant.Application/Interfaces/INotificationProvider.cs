namespace BistQuant.Application.Interfaces;

public record NotificationMessage(
    string Title,
    string Body,
    string Recipient,
    string? FormattedMarkdown = null
);

public interface INotificationProvider
{
    Task SendAsync(NotificationMessage message, CancellationToken cancellationToken = default);
}
