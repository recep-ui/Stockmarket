using BistQuant.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BistQuant.Infrastructure.Persistence.Configurations;

public class MarketConfiguration : IEntityTypeConfiguration<Market>
{
    public void Configure(EntityTypeBuilder<Market> builder)
    {
        builder.HasKey(m => m.Id);
        builder.Property(m => m.Code).HasMaxLength(20).IsRequired();
        builder.HasIndex(m => m.Code).IsUnique();
        builder.Property(m => m.Name).HasMaxLength(100).IsRequired();
        builder.Property(m => m.Currency).HasMaxLength(10).IsRequired();
        builder.Property(m => m.Timezone).HasMaxLength(50).IsRequired();
    }
}

public class SymbolConfiguration : IEntityTypeConfiguration<Symbol>
{
    public void Configure(EntityTypeBuilder<Symbol> builder)
    {
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Ticker).HasMaxLength(20).IsRequired();
        builder.HasIndex(s => s.Ticker).IsUnique();
        builder.Property(s => s.Name).HasMaxLength(200).IsRequired();
        builder.Property(s => s.Sector).HasMaxLength(100);
        builder.Property(s => s.Industry).HasMaxLength(100);

        builder.HasOne(s => s.Market)
               .WithMany(m => m.Symbols)
               .HasForeignKey(s => s.MarketId)
               .OnDelete(DeleteBehavior.Restrict);
    }
}

public class PriceBarConfiguration : IEntityTypeConfiguration<PriceBar>
{
    public void Configure(EntityTypeBuilder<PriceBar> builder)
    {
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Open).HasPrecision(18, 4);
        builder.Property(p => p.High).HasPrecision(18, 4);
        builder.Property(p => p.Low).HasPrecision(18, 4);
        builder.Property(p => p.Close).HasPrecision(18, 4);
        builder.Property(p => p.Volume).HasPrecision(24, 2);
        builder.Property(p => p.AdjustedClose).HasPrecision(18, 4);

        builder.HasIndex(p => new { p.SymbolId, p.Timeframe, p.Timestamp })
               .IsUnique()
               .HasDatabaseName("UIX_PriceBars_Symbol_Timeframe_Timestamp");

        builder.HasOne(p => p.Symbol)
               .WithMany(s => s.PriceBars)
               .HasForeignKey(p => p.SymbolId)
               .OnDelete(DeleteBehavior.Cascade);
    }
}

public class IndicatorSnapshotConfiguration : IEntityTypeConfiguration<IndicatorSnapshot>
{
    public void Configure(EntityTypeBuilder<IndicatorSnapshot> builder)
    {
        builder.HasKey(i => i.Id);

        builder.HasIndex(i => new { i.SymbolId, i.Timeframe, i.Timestamp })
               .IsUnique()
               .HasDatabaseName("UIX_IndicatorSnapshots_Symbol_Timeframe_Timestamp");

        builder.Property(i => i.EMA20).HasPrecision(18, 4);
        builder.Property(i => i.EMA50).HasPrecision(18, 4);
        builder.Property(i => i.EMA100).HasPrecision(18, 4);
        builder.Property(i => i.EMA200).HasPrecision(18, 4);
        builder.Property(i => i.SMA20).HasPrecision(18, 4);
        builder.Property(i => i.SMA50).HasPrecision(18, 4);
        builder.Property(i => i.SMA200).HasPrecision(18, 4);
        builder.Property(i => i.RSI14).HasPrecision(10, 4);
        builder.Property(i => i.MACD).HasPrecision(18, 4);
        builder.Property(i => i.MACDSignal).HasPrecision(18, 4);
        builder.Property(i => i.MACDHistogram).HasPrecision(18, 4);
        builder.Property(i => i.ATR14).HasPrecision(18, 4);
        builder.Property(i => i.ADX14).HasPrecision(10, 4);
        builder.Property(i => i.SuperTrend).HasPrecision(18, 4);
        builder.Property(i => i.BollingerUpper).HasPrecision(18, 4);
        builder.Property(i => i.BollingerMiddle).HasPrecision(18, 4);
        builder.Property(i => i.BollingerLower).HasPrecision(18, 4);
        builder.Property(i => i.VWAP).HasPrecision(18, 4);
        builder.Property(i => i.OBV).HasPrecision(24, 2);
        builder.Property(i => i.AverageVolume20).HasPrecision(24, 2);
        builder.Property(i => i.VolumeRatio).HasPrecision(10, 4);

        builder.HasOne(i => i.Symbol)
               .WithMany(s => s.IndicatorSnapshots)
               .HasForeignKey(i => i.SymbolId)
               .OnDelete(DeleteBehavior.Cascade);
    }
}

