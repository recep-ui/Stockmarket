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
        ILogger<TelegramNotificationProvider> logger)
    {
        _configuration = configuration;
        _logger = logger;
        _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
    }

    public async Task SendAsync(NotificationMessage message, CancellationToken cancellationToken = default)
    {
        var botToken = _configuration["Telegram:BotToken"];
        var chatId = message.Recipient ?? _configuration["Telegram:DefaultChatId"];

        if (string.IsNullOrWhiteSpace(botToken) || string.IsNullOrWhiteSpace(chatId))
        {
            _logger.LogInformation("[TELEGRAM DISPATCH (SIMULATED)] To: {ChatId}\n{Content}", chatId, message.FormattedMarkdown ?? message.Body);
            return;
        }

        try
        {
            var text = message.FormattedMarkdown ?? $"{message.Title}\n\n{message.Body}";
            var url = $"https://api.telegram.org/bot{botToken}/sendMessage";
            var payload = new { chat_id = chatId, text, parse_mode = "Markdown" };
            
            var response = await _httpClient.PostAsJsonAsync(url, payload, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Telegram notification successfully sent to {ChatId}.", chatId);
            }
            else
            {
                _logger.LogWarning("Telegram API returned non-success code: {Code}", response.StatusCode);
                throw new HttpRequestException($"Telegram API dispatch failed with status code {response.StatusCode}.");
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to dispatch Telegram notification to {ChatId}", chatId);
            throw;
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

    public async Task SendAsync(NotificationMessage message, CancellationToken cancellationToken = default)
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
    }
}
