using System.Text.RegularExpressions;
using BistQuant.Application.Common.Interfaces;
using BistQuant.Application.Common.Security;
using BistQuant.Application.DTOs.Auth;
using BistQuant.Application.Interfaces;
using BistQuant.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace BistQuant.Application.Services;

public interface IAuthService
{
    Task<AuthResponseDto> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default);
    Task<AuthResponseDto> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default);
    Task<UserProfileDto?> GetProfileAsync(long userId, CancellationToken cancellationToken = default);
    Task<UserProfileDto?> UpdateProfileAsync(long userId, UpdateProfileRequest request, CancellationToken cancellationToken = default);
}

public class AuthService : IAuthService
{
    private readonly IApplicationDbContext _context;
    private readonly IJwtService _jwtService;

    public AuthService(IApplicationDbContext context, IJwtService jwtService)
    {
        _context = context;
        _jwtService = jwtService;
    }

    public async Task<AuthResponseDto> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || !Regex.IsMatch(request.Email, @"^[^@\s]+@[^@\s]+\.[^@\s]+$"))
        {
            throw new ArgumentException("A valid email address is required.");
        }

        if (string.IsNullOrWhiteSpace(request.DisplayName))
        {
            throw new ArgumentException("Display name is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < 8)
        {
            throw new ArgumentException("Password must be at least 8 characters long.");
        }

        if (!request.Password.Any(char.IsUpper) || !request.Password.Any(char.IsLower) || !request.Password.Any(char.IsDigit))
        {
            throw new ArgumentException("Password must contain at least one uppercase letter, one lowercase letter, and one number.");
        }

        var normalizedEmail = request.Email.Trim().ToLowerInvariant();
        var existingUser = await _context.Users.FirstOrDefaultAsync(u => u.Email.ToLower() == normalizedEmail, cancellationToken);
        if (existingUser != null)
        {
            throw new InvalidOperationException("A user with this email already exists.");
        }

        var user = new User
        {
            Email = normalizedEmail,
            PasswordHash = PasswordHasher.HashPassword(request.Password),
            DisplayName = request.DisplayName.Trim(),
            Role = "User",
            IsActive = true
        };

        _context.Users.Add(user);
        await _context.SaveChangesAsync(cancellationToken);

        var (token, expiresAt) = _jwtService.GenerateToken(user);
        return new AuthResponseDto(token, user.Id, user.Email, user.DisplayName, user.Role, expiresAt);
    }

    public async Task<AuthResponseDto> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        var normalizedEmail = request.Email.Trim().ToLowerInvariant();
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Email.ToLower() == normalizedEmail, cancellationToken);
        if (user == null || !PasswordHasher.VerifyPassword(request.Password, user.PasswordHash))
        {
            throw new UnauthorizedAccessException("Invalid email or password.");
        }

        if (!user.IsActive)
        {
            throw new UnauthorizedAccessException("Account is disabled. Please contact system administrator.");
        }

        var (token, expiresAt) = _jwtService.GenerateToken(user);
        return new AuthResponseDto(token, user.Id, user.Email, user.DisplayName, user.Role, expiresAt);
    }

    public async Task<UserProfileDto?> GetProfileAsync(long userId, CancellationToken cancellationToken = default)
    {
        var user = await _context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
        if (user == null) return null;

        return new UserProfileDto(user.Id, user.Email, user.DisplayName, user.Role, user.TelegramChatId, user.IsActive);
    }

    public async Task<UserProfileDto?> UpdateProfileAsync(long userId, UpdateProfileRequest request, CancellationToken cancellationToken = default)
    {
        var user = await _context.Users.FindAsync(new object[] { userId }, cancellationToken);
        if (user == null) return null;

        if (!string.IsNullOrWhiteSpace(request.DisplayName))
            user.DisplayName = request.DisplayName.Trim();

        if (request.TelegramChatId != null)
            user.TelegramChatId = request.TelegramChatId.Trim();

        await _context.SaveChangesAsync(cancellationToken);
        return new UserProfileDto(user.Id, user.Email, user.DisplayName, user.Role, user.TelegramChatId, user.IsActive);
    }
}
