using BistQuant.Domain.Common;
using BistQuant.Domain.Enums;

namespace BistQuant.Domain.Entities;

public class User : BaseEntity<long>
{
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Role { get; set; } = "User"; // Admin, User
    public bool IsActive { get; set; } = true;
    public string? TelegramChatId { get; set; }

    public ICollection<Watchlist> Watchlists { get; set; } = new List<Watchlist>();
    public ICollection<AlertSubscription> AlertSubscriptions { get; set; } = new List<AlertSubscription>();
    public ICollection<PaperPortfolio> PaperPortfolios { get; set; } = new List<PaperPortfolio>();
}

public class Watchlist : BaseEntity<long>
{
    public long UserId { get; set; }
    public User User { get; set; } = null!;

    public string Name { get; set; } = "Takip Ettiklerim";
    public string Description { get; set; } = string.Empty;

    public ICollection<WatchlistItem> Items { get; set; } = new List<WatchlistItem>();
}

public class WatchlistItem : BaseEntity<long>
{
    public long WatchlistId { get; set; }
    public Watchlist Watchlist { get; set; } = null!;

    public int SymbolId { get; set; }
    public Symbol Symbol { get; set; } = null!;
}

public class AlertSubscription : BaseEntity<long>
{
    public long UserId { get; set; }
    public User User { get; set; } = null!;

    public int? SymbolId { get; set; }
    public Symbol? Symbol { get; set; }

    public int? StrategyId { get; set; }
    public Strategy? Strategy { get; set; }

    public string AlertType { get; set; } = "ScoreThreshold"; // ScoreThreshold, SignalChanged, PriceBreakout, RsiExtreme
    public string Condition { get; set; } = ">=";
    public decimal Value { get; set; } = 80m;

    public NotificationChannel Channel { get; set; } = NotificationChannel.InApp;
    public bool IsActive { get; set; } = true;

    public DateTime? LastTriggeredAt { get; set; }
    public ICollection<AlertNotification> Notifications { get; set; } = new List<AlertNotification>();
}

public class AlertNotification : BaseEntity<long>
{
    public long AlertSubscriptionId { get; set; }
    public AlertSubscription AlertSubscription { get; set; } = null!;

    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public bool IsDelivered { get; set; } = false;
    public bool IsRead { get; set; } = false;
    public DateTime? DeliveredAt { get; set; }
}
