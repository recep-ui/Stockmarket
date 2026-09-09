using System.Collections.Concurrent;
using BistQuant.Application.Common.Interfaces;
using BistQuant.Application.DTOs.Backfill;
using BistQuant.Domain.Entities;
using BistQuant.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace BistQuant.Infrastructure.Services.MarketData;

public class BistBulletinBackfillService : IBistBulletinBackfillService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IApplicationDbContext _context;
    private readonly IBistDailyBulletinMarketDataProvider _bulletinProvider;
    private readonly IMarketSessionCalendar _sessionCalendar;
    private readonly ILogger<BistBulletinBackfillService> _logger;

    private static readonly ConcurrentDictionary<long, CancellationTokenSource> ActiveJobTokens = new();

    public BistBulletinBackfillService(
        IServiceScopeFactory scopeFactory,
        IApplicationDbContext context,
        IBistDailyBulletinMarketDataProvider bulletinProvider,
        IMarketSessionCalendar sessionCalendar,
        ILogger<BistBulletinBackfillService> logger)
    {
        _scopeFactory = scopeFactory;
        _context = context;
        _bulletinProvider = bulletinProvider;
        _sessionCalendar = sessionCalendar;
        _logger = logger;
    }

    public async Task<BackfillJobDto> StartBackfillAsync(
        DateOnly startDate,
        DateOnly endDate,
        bool forceRevisionCheck = false,
        long? userId = null,
        CancellationToken cancellationToken = default)
    {
        if (startDate > endDate)
        {
            throw new ArgumentException("Start date must be earlier than or equal to end date.");
        }

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        if (endDate > today)
        {
            throw new ArgumentException("End date cannot be in the future.");
        }

        var tradingDays = new List<DateOnly>();
        for (var d = startDate; d <= endDate; d = d.AddDays(1))
        {
            if (_sessionCalendar.IsTradingDay(d))
            {
                tradingDays.Add(d);
            }
        }

        var job = new BackfillJob
        {
            StartDate = startDate,
            EndDate = endDate,
            CurrentDate = tradingDays.FirstOrDefault(),
            Status = BackfillJobStatus.Pending,
            SessionsTotal = tradingDays.Count,
            SessionsCompleted = 0,
            SessionsSkipped = 0,
            SessionsFailed = 0,
            BarsInserted = 0,
            StartedAt = DateTime.UtcNow,
            CreatedByUserId = userId
        };

        _context.BackfillJobs.Add(job);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Created BackfillJob #{JobId} for range {Start} to {End} ({Total} trading sessions).",
            job.Id, startDate, endDate, job.SessionsTotal);

        var cts = new CancellationTokenSource();
        ActiveJobTokens[job.Id] = cts;

        _ = Task.Run(() => ExecuteBackfillLoopAsync(job.Id, tradingDays, forceRevisionCheck, cts.Token));

        return ToDto(job);
    }

    public async Task<BackfillJobDto?> GetBackfillJobAsync(long jobId, CancellationToken cancellationToken = default)
    {
        var job = await _context.BackfillJobs
            .AsNoTracking()
            .FirstOrDefaultAsync(j => j.Id == jobId, cancellationToken);

        return job == null ? null : ToDto(job);
    }

    public async Task<List<BackfillJobDto>> GetBackfillJobsAsync(CancellationToken cancellationToken = default)
    {
        var jobs = await _context.BackfillJobs
            .AsNoTracking()
            .OrderByDescending(j => j.Id)
            .Take(50)
            .ToListAsync(cancellationToken);

        return jobs.Select(ToDto).ToList();
    }

    public async Task<bool> PauseBackfillAsync(long jobId, CancellationToken cancellationToken = default)
    {
        var job = await _context.BackfillJobs.FirstOrDefaultAsync(j => j.Id == jobId, cancellationToken);
        if (job == null || (job.Status != BackfillJobStatus.Running && job.Status != BackfillJobStatus.Pending))
        {
            return false;
        }

        job.Status = BackfillJobStatus.Paused;
        await _context.SaveChangesAsync(cancellationToken);

        if (ActiveJobTokens.TryRemove(jobId, out var cts))
        {
            cts.Cancel();
            cts.Dispose();
        }

        _logger.LogInformation("BackfillJob #{JobId} has been requested to pause.", jobId);
        return true;
    }

    public async Task<bool> ResumeBackfillAsync(long jobId, CancellationToken cancellationToken = default)
    {
        var job = await _context.BackfillJobs.FirstOrDefaultAsync(j => j.Id == jobId, cancellationToken);
        if (job == null || job.Status != BackfillJobStatus.Paused)
        {
            return false;
        }

        job.Status = BackfillJobStatus.Running;
        await _context.SaveChangesAsync(cancellationToken);

        var fromDate = job.CurrentDate ?? job.StartDate;
        var tradingDays = new List<DateOnly>();
        for (var d = fromDate; d <= job.EndDate; d = d.AddDays(1))
        {
            if (_sessionCalendar.IsTradingDay(d))
            {
                tradingDays.Add(d);
            }
        }

        var cts = new CancellationTokenSource();
        ActiveJobTokens[job.Id] = cts;

        _logger.LogInformation("Resuming BackfillJob #{JobId} from {CurrentDate} ({Remaining} sessions).",
            job.Id, fromDate, tradingDays.Count);

        _ = Task.Run(() => ExecuteBackfillLoopAsync(job.Id, tradingDays, false, cts.Token));

        return true;
    }

    public async Task<bool> CancelBackfillAsync(long jobId, CancellationToken cancellationToken = default)
    {
        var job = await _context.BackfillJobs.FirstOrDefaultAsync(j => j.Id == jobId, cancellationToken);
        if (job == null || job.Status == BackfillJobStatus.Completed || job.Status == BackfillJobStatus.Cancelled || job.Status == BackfillJobStatus.Failed)
        {
            return false;
        }

        job.Status = BackfillJobStatus.Cancelled;
        job.CompletedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);

        if (ActiveJobTokens.TryRemove(jobId, out var cts))
        {
            cts.Cancel();
            cts.Dispose();
        }

        _logger.LogInformation("BackfillJob #{JobId} has been cancelled.", jobId);
        return true;
    }

    private async Task ExecuteBackfillLoopAsync(
        long jobId,
        List<DateOnly> tradingDays,
        bool forceRevisionCheck,
        CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        var provider = scope.ServiceProvider.GetRequiredService<IBistDailyBulletinMarketDataProvider>();

        var job = await context.BackfillJobs.FirstOrDefaultAsync(j => j.Id == jobId);
        if (job == null) return;

        job.Status = BackfillJobStatus.Running;
        await context.SaveChangesAsync(CancellationToken.None);

        try
        {
            foreach (var sessionDate in tradingDays)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    _logger.LogInformation("BackfillJob #{JobId} cancellation or pause requested.", jobId);
                    return;
                }

                // Refresh job status from db in case user requested pause or cancel externally
                var currentStatus = await context.BackfillJobs
                    .Where(j => j.Id == jobId)
                    .Select(j => j.Status)
                    .FirstOrDefaultAsync(CancellationToken.None);

                if (currentStatus == BackfillJobStatus.Paused || currentStatus == BackfillJobStatus.Cancelled)
                {
                    return;
                }

                job.CurrentDate = sessionDate;

                try
                {
                    var import = await provider.ImportBulletinForDateAsync(sessionDate, forceRevisionCheck, cancellationToken);
                    if (import.Status == MarketDataImportStatus.Success)
                    {
                        job.SessionsCompleted++;
                        job.BarsInserted += import.PriceBarsInserted;
                    }
                    else if (import.Status == MarketDataImportStatus.Skipped)
                    {
                        job.SessionsSkipped++;
                    }
                    else
                    {
                        job.SessionsFailed++;
                        job.LastError = import.ErrorMessage;
                    }
                }
                catch (OperationCanceledException)
                {
                    return;
                }
                catch (Exception ex)
                {
                    job.SessionsFailed++;
                    job.LastError = ex.Message;
                    _logger.LogWarning(ex, "BackfillJob #{JobId}: failed to import bulletin for session {Date}", jobId, sessionDate);
                }

                await context.SaveChangesAsync(CancellationToken.None);

                // Politeness delay: min 2000 ms sequential delay between remote bulletin HTTP requests
                try
                {
                    await Task.Delay(2000, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
            }

            // Loop finished successfully
            job.CompletedAt = DateTime.UtcNow;
            if (job.SessionsFailed > 0 && job.SessionsCompleted > 0)
            {
                job.Status = BackfillJobStatus.CompletedWithErrors;
            }
            else if (job.SessionsFailed > 0 && job.SessionsCompleted == 0)
            {
                job.Status = BackfillJobStatus.Failed;
            }
            else
            {
                job.Status = BackfillJobStatus.Completed;
            }

            await context.SaveChangesAsync(CancellationToken.None);
            _logger.LogInformation("BackfillJob #{JobId} finished with status {Status}. Completed: {Completed}, Skipped: {Skipped}, Failed: {Failed}",
                jobId, job.Status, job.SessionsCompleted, job.SessionsSkipped, job.SessionsFailed);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("BackfillJob #{JobId} loop was cancelled.", jobId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "BackfillJob #{JobId} encountered fatal error.", jobId);
            job.Status = BackfillJobStatus.Failed;
            job.LastError = ex.Message;
            job.CompletedAt = DateTime.UtcNow;
            await context.SaveChangesAsync(CancellationToken.None);
        }
        finally
        {
            ActiveJobTokens.TryRemove(jobId, out _);
        }
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

        _logger.LogInformation("Starting synchronous BIST Daily Bulletin backfill from {Start} to {End}. Total trading sessions: {Total}", startDate, endDate, totalDays);

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

            // Politeness delay
            try
            {
                await Task.Delay(2000, cancellationToken);
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

    private static BackfillJobDto ToDto(BackfillJob job) => new(
        job.Id,
        job.StartDate,
        job.EndDate,
        job.CurrentDate,
        job.Status,
        job.SessionsTotal,
        job.SessionsCompleted,
        job.SessionsSkipped,
        job.SessionsFailed,
        job.BarsInserted,
        job.StartedAt,
        job.CompletedAt,
        job.LastError,
        job.CreatedByUserId
    );
}
