using PosFiap.Domain.Entities;
using PosFiap.Domain.ValueObjects;

namespace PosFiap.Domain.Interfaces;

/// <summary>
/// Porta (interface) de persistência para o agregado User.
/// A implementação concreta fica na camada de Infraestrutura (Dependency Inversion).
/// </summary>
public interface IUserRepository
{
    Task<User?> GetByEmailAsync(Email email, CancellationToken cancellationToken = default);
    Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task AddAsync(User user, CancellationToken cancellationToken = default);
    Task<bool> ExistsByEmailAsync(Email email, CancellationToken cancellationToken = default);
}
