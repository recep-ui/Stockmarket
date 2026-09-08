using System.Security.Claims;
using BistQuant.Application.Common.Models;
using BistQuant.Application.DTOs.Alerts;
using BistQuant.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BistQuant.API.Controllers;

[ApiController]
[Authorize]
[Route("api/alerts")]
public class AlertsController : ControllerBase
{
    private readonly IAlertEngine _alertEngine;

    public AlertsController(IAlertEngine alertEngine)
    {
        _alertEngine = alertEngine;
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
    public async Task<ActionResult<ApiResponse<List<AlertSubscriptionDto>>>> GetSubscriptions(
        CancellationToken cancellationToken = default)
    {
        var alerts = await _alertEngine.GetSubscriptionsAsync(GetUserId(), cancellationToken);
        return Ok(ApiResponse<List<AlertSubscriptionDto>>.Ok(alerts));
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponse<AlertSubscriptionDto>>> CreateSubscription(
        [FromBody] CreateAlertRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await _alertEngine.CreateSubscriptionAsync(GetUserId(), request, cancellationToken);
        return Ok(ApiResponse<AlertSubscriptionDto>.Ok(result, "Alert created successfully."));
    }

    [HttpDelete("{id}")]
    public async Task<ActionResult<ApiResponse<bool>>> DeleteSubscription(long id, CancellationToken cancellationToken = default)
    {
        var deleted = await _alertEngine.DeleteSubscriptionAsync(id, GetUserId(), cancellationToken);
        if (!deleted)
        {
            return NotFound(ApiResponse<bool>.Fail("Alert subscription not found or unauthorized."));
        }
        return Ok(ApiResponse<bool>.Ok(true, "Alert deleted successfully."));
    }

    [HttpGet("notifications")]
    public async Task<ActionResult<ApiResponse<List<NotificationDto>>>> GetNotifications(CancellationToken cancellationToken = default)
    {
        var notifications = await _alertEngine.GetRecentNotificationsAsync(GetUserId(), cancellationToken);
        return Ok(ApiResponse<List<NotificationDto>>.Ok(notifications));
    }
}