public class SignalConfiguration : IEntityTypeConfiguration<Signal>
{
    public void Configure(EntityTypeBuilder<Signal> builder)
    {
        builder.HasKey(s => s.Id);

        builder.Property(s => s.Price).HasPrecision(18, 4);
        builder.Property(s => s.StopLoss).HasPrecision(18, 4);
        builder.Property(s => s.TakeProfit1).HasPrecision(18, 4);
        builder.Property(s => s.TakeProfit2).HasPrecision(18, 4);
        builder.Property(s => s.RiskRewardRatio).HasPrecision(10, 2);
        builder.Property(s => s.Confidence).HasPrecision(5, 2);

        builder.HasIndex(s => new { s.Timeframe, s.CreatedAt, s.Score })
               .HasDatabaseName("IX_Signals_Timeframe_CreatedAt_Score");

        builder.HasOne(s => s.Symbol)
               .WithMany(sym => sym.Signals)
               .HasForeignKey(s => s.SymbolId)
               .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(s => s.Strategy)
               .WithMany(st => st.Signals)
               .HasForeignKey(s => s.StrategyId)
               .OnDelete(DeleteBehavior.SetNull);
    }
}

public class SignalReasonConfiguration : IEntityTypeConfiguration<SignalReason>
{
    public void Configure(EntityTypeBuilder<SignalReason> builder)
    {
        builder.HasKey(sr => sr.Id);
        builder.Property(sr => sr.Code).HasMaxLength(50).IsRequired();
        builder.Property(sr => sr.Title).HasMaxLength(150).IsRequired();
        builder.Property(sr => sr.Description).HasMaxLength(500);
        builder.Property(sr => sr.Indicator).HasMaxLength(50);
        builder.Property(sr => sr.IndicatorValue).HasPrecision(18, 4);

        builder.HasOne(sr => sr.Signal)
               .WithMany(s => s.Reasons)
               .HasForeignKey(sr => sr.SignalId)
               .OnDelete(DeleteBehavior.Cascade);
    }
}

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.HasKey(u => u.Id);
        builder.Property(u => u.Email).HasMaxLength(150).IsRequired();
        builder.HasIndex(u => u.Email).IsUnique();
        builder.Property(u => u.DisplayName).HasMaxLength(100);
        builder.Property(u => u.Role).HasMaxLength(50);
    }
}

public class StrategyConfiguration : IEntityTypeConfiguration<Strategy>
{
    public void Configure(EntityTypeBuilder<Strategy> builder)
    {
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Name).HasMaxLength(100).IsRequired();
        builder.Property(s => s.Description).HasMaxLength(500);
        builder.Property(s => s.StrategyType).HasMaxLength(50).IsRequired();
        builder.Property(s => s.IsSystem).HasDefaultValue(false);

        builder.HasOne(s => s.User)
               .WithMany()
               .HasForeignKey(s => s.UserId)
               .OnDelete(DeleteBehavior.SetNull);
    }
}

public class StrategyRuleConfiguration : IEntityTypeConfiguration<StrategyRule>
{
    public void Configure(EntityTypeBuilder<StrategyRule> builder)
    {
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Indicator).HasMaxLength(50).IsRequired();
        builder.Property(r => r.ComparisonIndicator).HasMaxLength(50);
        builder.Property(r => r.RuleGroup).HasMaxLength(50);
        builder.Property(r => r.Value).HasPrecision(18, 4);
        builder.Property(r => r.SecondaryValue).HasPrecision(18, 4);

        builder.HasOne(r => r.Strategy)
               .WithMany(s => s.Rules)
               .HasForeignKey(r => r.StrategyId)
               .OnDelete(DeleteBehavior.Cascade);
    }
}

public class BacktestRunConfiguration : IEntityTypeConfiguration<BacktestRun>
{
    public void Configure(EntityTypeBuilder<BacktestRun> builder)
    {
        builder.HasKey(b => b.Id);
        builder.Property(b => b.SymbolList).HasMaxLength(500);
        builder.Property(b => b.InitialCapital).HasPrecision(18, 2);
        builder.Property(b => b.CommissionRate).HasPrecision(10, 4);
        builder.Property(b => b.SlippageRate).HasPrecision(10, 4);
        builder.Property(b => b.ErrorMessage).HasMaxLength(1000);

        builder.HasOne(b => b.Strategy)
               .WithMany(s => s.BacktestRuns)
               .HasForeignKey(b => b.StrategyId)
               .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(b => b.Result)
               .WithOne(r => r.BacktestRun)
               .HasForeignKey<BacktestResult>(r => r.BacktestRunId)
               .OnDelete(DeleteBehavior.Cascade);
    }
}

