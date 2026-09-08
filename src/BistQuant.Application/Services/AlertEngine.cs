using BistQuant.Application.Common.Interfaces;
using BistQuant.Application.DTOs.Alerts;
using BistQuant.Application.DTOs.Signals;
using BistQuant.Application.Interfaces;
using BistQuant.Domain.Entities;
using BistQuant.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BistQuant.Application.Services;

public interface IAlertEngine
{
    Task ProcessAlertsForSignalAsync(Signal signal, CancellationToken cancellationToken = default);

    Task<List<AlertSubscriptionDto>> GetSubscriptionsAsync(long userId, CancellationToken cancellationToken = default);

    Task<AlertSubscriptionDto> CreateSubscriptionAsync(long userId, CreateAlertRequest request, CancellationToken cancellationToken = default);

    Task<bool> DeleteSubscriptionAsync(long id, long userId, CancellationToken cancellationToken = default);

    Task<List<NotificationDto>> GetRecentNotificationsAsync(long userId, CancellationToken cancellationToken = default);
}

public class AlertEngine : IAlertEngine
{
    private readonly IApplicationDbContext _context;
    private readonly IEnumerable<INotificationProvider> _providers;
    private readonly ILogger<AlertEngine> _logger;

    public AlertEngine(
        IApplicationDbContext context,
        IEnumerable<INotificationProvider> providers,
        ILogger<AlertEngine> logger)
    {
        _context = context;
        _providers = providers;
        _logger = logger;
    }

