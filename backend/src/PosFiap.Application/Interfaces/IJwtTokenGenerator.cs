using PosFiap.Domain.Entities;

namespace PosFiap.Application.Interfaces;

/// <summary>
/// Abstrai a geração de tokens JWT para autenticação e renovação de sessão.
/// </summary>
public interface IJwtTokenGenerator
{
    string GenerateToken(User user);
    string GenerateRefreshToken(User user);
}
