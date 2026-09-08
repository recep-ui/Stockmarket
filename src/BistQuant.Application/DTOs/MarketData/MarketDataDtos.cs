using BistQuant.Domain.Enums;

namespace BistQuant.Application.DTOs.MarketData;

public record SymbolDto(
    int Id,
    string Ticker,
    string Name,
    string Sector,
    string Industry,
    bool IsActive
);

public record PriceBarDto(
    string Symbol,
    Timeframe Timeframe,
    DateTime Timestamp,
    decimal Open,
    decimal High,
    decimal Low,
    decimal Close,
    decimal Volume,
    decimal? AdjustedClose = null
);
