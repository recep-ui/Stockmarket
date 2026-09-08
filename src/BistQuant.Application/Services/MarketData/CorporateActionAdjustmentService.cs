using BistQuant.Application.Common.Interfaces;
using BistQuant.Domain.Entities;
using BistQuant.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace BistQuant.Application.Services.MarketData;

public class CorporateActionAdjustmentService : ICorporateActionAdjustmentService
{
    private readonly IApplicationDbContext _context;
    private readonly IConfiguration _configuration;
    private readonly ILogger<CorporateActionAdjustmentService> _logger;

    public CorporateActionAdjustmentService(
        IApplicationDbContext context,
        IConfiguration configuration,
        ILogger<CorporateActionAdjustmentService> logger)
    {
        _context = context;
        _configuration = configuration;
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

        bool autoAdjustmentEnabled = _configuration.GetValue<bool>("CorporateActions:AutomaticAdjustmentEnabled", false);
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

            var expectedPrevClose = record.PreviousLastPrice!.Value;
            var actualPrevClose = prevBar.Close;

            if (Math.Abs(expectedPrevClose - actualPrevClose) > 0.0001m)
            {
                var factor = expectedPrevClose / actualPrevClose;

                // Create and store indicator continuity warning
                var warning = new IndicatorContinuityWarning
                {
                    SymbolId = symbol.Id,
                    SessionDate = sessionDate,
                    CorporateActionRaw = record.CorporateAction!,
                    PreviousCloseReported = expectedPrevClose,
                    PreviousRawCloseInDb = actualPrevClose,
                    WarningMessage = $"Corporate action '{record.CorporateAction}' detected on session {sessionDate}. Bulletin expected previous close {expectedPrevClose} vs raw previous close {actualPrevClose} (Implied factor: {factor:F6}). Historical price bars preserved unadjusted.",
                    IsAcknowledged = false
                };

                _context.IndicatorContinuityWarnings.Add(warning);

                _logger.LogWarning(
                    "Corporate action detected for {Ticker} on {Date}. Action: {Action}, Factor: {Factor:F6} (DbPrev: {PrevClose} -> BulletinPrev: {ExpectedClose}). Automatic historical adjustment is {Status}.",
                    record.Ticker, sessionDate, record.CorporateAction, factor, actualPrevClose, expectedPrevClose,
                    autoAdjustmentEnabled ? "ENABLED" : "DISABLED (raw history preserved)");

                if (autoAdjustmentEnabled)
                {
                    // Update historical bars adjusted close ONLY if explicitly enabled
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
        }

        await _context.SaveChangesAsync(cancellationToken);
    }
}
