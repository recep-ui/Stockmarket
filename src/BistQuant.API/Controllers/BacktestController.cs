using BistQuant.Application.Common.Models;
using BistQuant.Application.DTOs.Backtests;
using BistQuant.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BistQuant.API.Controllers;

[ApiController]
[Route("api/backtests")]
public class BacktestController : ControllerBase
{
    private readonly IBacktestEngine _backtestEngine;

    public BacktestController(IBacktestEngine backtestEngine)
    {
        _backtestEngine = backtestEngine;
    }

    [Authorize]
    [HttpPost]
    public async Task<ActionResult<ApiResponse<BacktestRunDto>>> RunBacktest(
        [FromBody] BacktestRunRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await _backtestEngine.RunBacktestAsync(request, cancellationToken);
        return Ok(ApiResponse<BacktestRunDto>.Ok(result, "Backtest completed successfully."));
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<ApiResponse<BacktestRunDto>>> GetBacktest(long id, CancellationToken cancellationToken = default)
    {
        var result = await _backtestEngine.GetBacktestByIdAsync(id, cancellationToken);
        if (result == null)
        {
            return NotFound(ApiResponse<BacktestRunDto>.Fail($"Backtest run {id} was not found."));
        }
        return Ok(ApiResponse<BacktestRunDto>.Ok(result));
    }

    [HttpGet("{id}/trades")]
    public async Task<ActionResult<ApiResponse<List<BacktestTradeDto>>>> GetBacktestTrades(long id, CancellationToken cancellationToken = default)
    {
        var trades = await _backtestEngine.GetBacktestTradesAsync(id, cancellationToken);
        return Ok(ApiResponse<List<BacktestTradeDto>>.Ok(trades));
    }
}
