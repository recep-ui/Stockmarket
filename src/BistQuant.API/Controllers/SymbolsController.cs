using BistQuant.Application.Common.Interfaces;
using BistQuant.Application.Common.Models;
using BistQuant.Application.DTOs.MarketData;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BistQuant.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SymbolsController : ControllerBase
{
    private readonly IApplicationDbContext _context;

    public SymbolsController(IApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<IEnumerable<SymbolDto>>>> GetSymbols(
        [FromQuery] string? search = null,
        [FromQuery] string? sector = null,
        CancellationToken cancellationToken = default)
    {
        var query = _context.Symbols.AsNoTracking().Where(s => s.IsActive);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.ToUpperInvariant();
            query = query.Where(sym => sym.Ticker.Contains(s) || sym.Name.ToUpper().Contains(s));
        }

        if (!string.IsNullOrWhiteSpace(sector))
        {
            query = query.Where(sym => sym.Sector == sector);
        }

        var list = await query
            .OrderBy(s => s.Ticker)
            .Select(s => new SymbolDto(s.Id, s.Ticker, s.Name, s.Sector, s.Industry, s.IsActive))
            .ToListAsync(cancellationToken);

        return Ok(ApiResponse<IEnumerable<SymbolDto>>.Ok(list));
    }

    [HttpGet("{symbol}")]
    public async Task<ActionResult<ApiResponse<SymbolDto>>> GetSymbol(
        string symbol,
        CancellationToken cancellationToken = default)
    {
        var s = await _context.Symbols
            .AsNoTracking()
            .FirstOrDefaultAsync(sym => sym.Ticker == symbol.ToUpper(), cancellationToken);

        if (s == null)
        {
            return NotFound(ApiResponse<SymbolDto>.Fail($"Symbol '{symbol}' was not found."));
        }

        return Ok(ApiResponse<SymbolDto>.Ok(new SymbolDto(s.Id, s.Ticker, s.Name, s.Sector, s.Industry, s.IsActive)));
    }
}
