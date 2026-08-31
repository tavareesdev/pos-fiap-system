using PosFiap.Application.Interfaces;

namespace PosFiap.Infrastructure.Auth;

/// <summary>
/// Implementação de IPasswordHasher usando BCrypt.Net.
/// </summary>
public class BCryptPasswordHasher : IPasswordHasher
{
    public string Hash(string plainPassword) => BCrypt.Net.BCrypt.HashPassword(plainPassword, workFactor: 12);

    public bool Verify(string plainPassword, string passwordHash) =>
        BCrypt.Net.BCrypt.Verify(plainPassword, passwordHash);
}
