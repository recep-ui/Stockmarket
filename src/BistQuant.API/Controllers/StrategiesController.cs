using BistQuant.Application.Common.Models;
using BistQuant.Application.DTOs.Strategies;
using BistQuant.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BistQuant.API.Controllers;

[ApiController]
[Route("api/strategies")]
public class StrategiesController : ControllerBase
{
    private readonly IStrategyEngine _strategyEngine;

    public StrategiesController(IStrategyEngine strategyEngine)
    {
        _strategyEngine = strategyEngine;
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<List<StrategyDto>>>> GetStrategies(CancellationToken cancellationToken = default)
    {
        var strategies = await _strategyEngine.GetAllStrategiesAsync(cancellationToken);
        return Ok(ApiResponse<List<StrategyDto>>.Ok(strategies));
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<ApiResponse<StrategyDto>>> GetStrategy(int id, CancellationToken cancellationToken = default)
    {
        var strategy = await _strategyEngine.GetStrategyByIdAsync(id, cancellationToken);
        if (strategy == null)
        {
            return NotFound(ApiResponse<StrategyDto>.Fail($"Strategy with ID {id} was not found."));
        }
        return Ok(ApiResponse<StrategyDto>.Ok(strategy));
    }

    [Authorize]
    [HttpPost]
    public async Task<ActionResult<ApiResponse<StrategyDto>>> CreateStrategy(
        [FromBody] CreateStrategyRequest request,
        CancellationToken cancellationToken = default)
    {
        var strategy = await _strategyEngine.CreateStrategyAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetStrategy), new { id = strategy.Id }, ApiResponse<StrategyDto>.Ok(strategy));
    }

    [Authorize]
    [HttpDelete("{id}")]
    public async Task<ActionResult<ApiResponse<bool>>> DeleteStrategy(int id, CancellationToken cancellationToken = default)
    {
        var success = await _strategyEngine.DeleteStrategyAsync(id, cancellationToken);
        if (!success)
        {
            return NotFound(ApiResponse<bool>.Fail($"Strategy with ID {id} was not found."));
        }
        return Ok(ApiResponse<bool>.Ok(true, "Strategy deleted successfully."));
    }
}
