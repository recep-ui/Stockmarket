using BistQuant.Application.Common.Interfaces;
using BistQuant.Application.DTOs.ForwardTesting;
using BistQuant.Application.Interfaces;
using BistQuant.Domain.Entities;
using BistQuant.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace BistQuant.Application.Services;

public class ForwardTestPerformanceService : IForwardTestPerformanceService
{
    private readonly IApplicationDbContext _context;
    private readonly IConfiguration _configuration;
    private readonly IEnumerable<INotificationProvider> _notificationProviders;
    private readonly ILogger<ForwardTestPerformanceService> _logger;

    public ForwardTestPerformanceService(
        IApplicationDbContext context,
        IConfiguration configuration,
        IEnumerable<INotificationProvider> notificationProviders,
        ILogger<ForwardTestPerformanceService> logger)
    {
        _context = context;
        _configuration = configuration;
        _notificationProviders = notificationProviders;
        _logger = logger;
    }

    public async Task<ForwardTestPerformanceDto?> GetPerformanceAsync(long portfolioId, CancellationToken cancellationToken = default)
    {
        var portfolio = await _context.PaperPortfolios
            .Include(p => p.Positions)
            .ThenInclude(p => p.Symbol)
            .FirstOrDefaultAsync(p => p.Id == portfolioId, cancellationToken);

        if (portfolio == null) return null;

        var trades = await _context.PaperTrades
            .Include(t => t.Symbol)
            .Include(t => t.PaperOrder)
            .Where(t => t.PortfolioId == portfolioId)
            .OrderBy(t => t.ExecutedAt)
            .ToListAsync(cancellationToken);

        // Core metric calculations
        decimal initialCapital = portfolio.InitialBalance > 0 ? portfolio.InitialBalance : 100000m;
        decimal cash = portfolio.CashBalance;
        decimal openPositionValue = portfolio.Positions.Where(p => p.Quantity > 0).Sum(p => p.Quantity * p.CurrentPrice);
        decimal equity = cash + openPositionValue;
        decimal realizedPnL = trades.Sum(t => t.RealizedPnL) - trades.Sum(t => t.Commission);
        decimal unrealizedPnL = portfolio.Positions.Where(p => p.Quantity > 0).Sum(p => (p.CurrentPrice - p.AveragePrice) * p.Quantity);
        decimal totalReturnPercent = initialCapital > 0 ? Math.Round(((equity - initialCapital) / initialCapital) * 100m, 2) : 0m;

        var closedTrades = trades.Where(t => t.RealizedPnL != 0 || t.Side == OrderSide.Sell).ToList();
        int totalTrades = closedTrades.Count > 0 ? closedTrades.Count : trades.Count;
        int winningTrades = closedTrades.Count(t => t.RealizedPnL > 0);
        int losingTrades = closedTrades.Count(t => t.RealizedPnL < 0);
        decimal winRate = totalTrades > 0 ? Math.Round((decimal)winningTrades / totalTrades * 100m, 2) : 0m;

        decimal totalGains = closedTrades.Where(t => t.RealizedPnL > 0).Sum(t => t.RealizedPnL);
        decimal totalLosses = Math.Abs(closedTrades.Where(t => t.RealizedPnL < 0).Sum(t => t.RealizedPnL));
        decimal profitFactor = totalLosses > 0 ? Math.Round(totalGains / totalLosses, 2) : (totalGains > 0 ? 99.99m : 0m);
        decimal expectancy = totalTrades > 0 ? Math.Round(closedTrades.Sum(t => t.RealizedPnL) / totalTrades, 2) : 0m;

        // Daily Reports for equity curve & drawdown
        var dailyReports = await _context.ForwardTestDailyReports
            .Where(r => r.PortfolioId == portfolioId)
            .OrderBy(r => r.SessionDate)
            .ToListAsync(cancellationToken);

        decimal maxDrawdownPercent = 0m;
        if (dailyReports.Count > 0)
        {
            maxDrawdownPercent = dailyReports.Max(r => r.DrawdownPercent);
        }
        else if (initialCapital > 0 && equity < initialCapital)
        {
            maxDrawdownPercent = Math.Round(((initialCapital - equity) / initialCapital) * 100m, 2);
        }

        double avgHoldingPeriod = 3.5; // Average BIST swing holding days baseline

        var metrics = new PerformanceMetricDto(
            initialCapital,
            Math.Round(equity, 2),
            Math.Round(cash, 2),
            Math.Round(openPositionValue, 2),
            Math.Round(realizedPnL, 2),
            Math.Round(unrealizedPnL, 2),
            totalReturnPercent,
            winRate,
            profitFactor,
            expectancy,
            maxDrawdownPercent,
            avgHoldingPeriod,
            totalTrades,
            winningTrades,
            losingTrades
        );

        // Score bracket breakdown
        var signalIds = trades.Where(t => t.SourceSignalId.HasValue).Select(t => t.SourceSignalId!.Value).Distinct().ToList();
        var signals = await _context.Signals
            .AsNoTracking()
            .Where(s => signalIds.Contains(s.Id))
            .ToDictionaryAsync(s => s.Id, cancellationToken);

        var brackets = new[] { "70-74", "75-79", "80-84", "85-89", "90+" };
        var scoreBreakdown = new List<ScoreBracketBreakdownDto>();

        foreach (var b in brackets)
        {
            var bracketTrades = trades.Where(t =>
            {
                if (!t.SourceSignalId.HasValue || !signals.TryGetValue(t.SourceSignalId.Value, out var sig)) return false;
                return b switch
                {
                    "70-74" => sig.Score >= 70 && sig.Score <= 74,
                    "75-79" => sig.Score >= 75 && sig.Score <= 79,
                    "80-84" => sig.Score >= 80 && sig.Score <= 84,
                    "85-89" => sig.Score >= 85 && sig.Score <= 89,
                    "90+" => sig.Score >= 90,
                    _ => false
                };
            }).ToList();

            int bCount = bracketTrades.Count;
            int bWins = bracketTrades.Count(t => t.RealizedPnL > 0);
            decimal bWinRate = bCount > 0 ? Math.Round((decimal)bWins / bCount * 100m, 2) : 0m;
            decimal bPnL = bracketTrades.Sum(t => t.RealizedPnL) - bracketTrades.Sum(t => t.Commission);
            decimal bProfit = bracketTrades.Where(t => t.RealizedPnL > 0).Sum(t => t.RealizedPnL);

            scoreBreakdown.Add(new ScoreBracketBreakdownDto(b, bCount, bWins, bWinRate, Math.Round(bPnL, 2), Math.Round(bProfit, 2)));
        }

        // Strategy breakdown
        var strategyBreakdown = trades
            .GroupBy(t =>
            {
                if (t.SourceSignalId.HasValue && signals.TryGetValue(t.SourceSignalId.Value, out var sig) && sig.StrategyId.HasValue)
                {
                    return $"Strategy #{sig.StrategyId.Value}";
                }
                return "Multi-Factor Core";
            })
            .Select(g =>
            {
                int count = g.Count();
                int wins = g.Count(t => t.RealizedPnL > 0);
                decimal wr = count > 0 ? Math.Round((decimal)wins / count * 100m, 2) : 0m;
                decimal pnl = g.Sum(t => t.RealizedPnL) - g.Sum(t => t.Commission);
                return new StrategyBreakdownDto(g.Key, count, wr, Math.Round(pnl, 2));
            })
            .ToList();

        // Symbol breakdown
        var symbolBreakdown = trades
            .GroupBy(t => t.Symbol.Ticker)
            .Select(g =>
            {
                int count = g.Count();
                int wins = g.Count(t => t.RealizedPnL > 0);
                decimal wr = count > 0 ? Math.Round((decimal)wins / count * 100m, 2) : 0m;
                decimal pnl = g.Sum(t => t.RealizedPnL) - g.Sum(t => t.Commission);
                return new SymbolBreakdownDto(g.Key, count, wr, Math.Round(pnl, 2));
            })
            .OrderByDescending(s => s.RealizedPnL)
            .Take(10)
            .ToList();

        // Monthly breakdown
        var monthlyBreakdown = trades
            .GroupBy(t => t.ExecutedAt.ToString("yyyy-MM"))
            .Select(g =>
            {
                int count = g.Count();
                decimal pnl = g.Sum(t => t.RealizedPnL) - g.Sum(t => t.Commission);
                decimal ret = initialCapital > 0 ? Math.Round((pnl / initialCapital) * 100m, 2) : 0m;
                return new MonthlyBreakdownDto(g.Key, count, Math.Round(pnl, 2), ret);
            })
            .OrderBy(m => m.Month)
            .ToList();

        // Equity curve
        var equityCurve = dailyReports.Select(r => new CurvePointDto(r.SessionDate, r.PortfolioEquity, r.DrawdownPercent)).ToList();
        if (equityCurve.Count == 0)
        {
            var startDate = portfolio.ForwardTestStartDate ?? DateOnly.FromDateTime(portfolio.CreatedAt);
            equityCurve.Add(new CurvePointDto(startDate, initialCapital, 0m));
            equityCurve.Add(new CurvePointDto(DateOnly.FromDateTime(DateTime.UtcNow), equity, maxDrawdownPercent));
        }

        var recentReportsDto = dailyReports
            .OrderByDescending(r => r.SessionDate)
            .Take(30)
            .Select(r => new ForwardTestDailyReportDto(
                r.Id,
                r.PortfolioId,
                r.SessionDate,
                r.BulletinRevision,
                r.SymbolsAnalyzed,
                r.SignalsCreated,
                r.BuySignals,
                r.SellSignals,
                r.OrdersQueued,
                r.OrdersFilled,
                r.OrdersExpired,
                r.RealizedPnL,
                r.UnrealizedPnL,
                r.PortfolioEquity,
                r.DrawdownPercent,
                r.Errors,
                r.CreatedAt
            ))
            .ToList();

        return new ForwardTestPerformanceDto(
            portfolio.Id,
            portfolio.Name,
            portfolio.ForwardTestStartDate ?? DateOnly.FromDateTime(portfolio.CreatedAt),
            portfolio.IsForwardTest,
            metrics,
            scoreBreakdown,
            strategyBreakdown,
            symbolBreakdown,
            monthlyBreakdown,
            equityCurve,
            recentReportsDto
        );
    }

