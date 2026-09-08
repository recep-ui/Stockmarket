using BistQuant.Application.Common.Interfaces;
using BistQuant.Application.DTOs.Watchlists;
using BistQuant.Domain.Entities;
using BistQuant.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BistQuant.Application.Services;

public interface IWatchlistService
{
    Task<List<WatchlistDto>> GetUserWatchlistsAsync(long userId, CancellationToken cancellationToken = default);
    Task<WatchlistDto?> GetWatchlistByIdAsync(long id, long userId, CancellationToken cancellationToken = default);
    Task<WatchlistDto> CreateWatchlistAsync(long userId, CreateWatchlistRequest request, CancellationToken cancellationToken = default);
    Task<WatchlistDto> UpdateWatchlistAsync(long id, long userId, UpdateWatchlistRequest request, CancellationToken cancellationToken = default);
    Task<bool> DeleteWatchlistAsync(long id, long userId, CancellationToken cancellationToken = default);
    Task<WatchlistDto> AddItemAsync(long id, long userId, AddWatchlistItemRequest request, CancellationToken cancellationToken = default);
    Task<bool> RemoveItemAsync(long id, long userId, int symbolId, CancellationToken cancellationToken = default);
}

public class WatchlistService : IWatchlistService
{
    private readonly IApplicationDbContext _context;
    private readonly ILogger<WatchlistService> _logger;

    public WatchlistService(IApplicationDbContext context, ILogger<WatchlistService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<List<WatchlistDto>> GetUserWatchlistsAsync(long userId, CancellationToken cancellationToken = default)
    {
        var watchlists = await _context.Watchlists
            .AsNoTracking()
            .Where(w => w.UserId == userId)
            .Include(w => w.Items)
                .ThenInclude(i => i.Symbol)
            .OrderBy(w => w.CreatedAt)
            .ToListAsync(cancellationToken);

        var result = new List<WatchlistDto>();
        foreach (var w in watchlists)
        {
            result.Add(await MapToDtoAsync(w, cancellationToken));
        }

        return result;
    }

    public async Task<WatchlistDto?> GetWatchlistByIdAsync(long id, long userId, CancellationToken cancellationToken = default)
    {
        var watchlist = await _context.Watchlists
            .AsNoTracking()
            .Include(w => w.Items)
                .ThenInclude(i => i.Symbol)
            .FirstOrDefaultAsync(w => w.Id == id && w.UserId == userId, cancellationToken);

        if (watchlist == null) return null;
        return await MapToDtoAsync(watchlist, cancellationToken);
    }

    public async Task<WatchlistDto> CreateWatchlistAsync(long userId, CreateWatchlistRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            throw new ArgumentException("Watchlist name is required.");

        var watchlist = new Watchlist
        {
            UserId = userId,
            Name = request.Name.Trim(),
            Description = request.Description?.Trim() ?? string.Empty
        };

        _context.Watchlists.Add(watchlist);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Created watchlist {WatchlistId} for user {UserId}", watchlist.Id, userId);
        return new WatchlistDto(watchlist.Id, watchlist.UserId, watchlist.Name, watchlist.Description, new List<WatchlistItemDto>());
    }

    public async Task<WatchlistDto> UpdateWatchlistAsync(long id, long userId, UpdateWatchlistRequest request, CancellationToken cancellationToken = default)
    {
        var watchlist = await _context.Watchlists
            .Include(w => w.Items)
                .ThenInclude(i => i.Symbol)
            .FirstOrDefaultAsync(w => w.Id == id && w.UserId == userId, cancellationToken);

        if (watchlist == null)
            throw new KeyNotFoundException($"Watchlist {id} not found or unauthorized.");

        if (string.IsNullOrWhiteSpace(request.Name))
            throw new ArgumentException("Watchlist name is required.");

        watchlist.Name = request.Name.Trim();
        if (request.Description != null)
            watchlist.Description = request.Description.Trim();

        await _context.SaveChangesAsync(cancellationToken);
        return await MapToDtoAsync(watchlist, cancellationToken);
    }

    public async Task<bool> DeleteWatchlistAsync(long id, long userId, CancellationToken cancellationToken = default)
    {
        var watchlist = await _context.Watchlists
            .FirstOrDefaultAsync(w => w.Id == id && w.UserId == userId, cancellationToken);

        if (watchlist == null) return false;

        _context.Watchlists.Remove(watchlist);
        await _context.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Deleted watchlist {WatchlistId} for user {UserId}", id, userId);
        return true;
    }

    public async Task<WatchlistDto> AddItemAsync(long id, long userId, AddWatchlistItemRequest request, CancellationToken cancellationToken = default)
    {
        var watchlist = await _context.Watchlists
            .Include(w => w.Items)
                .ThenInclude(i => i.Symbol)
            .FirstOrDefaultAsync(w => w.Id == id && w.UserId == userId, cancellationToken);

        if (watchlist == null)
            throw new KeyNotFoundException($"Watchlist {id} not found or unauthorized.");

        var ticker = request.Symbol.ToUpperInvariant().Trim();
        var symbol = await _context.Symbols
            .FirstOrDefaultAsync(s => s.Ticker == ticker, cancellationToken);

        if (symbol == null)
            throw new ArgumentException($"Symbol '{ticker}' not found.");

        if (!watchlist.Items.Any(i => i.SymbolId == symbol.Id))
        {
            var item = new WatchlistItem
            {
                WatchlistId = watchlist.Id,
                SymbolId = symbol.Id
            };
            watchlist.Items.Add(item);
            await _context.SaveChangesAsync(cancellationToken);
        }

        return await MapToDtoAsync(watchlist, cancellationToken);
    }

    public async Task<bool> RemoveItemAsync(long id, long userId, int symbolId, CancellationToken cancellationToken = default)
    {
        var watchlist = await _context.Watchlists
            .Include(w => w.Items)
            .FirstOrDefaultAsync(w => w.Id == id && w.UserId == userId, cancellationToken);

        if (watchlist == null) return false;

        var item = watchlist.Items.FirstOrDefault(i => i.SymbolId == symbolId);
        if (item == null) return false;

        watchlist.Items.Remove(item);
        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }

    private async Task<WatchlistDto> MapToDtoAsync(Watchlist w, CancellationToken cancellationToken)
    {
        var items = new List<WatchlistItemDto>();
        foreach (var item in w.Items)
        {
            var latestBar = await _context.PriceBars
                .AsNoTracking()
                .Where(p => p.SymbolId == item.SymbolId && p.Timeframe == Timeframe.Daily)
                .OrderByDescending(p => p.Timestamp)
                .Take(2)
                .ToListAsync(cancellationToken);

            decimal? currentPrice = latestBar.Count > 0 ? latestBar[0].Close : null;
            decimal? changePct = null;
            if (latestBar.Count >= 2 && latestBar[1].Close > 0)
            {
                changePct = Math.Round(((latestBar[0].Close - latestBar[1].Close) / latestBar[1].Close) * 100m, 2);
            }

            items.Add(new WatchlistItemDto(
                item.Id,
                item.SymbolId,
                item.Symbol?.Ticker ?? "",
                item.Symbol?.Name ?? "",
                currentPrice,
                changePct
            ));
        }

        return new WatchlistDto(
            w.Id,
            w.UserId,
            w.Name,
            w.Description,
            items
        );
    }
}
