namespace BistQuant.Application.DTOs.Watchlists;

public record WatchlistItemDto(
    long Id,
    int SymbolId,
    string Ticker,
    string Name,
    decimal? CurrentPrice,
    decimal? ChangePercent
);

public record WatchlistDto(
    long Id,
    long UserId,
    string Name,
    string Description,
    List<WatchlistItemDto> Items
);

public record CreateWatchlistRequest(
    string Name,
    string? Description = null
);

public record UpdateWatchlistRequest(
    string Name,
    string? Description = null
);

public record AddWatchlistItemRequest(
    string Symbol
);