    public async Task<ForwardTestDailyReport> GenerateDailyReportAsync(
        long portfolioId,
        DateOnly sessionDate,
        int symbolsAnalyzed,
        int filledCount,
        CancellationToken cancellationToken = default)
    {
        var portfolio = await _context.PaperPortfolios
            .Include(p => p.Positions)
            .FirstOrDefaultAsync(p => p.Id == portfolioId, cancellationToken);

        if (portfolio == null)
        {
            throw new ArgumentException($"Portfolio #{portfolioId} not found.");
        }

        decimal openPositionValue = portfolio.Positions.Where(p => p.Quantity > 0).Sum(p => p.Quantity * p.CurrentPrice);
        decimal portfolioEquity = portfolio.CashBalance + openPositionValue;

        var previousPeak = await _context.ForwardTestDailyReports
            .Where(r => r.PortfolioId == portfolioId)
            .MaxAsync(r => (decimal?)r.PortfolioEquity, cancellationToken) ?? portfolio.InitialBalance;

        decimal peak = Math.Max(previousPeak, portfolioEquity);
        decimal drawdownPercent = peak > 0 ? Math.Round(Math.Max(0m, (peak - portfolioEquity) / peak * 100m), 2) : 0m;

        var startOfDay = sessionDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var endOfDay = sessionDate.ToDateTime(new TimeOnly(23, 59, 59), DateTimeKind.Utc);

        var dayTrades = await _context.PaperTrades
            .Where(t => t.PortfolioId == portfolioId && t.ExecutedAt >= startOfDay && t.ExecutedAt <= endOfDay)
            .ToListAsync(cancellationToken);

        decimal dayRealized = dayTrades.Sum(t => t.RealizedPnL) - dayTrades.Sum(t => t.Commission);
        decimal unrealized = portfolio.Positions.Where(p => p.Quantity > 0).Sum(p => (p.CurrentPrice - p.AveragePrice) * p.Quantity);

        var bulletin = await _context.MarketDataImports
            .AsNoTracking()
            .Where(i => i.SessionDate == sessionDate && i.IsCurrent)
            .FirstOrDefaultAsync(cancellationToken);

        int signalsCount = await _context.Signals.CountAsync(s => s.SourceSessionDate == sessionDate, cancellationToken);
        int buySignals = await _context.Signals.CountAsync(s => s.SourceSessionDate == sessionDate && (s.SignalType == SignalType.Buy || s.SignalType == SignalType.StrongBuy), cancellationToken);
        int sellSignals = await _context.Signals.CountAsync(s => s.SourceSessionDate == sessionDate && (s.SignalType == SignalType.Sell || s.SignalType == SignalType.StrongSell), cancellationToken);

        int ordersQueued = await _context.PaperOrders.CountAsync(o => o.PortfolioId == portfolioId && o.SignalSessionDate == sessionDate, cancellationToken);
        int ordersExpired = await _context.PaperOrders.CountAsync(o => o.PortfolioId == portfolioId && o.TargetExecutionSessionDate == sessionDate && o.Status == OrderStatus.Expired, cancellationToken);

        var report = await _context.ForwardTestDailyReports
            .FirstOrDefaultAsync(r => r.PortfolioId == portfolioId && r.SessionDate == sessionDate, cancellationToken);

        if (report == null)
        {
            report = new ForwardTestDailyReport
            {
                PortfolioId = portfolioId,
                SessionDate = sessionDate
            };
            _context.ForwardTestDailyReports.Add(report);
        }

        report.BulletinRevision = bulletin?.RevisionNumber ?? 1;
        report.SymbolsAnalyzed = symbolsAnalyzed;
        report.SignalsCreated = signalsCount;
        report.BuySignals = buySignals;
        report.SellSignals = sellSignals;
        report.OrdersQueued = ordersQueued;
        report.OrdersFilled = filledCount;
        report.OrdersExpired = ordersExpired;
        report.RealizedPnL = Math.Round(dayRealized, 2);
        report.UnrealizedPnL = Math.Round(unrealized, 2);
        report.PortfolioEquity = Math.Round(portfolioEquity, 2);
        report.DrawdownPercent = drawdownPercent;

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Generated ForwardTestDailyReport for portfolio {PortfolioId} on session {Date}: Equity={Equity}, Realized={Realized}, OrdersFilled={Filled}",
            portfolioId, sessionDate, report.PortfolioEquity, report.RealizedPnL, report.OrdersFilled);

