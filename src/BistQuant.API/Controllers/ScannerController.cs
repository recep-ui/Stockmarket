using BistQuant.Application.Common.Models;
using BistQuant.Application.DTOs.Scanner;
using BistQuant.Application.Services;
using BistQuant.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BistQuant.API.Controllers;

[ApiController]
[Route("api/scanner")]
public class ScannerController : ControllerBase
{
    private readonly IMarketScannerService _scannerService;

    public ScannerController(IMarketScannerService scannerService)
    {
        _scannerService = scannerService;
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedResult<ScannerItemDto>>>> GetScannerResults(
        [FromQuery] Timeframe timeframe = Timeframe.Daily,
        [FromQuery] string? signal = null,
        [FromQuery] int? minScore = null,
        [FromQuery] string? sector = null,
        [FromQuery] decimal? minRsi = null,
        [FromQuery] decimal? maxRsi = null,
        [FromQuery] decimal? minVolumeRatio = null,
        [FromQuery] string? sortBy = "score",
        [FromQuery] string? sortDirection = "desc",
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var filter = new ScannerFilterDto(
            Timeframe: timeframe,
            Signal: signal,
            MinScore: minScore,
            Sector: sector,
            MinRsi: minRsi,
            MaxRsi: maxRsi,
            MinVolumeRatio: minVolumeRatio,
            SortBy: sortBy,
            SortDirection: sortDirection,
            Page: page,
            PageSize: pageSize
        );

        var result = await _scannerService.GetScannerResultsAsync(filter, cancellationToken);
        return Ok(ApiResponse<PagedResult<ScannerItemDto>>.Ok(result));
    }

    [HttpGet("overview")]
    public async Task<ActionResult<ApiResponse<MarketOverviewDto>>> GetMarketOverview(CancellationToken cancellationToken = default)
    {
        var overview = await _scannerService.GetMarketOverviewAsync(cancellationToken);
        return Ok(ApiResponse<MarketOverviewDto>.Ok(overview));
    }

    [Authorize]
    [HttpPost("run")]
    public async Task<ActionResult<ApiResponse<int>>> RunScanner(
        [FromQuery] Timeframe timeframe = Timeframe.Daily,
        CancellationToken cancellationToken = default)
    {
        var results = await _scannerService.ScanUniverseAsync(timeframe, cancellationToken);
        return Ok(ApiResponse<int>.Ok(results.Count, $"Successfully scanned {results.Count} symbols across {timeframe}."));
    }
}