    public async Task ProcessAlertsForSignalAsync(Signal signal, CancellationToken cancellationToken = default)
    {
        var sym = await _context.Symbols.FindAsync(new object[] { signal.SymbolId }, cancellationToken);
        if (sym == null) return;

        var activeSubs = await _context.AlertSubscriptions
            .Include(a => a.User)
            .Where(a => a.IsActive && (a.SymbolId == null || a.SymbolId == signal.SymbolId))
            .ToListAsync(cancellationToken);

        var snapshot = await _context.IndicatorSnapshots
            .AsNoTracking()
            .Where(i => i.SymbolId == signal.SymbolId && i.Timeframe == signal.Timeframe)
            .OrderByDescending(i => i.Timestamp)
            .FirstOrDefaultAsync(cancellationToken);

        var now = DateTime.UtcNow;

        foreach (var sub in activeSubs)
        {
            // Cooldown check
            var cooldown = signal.Timeframe switch
            {
                Timeframe.M15 => TimeSpan.FromMinutes(15),
                Timeframe.H1 => TimeSpan.FromHours(1),
                _ => TimeSpan.FromHours(8) // Daily
            };

            if (sub.LastTriggeredAt.HasValue && (now - sub.LastTriggeredAt.Value) < cooldown)
            {
                continue; // Prevent spamming duplicate signals
            }

            // Direct IndicatorSnapshot / Price evaluation (no SignalReasons string parsing)
            bool isTriggered = sub.AlertType switch
            {
                "ScoreThreshold" => sub.Condition == "<=" ? signal.Score <= sub.Value : signal.Score >= sub.Value,
                "StrongBuy" => signal.SignalType == SignalType.StrongBuy,
                "Buy" => signal.SignalType == SignalType.Buy || signal.SignalType == SignalType.StrongBuy,
                "PriceBreakout" or "Breakout" => snapshot != null && (snapshot.IsBreakout == true || (snapshot.Resistance1.HasValue && signal.Price > snapshot.Resistance1.Value)),
                "SupportBreakout" or "SupportBreakdown" => snapshot != null && snapshot.Support1.HasValue && signal.Price < snapshot.Support1.Value,
                "VolumeSurge" => snapshot != null && snapshot.VolumeRatio.HasValue && (sub.Condition == "<=" ? snapshot.VolumeRatio.Value <= sub.Value : snapshot.VolumeRatio.Value >= (sub.Value > 0 ? sub.Value : 1.5m)),
                "RsiExtreme" => snapshot != null && snapshot.RSI14.HasValue && (sub.Condition switch
                {
                    "<=" or "<" => snapshot.RSI14.Value <= sub.Value,
                    ">=" or ">" => snapshot.RSI14.Value >= sub.Value,
                    _ => snapshot.RSI14.Value <= sub.Value
                }),
                _ => false // Fail-closed: unsupported conditions do not fire false positive alerts
            };

            if (isTriggered)
            {
                var title = $"🚨 {sym.Ticker} {signal.SignalType.ToString().ToUpperInvariant()} SİNYALİ";
                var body = $"Score: {signal.Score}/100 | Fiyat: {signal.Price:F2} TL | Stop: {signal.StopLoss:F2} | Hedef 1: {signal.TakeProfit1:F2} | R:R: {signal.RiskRewardRatio:F2}";
                
                var markdown = $@"🚨 *{sym.Ticker} {signal.SignalType.ToString().ToUpperInvariant()} SİNYALİ*

*Score:* `{signal.Score}/100`
*Fiyat:* `{signal.Price:F2} TL`
*Stop:* `{signal.StopLoss:F2} TL`
*Hedef 1:* `{signal.TakeProfit1:F2} TL`
*Hedef 2:* `{signal.TakeProfit2:F2} TL`
*Risk / Reward:* `{signal.RiskRewardRatio:F2}`
*Saat:* `{now:HH:mm:ss}`";

                var recipient = sub.Channel == NotificationChannel.Telegram ? (sub.User?.TelegramChatId ?? "") : sub.Id.ToString();
                var msg = new NotificationMessage(title, body, recipient, markdown);

                // Channel-specific routing: Telegram -> Telegram provider only, InApp -> InApp provider only
                var provider = _providers.FirstOrDefault(p => sub.Channel == NotificationChannel.Telegram
                    ? p.GetType().Name.Contains("Telegram")
                    : p.GetType().Name.Contains("InApp"));

                bool dispatched = false;
                if (provider != null)
                {
                    try
                    {
                        var result = await provider.SendAsync(msg, cancellationToken);
                        dispatched = result.Success;
                        if (!result.Success)
                        {
                            _logger.LogWarning("Notification dispatch failed for subscription {SubId} via {Channel}: {Error}", sub.Id, sub.Channel, result.Error);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Exception during alert dispatch via {Channel} for subscription {SubId}", sub.Channel, sub.Id);
                        dispatched = false;
                    }
                }

                // Invariant: LastTriggeredAt is updated strictly after verified successful dispatch
                if (dispatched)
                {
                    sub.LastTriggeredAt = now;
                }
            }
        }

        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<List<AlertSubscriptionDto>> GetSubscriptionsAsync(long userId, CancellationToken cancellationToken = default)
    {
        var subs = await _context.AlertSubscriptions
            .AsNoTracking()
            .Where(a => a.UserId == userId)
            .Include(a => a.Symbol)
            .OrderByDescending(a => a.Id)
            .ToListAsync(cancellationToken);

        return subs.Select(s => new AlertSubscriptionDto(
            s.Id,
            s.UserId,
            s.SymbolId,
            s.Symbol?.Ticker,
            s.AlertType,
            s.Condition,
            s.Value,
            s.Channel,
            s.IsActive,
            s.LastTriggeredAt
        )).ToList();
    }

    public async Task<AlertSubscriptionDto> CreateSubscriptionAsync(long userId, CreateAlertRequest request, CancellationToken cancellationToken = default)
    {
        Symbol? symbol = null;
        if (!string.IsNullOrWhiteSpace(request.Symbol))
        {
            symbol = await _context.Symbols.FirstOrDefaultAsync(s => s.Ticker == request.Symbol.ToUpper(), cancellationToken);
        }

        var sub = new AlertSubscription
        {
            UserId = userId,
            SymbolId = symbol?.Id,
            AlertType = request.AlertType,
            Condition = request.Condition,
            Value = request.Value,
            Channel = request.Channel,
            IsActive = true
        };

        _context.AlertSubscriptions.Add(sub);
        await _context.SaveChangesAsync(cancellationToken);

        return new AlertSubscriptionDto(
            sub.Id,
            sub.UserId,
            sub.SymbolId,
            symbol?.Ticker,
            sub.AlertType,
            sub.Condition,
            sub.Value,
            sub.Channel,
            sub.IsActive,
            sub.LastTriggeredAt
        );
    }

    public async Task<bool> DeleteSubscriptionAsync(long id, long userId, CancellationToken cancellationToken = default)
    {
        var sub = await _context.AlertSubscriptions
            .FirstOrDefaultAsync(a => a.Id == id && a.UserId == userId, cancellationToken);

        if (sub == null) return false;

        _context.AlertSubscriptions.Remove(sub);
        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<List<NotificationDto>> GetRecentNotificationsAsync(long userId, CancellationToken cancellationToken = default)
    {
        var notifications = await _context.AlertNotifications
            .AsNoTracking()
            .Include(n => n.AlertSubscription)
            .Where(n => n.AlertSubscription.UserId == userId)
            .OrderByDescending(n => n.CreatedAt)
            .Take(30)
            .ToListAsync(cancellationToken);

        return notifications.Select(n => new NotificationDto(
            n.Id,
            n.Title,
            n.Message,
            n.IsDelivered,
            n.IsRead,
            n.CreatedAt
        )).ToList();
    }
}
