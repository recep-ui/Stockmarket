using BistQuant.Application.Common.Interfaces;
using BistQuant.Application.Interfaces;
using BistQuant.Domain.Entities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Net.Http.Json;

namespace BistQuant.Infrastructure.Providers.Notifications;

public class TelegramNotificationProvider : INotificationProvider
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<TelegramNotificationProvider> _logger;
    private readonly HttpClient _httpClient;

    public TelegramNotificationProvider(
        IConfiguration configuration,
        ILogger<TelegramNotificationProvider> logger,
        HttpClient? httpClient = null)
    {
        _configuration = configuration;
        _logger = logger;
        _httpClient = httpClient ?? new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
    }

    public async Task<NotificationDeliveryResult> SendAsync(NotificationMessage message, CancellationToken cancellationToken = default)
    {
        var botToken = _configuration["Telegram:BotToken"];
        var defaultChatId = _configuration["Telegram:DefaultChatId"];
        var chatId = !string.IsNullOrWhiteSpace(message.Recipient) ? message.Recipient : defaultChatId;
        var simulationMode = _configuration.GetValue<bool>("Telegram:SimulationMode");

        if (string.IsNullOrWhiteSpace(botToken))
        {
            if (simulationMode)
            {
                _logger.LogInformation("[TELEGRAM DISPATCH (SIMULATED - NO BOT TOKEN)] To: {ChatId}\n{Content}", chatId, message.FormattedMarkdown ?? message.Body);
                return new NotificationDeliveryResult(true, "Telegram", "SIM_NO_TOKEN", null, true);
            }
            _logger.LogWarning("Telegram BotToken is missing and SimulationMode is false. Notification dispatch failed.");
            return new NotificationDeliveryResult(false, "Telegram", null, "Telegram BotToken is missing in configuration.");
        }

        if (string.IsNullOrWhiteSpace(chatId))
        {
            if (simulationMode)
            {
                _logger.LogInformation("[TELEGRAM DISPATCH (SIMULATED - NO CHAT ID)]\n{Content}", message.FormattedMarkdown ?? message.Body);
                return new NotificationDeliveryResult(true, "Telegram", "SIM_NO_CHATID", null, true);
            }
            _logger.LogWarning("Telegram ChatId is missing for recipient '{Recipient}' and SimulationMode is false.", message.Recipient);
            return new NotificationDeliveryResult(false, "Telegram", null, "Telegram ChatId is missing.");
        }

        try
        {
            var text = message.FormattedMarkdown ?? $"{message.Title}\n\n{message.Body}";
            var url = $"https://api.telegram.org/bot{botToken}/sendMessage";
            var payload = new { chat_id = chatId, text, parse_mode = "Markdown" };

            var response = await _httpClient.PostAsJsonAsync(url, payload, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Telegram notification successfully delivered to {ChatId}.", chatId);
                return new NotificationDeliveryResult(true, "Telegram", Guid.NewGuid().ToString());
            }

            _logger.LogWarning("Telegram API returned non-success code: {Code}", response.StatusCode);
            return new NotificationDeliveryResult(false, "Telegram", null, $"Telegram API returned HTTP {(int)response.StatusCode} ({response.StatusCode}).");
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "Telegram API dispatch timed out for {ChatId}", chatId);
            return new NotificationDeliveryResult(false, "Telegram", null, "Telegram API dispatch timed out.");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to dispatch Telegram notification to {ChatId}", chatId);
            return new NotificationDeliveryResult(false, "Telegram", null, ex.Message);
        }
    }
}

public class InAppNotificationProvider : INotificationProvider
{
    private readonly IApplicationDbContext _context;
    private readonly ILogger<InAppNotificationProvider> _logger;

    public InAppNotificationProvider(IApplicationDbContext context, ILogger<InAppNotificationProvider> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<NotificationDeliveryResult> SendAsync(NotificationMessage message, CancellationToken cancellationToken = default)
    {
        long subId = 1;
        if (long.TryParse(message.Recipient, out var parsedId))
        {
            subId = parsedId;
        }

        var notification = new AlertNotification
        {
            AlertSubscriptionId = subId,
            Title = message.Title,
            Message = message.Body,
            IsDelivered = true,
            DeliveredAt = DateTime.UtcNow
        };

        _context.AlertNotifications.Add(notification);
        await _context.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("In-app notification saved: {Title}", message.Title);

        return new NotificationDeliveryResult(true, "InApp", notification.Id.ToString());
    }
}