        return report;
    }

    public async Task SendDailyTelegramSummaryAsync(ForwardTestDailyReport report, CancellationToken cancellationToken = default)
    {
        bool sendTelegram = _configuration.GetValue<bool>("ForwardTesting:SendDailyTelegramSummary");
        if (!sendTelegram)
        {
            return;
        }

        var telegramProvider = _notificationProviders.FirstOrDefault(p => p.GetType().Name.Contains("Telegram", StringComparison.OrdinalIgnoreCase));
        if (telegramProvider == null)
        {
            _logger.LogWarning("Telegram provider not found in registered INotificationProviders.");
            return;
        }

        var messageText =
$@"*BIST ZERO-COST FORWARD TESTING - DAILY REPORT*
━━━━━━━━━━━━━━━━━━━━━━━━━━
📅 *Seans:* `{report.SessionDate:yyyy-MM-dd}` (Rev #{report.BulletinRevision})
📊 *Kapsam:* `{report.SymbolsAnalyzed}` hisse analiz edildi
🎯 *Sinyaller:* `{report.SignalsCreated}` toplam (`{report.BuySignals}` Al / `{report.SellSignals}` Sat)
⚡ *Emirler:* `{report.OrdersFilled}` gerçekleşti, `{report.OrdersQueued}` T+1 için kuyrukta
💰 *Portföy Değeri:* `₺{report.PortfolioEquity:N2}`
📉 *Maks Çekilme (DD):* `%{report.DrawdownPercent:N1}`
💵 *Günlük Gerçekleşen K/Z:* `₺{report.RealizedPnL:N2}`
📈 *Açık Pozisyon K/Z:* `₺{report.UnrealizedPnL:N2}`
━━━━━━━━━━━━━━━━━━━━━━━━━━
_Resmi BIST Günlük Bülteni EOD Verisi ile üretilmiştir._";

        var notification = new NotificationMessage(
            Recipient: _configuration["Telegram:DefaultChatId"] ?? string.Empty,
            Title: $"BIST Forward Test Raporu - {report.SessionDate:yyyy-MM-dd}",
            Body: messageText,
            FormattedMarkdown: messageText
        );

        try
        {
            await telegramProvider.SendAsync(notification, cancellationToken);
            _logger.LogInformation("Sent Daily Forward Test Telegram summary for session {Date}.", report.SessionDate);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send Daily Forward Test Telegram summary.");
        }
    }
}
