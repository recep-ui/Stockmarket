namespace BistQuant.Application.DTOs.Auth;

public record RegisterRequest(
    string Email,
    string Password,
    string DisplayName
);

public record LoginRequest(
    string Email,
    string Password
);

public record AuthResponseDto(
    string Token,
    long UserId,
    string Email,
    string DisplayName,
    string Role,
    DateTime ExpiresAt
);

public record UserProfileDto(
    long Id,
    string Email,
    string DisplayName,
    string Role,
    string? TelegramChatId,
    bool IsActive
);

public record UpdateProfileRequest(
    string? DisplayName,
    string? TelegramChatId
);
