using BistQuant.Application.Common.Models;
using BistQuant.Application.DTOs.MarketData;
using BistQuant.Application.Interfaces;
using BistQuant.Domain.Enums;
using Microsoft.AspNetCore.Mvc;

namespace BistQuant.API.Controllers;

[ApiController]
[Route("api/market-data")]
public class MarketDataController : ControllerBase
{
    private readonly IMarketDataProvider _provider;

    public MarketDataController(IMarketDataProvider provider)
    {
        _provider = provider;
    }

    [HttpGet("{symbol}/history")]
    public async Task<ActionResult<ApiResponse<IEnumerable<PriceBarDto>>>> GetHistoricalBars(
        string symbol,
        [FromQuery] Timeframe timeframe = Timeframe.Daily,
        [FromQuery] DateTime? start = null,
        [FromQuery] DateTime? end = null,
        CancellationToken cancellationToken = default)
    {
        var startDate = start ?? DateTime.UtcNow.AddMonths(-6);
        var endDate = end ?? DateTime.UtcNow;

        var bars = await _provider.GetHistoricalBarsAsync(symbol, startDate, endDate, timeframe, cancellationToken);
        return Ok(ApiResponse<IEnumerable<PriceBarDto>>.Ok(bars));
    }

    [HttpGet("{symbol}/latest")]
    public async Task<ActionResult<ApiResponse<PriceBarDto>>> GetLatestBar(
        string symbol,
        [FromQuery] Timeframe timeframe = Timeframe.Daily,
        CancellationToken cancellationToken = default)
    {
        var bar = await _provider.GetLatestBarAsync(symbol, timeframe, cancellationToken);
        if (bar == null)
        {
            return NotFound(ApiResponse<PriceBarDto>.Fail($"Latest bar for '{symbol}' not found."));
        }

        return Ok(ApiResponse<PriceBarDto>.Ok(bar));
    }
}
