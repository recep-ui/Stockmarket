using BistQuant.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace BistQuant.Application.Common.Interfaces;

public interface IApplicationDbContext
{
    DbSet<Market> Markets { get; }
    DbSet<Symbol> Symbols { get; }
    DbSet<PriceBar> PriceBars { get; }
    DbSet<IndicatorSnapshot> IndicatorSnapshots { get; }
    DbSet<Signal> Signals { get; }
    DbSet<SignalReason> SignalReasons { get; }
    DbSet<Strategy> Strategies { get; }
    DbSet<StrategyRule> StrategyRules { get; }
    DbSet<BacktestRun> BacktestRuns { get; }
    DbSet<BacktestTrade> BacktestTrades { get; }
    DbSet<BacktestResult> BacktestResults { get; }
    DbSet<PaperPortfolio> PaperPortfolios { get; }
    DbSet<PaperPosition> PaperPositions { get; }
    DbSet<PaperOrder> PaperOrders { get; }
    DbSet<PaperTrade> PaperTrades { get; }
    DbSet<Watchlist> Watchlists { get; }
    DbSet<WatchlistItem> WatchlistItems { get; }
    DbSet<AlertSubscription> AlertSubscriptions { get; }
    DbSet<AlertNotification> AlertNotifications { get; }
    DbSet<User> Users { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}

public interface ICacheService
{
    Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default);
    Task SetAsync<T>(string key, T value, TimeSpan? expiration = null, CancellationToken cancellationToken = default);
    Task RemoveAsync(string key, CancellationToken cancellationToken = default);
}
