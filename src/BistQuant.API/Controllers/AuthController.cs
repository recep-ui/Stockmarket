using System.Security.Claims;
using BistQuant.Application.Common.Models;
using BistQuant.Application.DTOs.Auth;
using BistQuant.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BistQuant.API.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService)
    {
        _authService = authService;
    }

    [HttpPost("register")]
    public async Task<ActionResult<ApiResponse<AuthResponseDto>>> Register(
        [FromBody] RegisterRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await _authService.RegisterAsync(request, cancellationToken);
        return Ok(ApiResponse<AuthResponseDto>.Ok(result, "User registered successfully."));
    }

    [HttpPost("login")]
    public async Task<ActionResult<ApiResponse<AuthResponseDto>>> Login(
        [FromBody] LoginRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await _authService.LoginAsync(request, cancellationToken);
        return Ok(ApiResponse<AuthResponseDto>.Ok(result, "Login successful."));
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<ActionResult<ApiResponse<UserProfileDto>>> GetMe(CancellationToken cancellationToken = default)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !long.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized(ApiResponse<UserProfileDto>.Fail("Unauthorized: Invalid user claim."));
        }

        var profile = await _authService.GetProfileAsync(userId, cancellationToken);
        if (profile == null)
        {
            return NotFound(ApiResponse<UserProfileDto>.Fail("User not found."));
        }

        return Ok(ApiResponse<UserProfileDto>.Ok(profile));
    }

    [Authorize]
    [HttpPut("profile")]
    public async Task<ActionResult<ApiResponse<UserProfileDto>>> UpdateProfile(
        [FromBody] UpdateProfileRequest request,
        CancellationToken cancellationToken = default)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !long.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized(ApiResponse<UserProfileDto>.Fail("Unauthorized: Invalid user claim."));
        }

        var profile = await _authService.UpdateProfileAsync(userId, request, cancellationToken);
        if (profile == null)
        {
            return NotFound(ApiResponse<UserProfileDto>.Fail("User not found."));
        }

        return Ok(ApiResponse<UserProfileDto>.Ok(profile, "Profile updated successfully."));
    }
}
