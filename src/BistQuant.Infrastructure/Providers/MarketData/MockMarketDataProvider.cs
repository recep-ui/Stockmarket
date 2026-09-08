using BistQuant.Application.Common.Interfaces;
using BistQuant.Application.DTOs.MarketData;
using BistQuant.Application.Interfaces;
using BistQuant.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace BistQuant.Infrastructure.Providers.MarketData;

public class MockMarketDataProvider : IMarketDataProvider
{
    private readonly IApplicationDbContext _context;

    public MarketDataProviderCapabilities Capabilities => new(
        ProviderName: "Mock / Database Provider",
        SupportsRealtime: false,
        SupportsEndOfWeekOrDay: true,
        SupportedTimeframes: new[] { Timeframe.Daily, Timeframe.H1, Timeframe.M15 },
        RequiresSessionClosure: false,
        Description: "Mock and local database price bar provider for development and testing."
    );

    public MockMarketDataProvider(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<SymbolDto>> GetSymbolsAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Symbols
            .AsNoTracking()
            .Where(s => s.IsActive)
            .OrderBy(s => s.Ticker)
            .Select(s => new SymbolDto(s.Id, s.Ticker, s.Name, s.Sector, s.Industry, s.IsActive))
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<PriceBarDto>> GetHistoricalBarsAsync(
        string symbol,
        DateTime start,
        DateTime end,
        Timeframe timeframe,
        CancellationToken cancellationToken = default)
    {
        var sym = await _context.Symbols
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Ticker == symbol.ToUpper(), cancellationToken);

        if (sym == null)
        {
            return Enumerable.Empty<PriceBarDto>();
        }

        return await _context.PriceBars
            .AsNoTracking()
            .Where(p => p.SymbolId == sym.Id && p.Timeframe == timeframe && p.Timestamp >= start && p.Timestamp <= end)
            .OrderBy(p => p.Timestamp)
            .Select(p => new PriceBarDto(
                sym.Ticker,
                p.Timeframe,
                p.Timestamp,
                p.Open,
                p.High,
                p.Low,
                p.Close,
                p.Volume,
                p.AdjustedClose))
            .ToListAsync(cancellationToken);
    }

    public async Task<PriceBarDto?> GetLatestBarAsync(
        string symbol,
        Timeframe timeframe,
        CancellationToken cancellationToken = default)
    {
        var sym = await _context.Symbols
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Ticker == symbol.ToUpper(), cancellationToken);

        if (sym == null)
        {
            return null;
        }

        var latest = await _context.PriceBars
            .AsNoTracking()
            .Where(p => p.SymbolId == sym.Id && p.Timeframe == timeframe)
            .OrderByDescending(p => p.Timestamp)
            .FirstOrDefaultAsync(cancellationToken);

        if (latest == null)
        {
            return null;
        }

        return new PriceBarDto(
            sym.Ticker,
            latest.Timeframe,
            latest.Timestamp,
            latest.Open,
            latest.High,
            latest.Low,
            latest.Close,
            latest.Volume,
            latest.AdjustedClose);
    }
}
