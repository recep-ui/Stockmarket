using BistQuant.Application.Common.Interfaces;
using BistQuant.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace BistQuant.Infrastructure.Services.MarketData;

public class BistBulletinBackfillService : IBistBulletinBackfillService
{
    private readonly IBistDailyBulletinMarketDataProvider _bulletinProvider;
    private readonly IMarketSessionCalendar _sessionCalendar;
    private readonly ILogger<BistBulletinBackfillService> _logger;

    public BistBulletinBackfillService(
        IBistDailyBulletinMarketDataProvider bulletinProvider,
        IMarketSessionCalendar sessionCalendar,
        ILogger<BistBulletinBackfillService> logger)
    {
        _bulletinProvider = bulletinProvider;
        _sessionCalendar = sessionCalendar;
        _logger = logger;
    }

    public async Task<BackfillProgress> BackfillRangeAsync(
        DateOnly startDate,
        DateOnly endDate,
        bool forceRefresh = false,
        IProgress<BackfillProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (startDate > endDate)
        {
            throw new ArgumentException("Start date must be earlier than or equal to end date.");
        }

        var tradingDays = new List<DateOnly>();
        for (var d = startDate; d <= endDate; d = d.AddDays(1))
        {
            if (_sessionCalendar.IsTradingDay(d))
            {
                tradingDays.Add(d);
            }
        }

        int totalDays = tradingDays.Count;
        int processedDays = 0;
        int successCount = 0;
        int skippedCount = 0;
        int failedCount = 0;

        _logger.LogInformation("Starting BIST Daily Bulletin backfill from {Start} to {End}. Total trading sessions: {Total}", startDate, endDate, totalDays);

        foreach (var sessionDate in tradingDays)
        {
            cancellationToken.ThrowIfCancellationRequested();

            processedDays++;
            var stage = $"Processing {sessionDate} ({processedDays}/{totalDays})";
            progress?.Report(new BackfillProgress(startDate, endDate, totalDays, processedDays, successCount, skippedCount, failedCount, stage));

            try
            {
                var import = await _bulletinProvider.ImportBulletinForDateAsync(sessionDate, forceRefresh, cancellationToken);
                if (import.Status == MarketDataImportStatus.Success)
                {
                    successCount++;
                }
                else if (import.Status == MarketDataImportStatus.Skipped)
                {
                    skippedCount++;
                }
                else
                {
                    failedCount++;
                }
            }
            catch (Exception ex)
            {
                failedCount++;
                _logger.LogWarning(ex, "Failed to backfill session {Date}", sessionDate);
            }

            // Politeness delay between sequential remote downloads
            try
            {
                await Task.Delay(500, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        var finalProgress = new BackfillProgress(
            startDate,
            endDate,
            totalDays,
            processedDays,
            successCount,
            skippedCount,
            failedCount,
            "Completed"
        );

        _logger.LogInformation(
            "Backfill completed. Processed: {Processed}/{Total}, Success: {Success}, Skipped: {Skipped}, Failed: {Failed}",
            processedDays, totalDays, successCount, skippedCount, failedCount);

        return finalProgress;
    }
}
