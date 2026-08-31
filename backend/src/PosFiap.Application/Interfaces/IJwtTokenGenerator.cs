using PosFiap.Domain.Entities;

namespace PosFiap.Application.Interfaces;

/// <summary>
/// Abstrai a geração de tokens JWT para autenticação.
/// </summary>
public interface IJwtTokenGenerator
{
    string GenerateToken(User user);
}
