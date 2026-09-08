using BistQuant.Domain.Entities;

namespace BistQuant.Application.Interfaces;

public interface IJwtService
{
    (string Token, DateTime ExpiresAt) GenerateToken(User user);
}
