using System.Security.Claims;
using BistQuant.Application.Common.Interfaces;
using BistQuant.Application.Common.Models;
using BistQuant.Application.DTOs.PaperTrading;
using BistQuant.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BistQuant.API.Controllers;

[ApiController]
[Authorize]
[Route("api/paper-portfolios")]
public class PaperTradingController : ControllerBase
{
    private readonly IPaperTradingService _paperTradingService;
    private readonly IApplicationDbContext _context;

    public PaperTradingController(IPaperTradingService paperTradingService, IApplicationDbContext context)
    {
        _paperTradingService = paperTradingService;
        _context = context;
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

    private async Task<bool> VerifyPortfolioOwnershipAsync(long portfolioId, long userId, CancellationToken cancellationToken)
    {
        return await _context.PaperPortfolios
            .AsNoTracking()
            .AnyAsync(p => p.Id == portfolioId && p.UserId == userId, cancellationToken);
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<PaperPortfolioDto>>> GetDefaultPortfolio(
        CancellationToken cancellationToken = default)
    {
        var portfolio = await _paperTradingService.GetOrCreateDefaultPortfolioAsync(GetUserId(), cancellationToken);
        return Ok(ApiResponse<PaperPortfolioDto>.Ok(portfolio));
    }

    [HttpGet("{id}/positions")]
    public async Task<ActionResult<ApiResponse<List<PaperPositionDto>>>> GetPositions(
        long id,
        CancellationToken cancellationToken = default)
    {
        var isOwner = await VerifyPortfolioOwnershipAsync(id, GetUserId(), cancellationToken);
        if (!isOwner)
        {
            return Forbid();
        }

        var positions = await _paperTradingService.GetPositionsAsync(id, cancellationToken);
        return Ok(ApiResponse<List<PaperPositionDto>>.Ok(positions));
    }

    [HttpGet("{id}/trades")]
    public async Task<ActionResult<ApiResponse<List<PaperTradeDto>>>> GetTrades(
        long id,
        CancellationToken cancellationToken = default)
    {
        var isOwner = await VerifyPortfolioOwnershipAsync(id, GetUserId(), cancellationToken);
        if (!isOwner)
        {
            return Forbid();
        }

        var trades = await _paperTradingService.GetTradesAsync(id, cancellationToken);
        return Ok(ApiResponse<List<PaperTradeDto>>.Ok(trades));
    }

    [HttpPost("orders")]
    public async Task<ActionResult<ApiResponse<PaperTradeDto>>> ExecuteOrder(
        [FromBody] CreatePaperOrderRequest request,
        CancellationToken cancellationToken = default)
    {
        var isOwner = await VerifyPortfolioOwnershipAsync(request.PortfolioId, GetUserId(), cancellationToken);
        if (!isOwner)
        {
            return Forbid();
        }

        var trade = await _paperTradingService.ExecuteOrderAsync(request, cancellationToken);
        return Ok(ApiResponse<PaperTradeDto>.Ok(trade, "Paper order executed successfully."));
    }

    [HttpPost("{id}/auto-trade")]
    public async Task<ActionResult<ApiResponse<bool>>> TriggerAutoTrade(
        long id,
        CancellationToken cancellationToken = default)
    {
        var isOwner = await VerifyPortfolioOwnershipAsync(id, GetUserId(), cancellationToken);
        if (!isOwner)
        {
            return Forbid();
        }

        await _paperTradingService.AutoTradeScanAsync(id, cancellationToken);
        return Ok(ApiResponse<bool>.Ok(true, "Auto-trade scan completed."));
    }
}
