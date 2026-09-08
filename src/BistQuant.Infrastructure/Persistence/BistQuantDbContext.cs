using BistQuant.Application.Common.Interfaces;
using BistQuant.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace BistQuant.Infrastructure.Persistence;

public class BistQuantDbContext : DbContext, IApplicationDbContext
{
    public BistQuantDbContext(DbContextOptions<BistQuantDbContext> options) : base(options)
    {
    }

    public DbSet<Market> Markets => Set<Market>();
    public DbSet<Symbol> Symbols => Set<Symbol>();
    public DbSet<PriceBar> PriceBars => Set<PriceBar>();
    public DbSet<IndicatorSnapshot> IndicatorSnapshots => Set<IndicatorSnapshot>();
    public DbSet<Signal> Signals => Set<Signal>();
    public DbSet<SignalReason> SignalReasons => Set<SignalReason>();
    public DbSet<Strategy> Strategies => Set<Strategy>();
    public DbSet<StrategyRule> StrategyRules => Set<StrategyRule>();
    public DbSet<BacktestRun> BacktestRuns => Set<BacktestRun>();
    public DbSet<BacktestTrade> BacktestTrades => Set<BacktestTrade>();
    public DbSet<BacktestResult> BacktestResults => Set<BacktestResult>();
    public DbSet<PaperPortfolio> PaperPortfolios => Set<PaperPortfolio>();
    public DbSet<PaperPosition> PaperPositions => Set<PaperPosition>();
    public DbSet<PaperOrder> PaperOrders => Set<PaperOrder>();
    public DbSet<PaperTrade> PaperTrades => Set<PaperTrade>();
    public DbSet<Watchlist> Watchlists => Set<Watchlist>();
    public DbSet<WatchlistItem> WatchlistItems => Set<WatchlistItem>();
    public DbSet<AlertSubscription> AlertSubscriptions => Set<AlertSubscription>();
    public DbSet<AlertNotification> AlertNotifications => Set<AlertNotification>();
    public DbSet<User> Users => Set<User>();
    public DbSet<WorkerHeartbeat> WorkerHeartbeats => Set<WorkerHeartbeat>();

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        base.ConfigureConventions(configurationBuilder);
        // Default decimal convention: 18, 4
        configurationBuilder.Properties<decimal>().HavePrecision(18, 4);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(BistQuantDbContext).Assembly);
    }
}
