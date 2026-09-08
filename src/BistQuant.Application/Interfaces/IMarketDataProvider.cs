using BistQuant.Application.DTOs.MarketData;
using BistQuant.Domain.Enums;

namespace BistQuant.Application.Interfaces;

public interface IMarketDataProvider
{
    Task<IEnumerable<SymbolDto>> GetSymbolsAsync(CancellationToken cancellationToken = default);

    Task<IEnumerable<PriceBarDto>> GetHistoricalBarsAsync(
        string symbol,
        DateTime start,
        DateTime end,
        Timeframe timeframe,
        CancellationToken cancellationToken = default);

    Task<PriceBarDto?> GetLatestBarAsync(
        string symbol,
        Timeframe timeframe,
        CancellationToken cancellationToken = default);
}