public class BacktestTradeConfiguration : IEntityTypeConfiguration<BacktestTrade>
{
    public void Configure(EntityTypeBuilder<BacktestTrade> builder)
    {
        builder.HasKey(t => t.Id);
        builder.Property(t => t.EntryPrice).HasPrecision(18, 4);
        builder.Property(t => t.ExitPrice).HasPrecision(18, 4);
        builder.Property(t => t.Quantity).HasPrecision(18, 4);
        builder.Property(t => t.GrossPnL).HasPrecision(18, 4);
        builder.Property(t => t.NetPnL).HasPrecision(18, 4);
        builder.Property(t => t.ReturnPercent).HasPrecision(10, 4);
        builder.Property(t => t.ExitReason).HasMaxLength(50);

        builder.HasOne(t => t.BacktestRun)
               .WithMany(b => b.Trades)
               .HasForeignKey(t => t.BacktestRunId)
               .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(t => t.Symbol)
               .WithMany()
               .HasForeignKey(t => t.SymbolId)
               .OnDelete(DeleteBehavior.Restrict);
    }
}

public class BacktestResultConfiguration : IEntityTypeConfiguration<BacktestResult>
{
    public void Configure(EntityTypeBuilder<BacktestResult> builder)
    {
        builder.HasKey(r => r.Id);
        builder.Property(r => r.WinRate).HasPrecision(10, 4);
        builder.Property(r => r.TotalReturn).HasPrecision(10, 4);
        builder.Property(r => r.AnnualizedReturn).HasPrecision(10, 4);
        builder.Property(r => r.AverageWin).HasPrecision(18, 4);
        builder.Property(r => r.AverageLoss).HasPrecision(18, 4);
        builder.Property(r => r.ProfitFactor).HasPrecision(10, 4);
        builder.Property(r => r.MaxDrawdown).HasPrecision(10, 4);
        builder.Property(r => r.SharpeRatio).HasPrecision(10, 4);
        builder.Property(r => r.SortinoRatio).HasPrecision(10, 4);
        builder.Property(r => r.Expectancy).HasPrecision(18, 4);
    }
}

public class PaperPortfolioConfiguration : IEntityTypeConfiguration<PaperPortfolio>
{
    public void Configure(EntityTypeBuilder<PaperPortfolio> builder)
    {
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Name).HasMaxLength(100).IsRequired();
        builder.Property(p => p.InitialBalance).HasPrecision(18, 2);
        builder.Property(p => p.CashBalance).HasPrecision(18, 2);
        builder.Property(p => p.AutoTradingMaxAllocationPercent).HasPrecision(10, 2);

        builder.HasOne(p => p.User)
               .WithMany(u => u.PaperPortfolios)
               .HasForeignKey(p => p.UserId)
               .OnDelete(DeleteBehavior.Cascade);
    }
}

public class PaperPositionConfiguration : IEntityTypeConfiguration<PaperPosition>
{
    public void Configure(EntityTypeBuilder<PaperPosition> builder)
    {
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Quantity).HasPrecision(18, 4);
        builder.Property(p => p.AveragePrice).HasPrecision(18, 4);
        builder.Property(p => p.CurrentPrice).HasPrecision(18, 4);

        builder.HasOne(p => p.Portfolio)
               .WithMany(pf => pf.Positions)
               .HasForeignKey(p => p.PortfolioId)
               .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(p => p.Symbol)
               .WithMany()
               .HasForeignKey(p => p.SymbolId)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(p => new { p.PortfolioId, p.SymbolId }).IsUnique();
    }
}

public class PaperOrderConfiguration : IEntityTypeConfiguration<PaperOrder>
{
    public void Configure(EntityTypeBuilder<PaperOrder> builder)
    {
        builder.HasKey(o => o.Id);
        builder.Property(o => o.ClientOrderId).HasMaxLength(100);
        builder.Property(o => o.Quantity).HasPrecision(18, 4);
        builder.Property(o => o.LimitPrice).HasPrecision(18, 4);
        builder.Property(o => o.TargetPrice).HasPrecision(18, 4);
        builder.Property(o => o.StopLossPrice).HasPrecision(18, 4);
        builder.Property(o => o.FilledPrice).HasPrecision(18, 4);

        builder.HasOne(o => o.Portfolio)
               .WithMany(p => p.Orders)
               .HasForeignKey(o => o.PortfolioId)
               .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(o => o.Symbol)
               .WithMany()
               .HasForeignKey(o => o.SymbolId)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(o => new { o.PortfolioId, o.ClientOrderId })
               .IsUnique()
               .HasFilter("ClientOrderId IS NOT NULL");
    }
}

