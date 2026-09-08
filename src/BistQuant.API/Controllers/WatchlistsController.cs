using System.Security.Claims;
using BistQuant.Application.Common.Models;
using BistQuant.Application.DTOs.Watchlists;
using BistQuant.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BistQuant.API.Controllers;

[ApiController]
[Authorize]
[Route("api/watchlists")]
public class WatchlistsController : ControllerBase
{
    private readonly IWatchlistService _watchlistService;

    public WatchlistsController(IWatchlistService watchlistService)
    {
        _watchlistService = watchlistService;
    }

    private long GetUserId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(claim) || !long.TryParse(claim, out var userId))
        {
            throw new UnauthorizedAccessException("Valid user ID claim is required.");
        }
        return userId;
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<List<WatchlistDto>>>> GetUserWatchlists(CancellationToken cancellationToken = default)
    {
        var watchlists = await _watchlistService.GetUserWatchlistsAsync(GetUserId(), cancellationToken);
        return Ok(ApiResponse<List<WatchlistDto>>.Ok(watchlists));
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<ApiResponse<WatchlistDto>>> GetWatchlist(long id, CancellationToken cancellationToken = default)
    {
        var watchlist = await _watchlistService.GetWatchlistByIdAsync(id, GetUserId(), cancellationToken);
        if (watchlist == null)
        {
            return NotFound(ApiResponse<WatchlistDto>.Fail($"Watchlist {id} not found or unauthorized."));
        }
        return Ok(ApiResponse<WatchlistDto>.Ok(watchlist));
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponse<WatchlistDto>>> CreateWatchlist(
        [FromBody] CreateWatchlistRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await _watchlistService.CreateWatchlistAsync(GetUserId(), request, cancellationToken);
        return CreatedAtAction(nameof(GetWatchlist), new { id = result.Id }, ApiResponse<WatchlistDto>.Ok(result, "Watchlist created."));
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<ApiResponse<WatchlistDto>>> UpdateWatchlist(
        long id,
        [FromBody] UpdateWatchlistRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _watchlistService.UpdateWatchlistAsync(id, GetUserId(), request, cancellationToken);
            return Ok(ApiResponse<WatchlistDto>.Ok(result, "Watchlist updated."));
        }
        catch (KeyNotFoundException)
        {
            return NotFound(ApiResponse<WatchlistDto>.Fail($"Watchlist {id} not found or unauthorized."));
        }
    }

    [HttpDelete("{id}")]
    public async Task<ActionResult<ApiResponse<bool>>> DeleteWatchlist(long id, CancellationToken cancellationToken = default)
    {
        var deleted = await _watchlistService.DeleteWatchlistAsync(id, GetUserId(), cancellationToken);
        if (!deleted)
        {
            return NotFound(ApiResponse<bool>.Fail($"Watchlist {id} not found or unauthorized."));
        }
        return Ok(ApiResponse<bool>.Ok(true, "Watchlist deleted."));
    }

    [HttpPost("{id}/items")]
    public async Task<ActionResult<ApiResponse<WatchlistDto>>> AddItem(
        long id,
        [FromBody] AddWatchlistItemRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _watchlistService.AddItemAsync(id, GetUserId(), request, cancellationToken);
            return Ok(ApiResponse<WatchlistDto>.Ok(result, "Item added to watchlist."));
        }
        catch (KeyNotFoundException)
        {
            return NotFound(ApiResponse<WatchlistDto>.Fail($"Watchlist {id} not found or unauthorized."));
        }
    }

    [HttpDelete("{id}/items/{symbolId}")]
    public async Task<ActionResult<ApiResponse<bool>>> RemoveItem(
        long id,
        int symbolId,
        CancellationToken cancellationToken = default)
    {
        var removed = await _watchlistService.RemoveItemAsync(id, GetUserId(), symbolId, cancellationToken);
        if (!removed)
        {
            return NotFound(ApiResponse<bool>.Fail($"Item {symbolId} not found in watchlist {id}."));
        }
        return Ok(ApiResponse<bool>.Ok(true, "Item removed from watchlist."));
    }
}
