using BistQuant.Application.Common.Interfaces;
using BistQuant.Application.Common.Models;
using BistQuant.Application.Interfaces;
using BistQuant.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BistQuant.API.Controllers;

public record ImportSessionRequest(DateOnly SessionDate, bool Force = false);
public record BackfillRequest(DateOnly StartDate, DateOnly EndDate, bool Force = false);

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
    private readonly IMarketDataProvider _marketDataProvider;
    private readonly ILogger<AdminMarketDataController> _logger;

    public AdminMarketDataController(
        IBistDailyBulletinMarketDataProvider bulletinProvider,
        IBistBulletinBackfillService backfillService,
        IMarketDataProvider marketDataProvider,
        ILogger<AdminMarketDataController> logger)
    {
        _bulletinProvider = bulletinProvider;
        _backfillService = backfillService;
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
    public async Task<ActionResult<ApiResponse<BackfillProgress>>> RunBackfill(
        [FromBody] BackfillRequest request,
        CancellationToken cancellationToken)
    {
        if (request.StartDate > request.EndDate)
        {
            return BadRequest(ApiResponse<BackfillProgress>.Fail("StartDate must be prior or equal to EndDate."));
        }

        if (request.EndDate > DateOnly.FromDateTime(DateTime.UtcNow))
        {
            return BadRequest(ApiResponse<BackfillProgress>.Fail("EndDate cannot be in the future."));
        }

        _logger.LogInformation("Admin triggered historical bulletin backfill from {Start} to {End}", request.StartDate, request.EndDate);
        var result = await _backfillService.BackfillRangeAsync(request.StartDate, request.EndDate, request.Force, null, cancellationToken);

        return Ok(ApiResponse<BackfillProgress>.Ok(result));
    }
}