public class PaperTradeConfiguration : IEntityTypeConfiguration<PaperTrade>
{
    public void Configure(EntityTypeBuilder<PaperTrade> builder)
    {
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Quantity).HasPrecision(18, 4);
        builder.Property(t => t.Price).HasPrecision(18, 4);
        builder.Property(t => t.RealizedPnL).HasPrecision(18, 4);
        builder.Property(t => t.Commission).HasPrecision(18, 4);

        builder.HasOne(t => t.Portfolio)
               .WithMany(p => p.Trades)
               .HasForeignKey(t => t.PortfolioId)
               .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(t => t.Symbol)
               .WithMany()
               .HasForeignKey(t => t.SymbolId)
               .OnDelete(DeleteBehavior.Restrict);
    }
}

public class WatchlistConfiguration : IEntityTypeConfiguration<Watchlist>
{
    public void Configure(EntityTypeBuilder<Watchlist> builder)
    {
        builder.HasKey(w => w.Id);
        builder.Property(w => w.Name).HasMaxLength(100).IsRequired();
        builder.Property(w => w.Description).HasMaxLength(500);

        builder.HasOne(w => w.User)
               .WithMany(u => u.Watchlists)
               .HasForeignKey(w => w.UserId)
               .OnDelete(DeleteBehavior.Cascade);
    }
}

public class WatchlistItemConfiguration : IEntityTypeConfiguration<WatchlistItem>
{
    public void Configure(EntityTypeBuilder<WatchlistItem> builder)
    {
        builder.HasKey(w => w.Id);

        builder.HasOne(w => w.Watchlist)
               .WithMany(wl => wl.Items)
               .HasForeignKey(w => w.WatchlistId)
               .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(w => w.Symbol)
               .WithMany()
               .HasForeignKey(w => w.SymbolId)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(w => new { w.WatchlistId, w.SymbolId })
               .IsUnique();
    }
}

public class AlertSubscriptionConfiguration : IEntityTypeConfiguration<AlertSubscription>
{
    public void Configure(EntityTypeBuilder<AlertSubscription> builder)
    {
        builder.HasKey(a => a.Id);
        builder.Property(a => a.AlertType).HasMaxLength(50).IsRequired();
        builder.Property(a => a.Condition).HasMaxLength(20).IsRequired();
        builder.Property(a => a.Value).HasPrecision(18, 4);

        builder.HasOne(a => a.User)
               .WithMany(u => u.AlertSubscriptions)
               .HasForeignKey(a => a.UserId)
               .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(a => a.Symbol)
               .WithMany()
               .HasForeignKey(a => a.SymbolId)
               .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(a => a.Strategy)
               .WithMany()
               .HasForeignKey(a => a.StrategyId)
               .OnDelete(DeleteBehavior.SetNull);
    }
}

public class AlertNotificationConfiguration : IEntityTypeConfiguration<AlertNotification>
{
    public void Configure(EntityTypeBuilder<AlertNotification> builder)
    {
        builder.HasKey(n => n.Id);
        builder.Property(n => n.Title).HasMaxLength(150).IsRequired();
        builder.Property(n => n.Message).HasMaxLength(1000).IsRequired();

        builder.HasOne(n => n.AlertSubscription)
               .WithMany(a => a.Notifications)
               .HasForeignKey(n => n.AlertSubscriptionId)
               .OnDelete(DeleteBehavior.Cascade);
    }
}

public class WorkerHeartbeatConfiguration : IEntityTypeConfiguration<WorkerHeartbeat>
{
    public void Configure(EntityTypeBuilder<WorkerHeartbeat> builder)
    {
        builder.HasKey(w => w.Id);
        builder.Property(w => w.WorkerInstance).HasMaxLength(100).IsRequired();
        builder.Property(w => w.ScanType).HasMaxLength(50).IsRequired();
        builder.Property(w => w.ErrorMessage).HasMaxLength(1000);
        builder.HasIndex(w => new { w.Timeframe, w.CompletedAt });
    }
}

