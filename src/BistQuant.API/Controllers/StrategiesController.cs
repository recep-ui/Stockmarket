using System.Security.Claims;
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
        long? userId = long.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var parsed) ? parsed : null;
        var strategies = await _strategyEngine.GetAllStrategiesAsync(userId, cancellationToken);
        return Ok(ApiResponse<List<StrategyDto>>.Ok(strategies));
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<ApiResponse<StrategyDto>>> GetStrategy(int id, CancellationToken cancellationToken = default)
    {
        long? userId = long.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var parsed) ? parsed : null;
        var strategy = await _strategyEngine.GetStrategyByIdAsync(id, userId, cancellationToken);
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
        long? userId = long.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var parsed) ? parsed : null;
        var strategy = await _strategyEngine.CreateStrategyAsync(request, userId, cancellationToken);
        return CreatedAtAction(nameof(GetStrategy), new { id = strategy.Id }, ApiResponse<StrategyDto>.Ok(strategy));
    }

    [Authorize]
    [HttpDelete("{id}")]
    public async Task<ActionResult<ApiResponse<bool>>> DeleteStrategy(int id, CancellationToken cancellationToken = default)
    {
        long? userId = long.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var parsed) ? parsed : null;
        bool isAdmin = User.IsInRole("Admin");

        try
        {
            var success = await _strategyEngine.DeleteStrategyAsync(id, userId, isAdmin, cancellationToken);
            if (!success)
            {
                return NotFound(ApiResponse<bool>.Fail($"Strategy with ID {id} was not found."));
            }
            return Ok(ApiResponse<bool>.Ok(true, "Strategy deleted successfully."));
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(403, ApiResponse<bool>.Fail(ex.Message));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<bool>.Fail(ex.Message));
        }
    }
}
