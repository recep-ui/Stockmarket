using System.Security.Claims;
using BistQuant.Application.Common.Interfaces;
using BistQuant.Application.Common.Models;
using BistQuant.Application.DTOs.Backfill;
using BistQuant.Application.DTOs.MarketData;
using BistQuant.Application.Interfaces;
using BistQuant.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BistQuant.API.Controllers;

public record ImportSessionRequest(DateOnly SessionDate, bool Force = false);

public record MarketDataProviderStatusDto(
    string ProviderName,
    bool SupportsRealtime,
    bool SupportsEndOfWeekOrDay,
    IReadOnlyCollection<string> SupportedTimeframes,
    bool RequiresSessionClosure,
    string Description,
    MarketDataImport? LatestImport
);

[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/admin/market-data")]
public class AdminMarketDataController : ControllerBase
{
    private readonly IBistDailyBulletinMarketDataProvider _bulletinProvider;
    private readonly IBistBulletinBackfillService _backfillService;
    private readonly IMarketDataGapDetector _gapDetector;
    private readonly IMarketDataProvider _marketDataProvider;
    private readonly ILogger<AdminMarketDataController> _logger;

    public AdminMarketDataController(
        IBistDailyBulletinMarketDataProvider bulletinProvider,
        IBistBulletinBackfillService backfillService,
        IMarketDataGapDetector gapDetector,
        IMarketDataProvider marketDataProvider,
        ILogger<AdminMarketDataController> logger)
    {
        _bulletinProvider = bulletinProvider;
        _backfillService = backfillService;
        _gapDetector = gapDetector;
        _marketDataProvider = marketDataProvider;
        _logger = logger;
    }

    [HttpGet("status")]
    public async Task<ActionResult<ApiResponse<MarketDataProviderStatusDto>>> GetStatus(CancellationToken cancellationToken)
    {
        var capabilities = _marketDataProvider.Capabilities;
        var latestImport = await _bulletinProvider.GetLatestImportAsync(cancellationToken);

        var dto = new MarketDataProviderStatusDto(
            ProviderName: capabilities.ProviderName,
            SupportsRealtime: capabilities.SupportsRealtime,
            SupportsEndOfWeekOrDay: capabilities.SupportsEndOfWeekOrDay,
            SupportedTimeframes: capabilities.SupportedTimeframes.Select(t => t.ToString()).ToList(),
            RequiresSessionClosure: capabilities.RequiresSessionClosure,
            Description: capabilities.Description,
            LatestImport: latestImport
        );

        return Ok(ApiResponse<MarketDataProviderStatusDto>.Ok(dto));
    }

    [HttpGet("imports")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<MarketDataImport>>>> GetRecentImports(
        [FromQuery] int count = 30,
        CancellationToken cancellationToken = default)
    {
        var imports = await _bulletinProvider.GetRecentImportsAsync(Math.Clamp(count, 1, 100), cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<MarketDataImport>>.Ok(imports));
    }

    [HttpPost("import-session")]
    public async Task<ActionResult<ApiResponse<MarketDataImport>>> ImportSession(
        [FromBody] ImportSessionRequest request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Admin triggered manual bulletin import for session date {Date} (force: {Force})", request.SessionDate, request.Force);
        var result = await _bulletinProvider.ImportBulletinForDateAsync(request.SessionDate, request.Force, cancellationToken);
        return Ok(ApiResponse<MarketDataImport>.Ok(result));
    }

    [HttpPost("upload")]
    [RequestSizeLimit(30 * 1024 * 1024)] // 30 MB max
    public async Task<ActionResult<ApiResponse<MarketDataImport>>> UploadBulletin(
        IFormFile file,
        [FromQuery] string? sessionDateStr = null,
        CancellationToken cancellationToken = default)
    {
        if (file == null || file.Length == 0)
        {
            return BadRequest(ApiResponse<MarketDataImport>.Fail("File is empty or not provided."));
        }

        if (file.Length > 25 * 1024 * 1024)
        {
            return BadRequest(ApiResponse<MarketDataImport>.Fail("File size exceeds 25 MB maximum limit."));
        }

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (ext != ".csv" && ext != ".zip")
        {
            return BadRequest(ApiResponse<MarketDataImport>.Fail("Only .csv or .zip official bulletin files are accepted."));
        }

        using var stream = file.OpenReadStream();
        var headerBytes = new byte[32];
        var bytesRead = await stream.ReadAsync(headerBytes, 0, headerBytes.Length, cancellationToken);
        stream.Position = 0;

        if (ext == ".zip")
        {
            if (bytesRead < 4 || headerBytes[0] != 0x50 || headerBytes[1] != 0x4B)
            {
                return BadRequest(ApiResponse<MarketDataImport>.Fail("Uploaded .zip file does not match PK zip archive magic header signature."));
            }
        }
        else if (ext == ".csv")
        {
            var previewText = System.Text.Encoding.UTF8.GetString(headerBytes.Take(bytesRead).ToArray()).TrimStart();
            if (previewText.StartsWith("<!DOCTYPE", StringComparison.OrdinalIgnoreCase) ||
                previewText.StartsWith("<html", StringComparison.OrdinalIgnoreCase) ||
                previewText.StartsWith("<?xml", StringComparison.OrdinalIgnoreCase) ||
                (headerBytes[0] == 0x7F && headerBytes[1] == 0x45 && headerBytes[2] == 0x4C && headerBytes[3] == 0x46) ||
                (headerBytes[0] == 0x4D && headerBytes[1] == 0x5A))
            {
                return BadRequest(ApiResponse<MarketDataImport>.Fail("Uploaded .csv file contains invalid content (HTML, XML, or binary executable signature)."));
            }
        }

        DateOnly? dateHint = null;
        if (!string.IsNullOrWhiteSpace(sessionDateStr) && DateOnly.TryParse(sessionDateStr, out var parsedDate))
        {
            dateHint = parsedDate;
        }

        var import = await _bulletinProvider.ImportBulletinStreamAsync(dateHint, file.FileName, stream, cancellationToken);

        return Ok(ApiResponse<MarketDataImport>.Ok(import));
    }

    [HttpPost("backfill")]
    public async Task<ActionResult<ApiResponse<BackfillJobDto>>> StartBackfill(
        [FromBody] CreateBackfillJobRequest request,
        CancellationToken cancellationToken)
    {
        if (request.StartDate > request.EndDate)
        {
            return BadRequest(ApiResponse<BackfillJobDto>.Fail("StartDate must be prior or equal to EndDate."));
        }

        if (request.EndDate > DateOnly.FromDateTime(DateTime.UtcNow))
        {
            return BadRequest(ApiResponse<BackfillJobDto>.Fail("EndDate cannot be in the future."));
        }

        long? userId = long.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var uid) ? uid : null;

        _logger.LogInformation("Admin triggered historical bulletin backfill from {Start} to {End} (forceCheck: {Force})",
            request.StartDate, request.EndDate, request.ForceRevisionCheck);

        var job = await _backfillService.StartBackfillAsync(request.StartDate, request.EndDate, request.ForceRevisionCheck, userId, cancellationToken);
        return Ok(ApiResponse<BackfillJobDto>.Ok(job));
    }

    [HttpGet("backfill")]
    public async Task<ActionResult<ApiResponse<List<BackfillJobDto>>>> GetBackfillJobs(CancellationToken cancellationToken)
    {
        var jobs = await _backfillService.GetBackfillJobsAsync(cancellationToken);
        return Ok(ApiResponse<List<BackfillJobDto>>.Ok(jobs));
    }

    [HttpGet("backfill/{id:long}")]
    public async Task<ActionResult<ApiResponse<BackfillJobDto>>> GetBackfillJob(
        [FromRoute] long id,
        CancellationToken cancellationToken)
    {
        var job = await _backfillService.GetBackfillJobAsync(id, cancellationToken);
        if (job == null)
        {
            return NotFound(ApiResponse<BackfillJobDto>.Fail($"Backfill job #{id} not found."));
        }

        return Ok(ApiResponse<BackfillJobDto>.Ok(job));
    }

    [HttpPost("backfill/{id:long}/pause")]
    public async Task<ActionResult<ApiResponse<bool>>> PauseBackfill(
        [FromRoute] long id,
        CancellationToken cancellationToken)
    {
        var success = await _backfillService.PauseBackfillAsync(id, cancellationToken);
        if (!success)
        {
            return BadRequest(ApiResponse<bool>.Fail($"Cannot pause backfill job #{id}. Job must be in Running or Pending status."));
        }

        return Ok(ApiResponse<bool>.Ok(true));
    }

    [HttpPost("backfill/{id:long}/resume")]
    public async Task<ActionResult<ApiResponse<bool>>> ResumeBackfill(
        [FromRoute] long id,
        CancellationToken cancellationToken)
    {
        var success = await _backfillService.ResumeBackfillAsync(id, cancellationToken);
        if (!success)
        {
            return BadRequest(ApiResponse<bool>.Fail($"Cannot resume backfill job #{id}. Job must be in Paused status."));
        }

        return Ok(ApiResponse<bool>.Ok(true));
    }

    [HttpPost("backfill/{id:long}/cancel")]
    public async Task<ActionResult<ApiResponse<bool>>> CancelBackfill(
        [FromRoute] long id,
        CancellationToken cancellationToken)
    {
        var success = await _backfillService.CancelBackfillAsync(id, cancellationToken);
        if (!success)
        {
            return BadRequest(ApiResponse<bool>.Fail($"Cannot cancel backfill job #{id}. Job may already be completed or cancelled."));
        }

        return Ok(ApiResponse<bool>.Ok(true));
    }

    [HttpGet("coverage")]
    public async Task<ActionResult<ApiResponse<UniverseCoverageSummaryDto>>> GetUniverseCoverage(
        [FromQuery] DateOnly? startDate = null,
        [FromQuery] DateOnly? endDate = null,
        CancellationToken cancellationToken = default)
    {
        var summary = await _gapDetector.GetUniverseCoverageAsync(startDate, endDate, cancellationToken);
        return Ok(ApiResponse<UniverseCoverageSummaryDto>.Ok(summary));
    }

    [HttpGet("coverage/{symbol}/gaps")]
    public async Task<ActionResult<ApiResponse<SymbolGapsDto>>> GetSymbolGaps(
        [FromRoute] string symbol,
        [FromQuery] DateOnly? startDate = null,
        [FromQuery] DateOnly? endDate = null,
        CancellationToken cancellationToken = default)
    {
        var start = startDate ?? DateOnly.FromDateTime(DateTime.UtcNow.AddYears(-1));
        var end = endDate ?? DateOnly.FromDateTime(DateTime.UtcNow);

        var gaps = await _gapDetector.DetectGapsForSymbolAsync(symbol, start, end, cancellationToken);
        return Ok(ApiResponse<SymbolGapsDto>.Ok(gaps));
    }
}
