using BistQuant.Application.Common.Interfaces;
using BistQuant.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BistQuant.Application.Services.MarketData;

public class CorporateActionAdjustmentService : ICorporateActionAdjustmentService
{
    private readonly IApplicationDbContext _context;
    private readonly ILogger<CorporateActionAdjustmentService> _logger;

    public CorporateActionAdjustmentService(
        IApplicationDbContext context,
        ILogger<CorporateActionAdjustmentService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task ProcessCorporateActionsAsync(
        DateOnly sessionDate,
        IReadOnlyList<BistBulletinEquityRecord> records,
        CancellationToken cancellationToken = default)
    {
        var actionsToProcess = records
            .Where(r => !string.IsNullOrWhiteSpace(r.CorporateAction) && r.PreviousLastPrice.HasValue && r.PreviousLastPrice.Value > 0)
            .ToList();

        if (!actionsToProcess.Any())
        {
            return;
        }

        var sessionDateUtc = sessionDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);

        foreach (var record in actionsToProcess)
        {
            var symbol = await _context.Symbols
                .FirstOrDefaultAsync(s => s.Ticker == record.Ticker, cancellationToken);

            if (symbol == null) continue;

            // Find latest previous bar before session date
            var prevBar = await _context.PriceBars
                .Where(b => b.SymbolId == symbol.Id && b.Timeframe == Timeframe.Daily && b.Timestamp < sessionDateUtc)
                .OrderByDescending(b => b.Timestamp)
                .FirstOrDefaultAsync(cancellationToken);

            if (prevBar == null || prevBar.Close <= 0) continue;

            // Ratio of adjusted previous last price to raw unadjusted previous close
            var expectedPrevClose = record.PreviousLastPrice!.Value;
            var actualPrevClose = prevBar.Close;

            if (Math.Abs(expectedPrevClose - actualPrevClose) > 0.0001m)
            {
                var factor = expectedPrevClose / actualPrevClose;
                _logger.LogInformation(
                    "Applying corporate action adjustment for {Ticker} on {Date}. Action: {Action}, Factor: {Factor:F6} (Prev: {PrevClose} -> Adj: {ExpectedClose})",
                    record.Ticker, sessionDate, record.CorporateAction, factor, actualPrevClose, expectedPrevClose);

                // Update historical bars adjusted close
                var historicalBars = await _context.PriceBars
                    .Where(b => b.SymbolId == symbol.Id && b.Timeframe == Timeframe.Daily && b.Timestamp <= prevBar.Timestamp)
                    .ToListAsync(cancellationToken);

                foreach (var bar in historicalBars)
                {
                    var basePrice = bar.AdjustedClose ?? bar.Close;
                    bar.AdjustedClose = Math.Round(basePrice * factor, 4);
                }
            }
        }

        await _context.SaveChangesAsync(cancellationToken);
    }
}
