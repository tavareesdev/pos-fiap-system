using PosFiap.Domain.Common;
using PosFiap.Domain.Exceptions;
using PosFiap.Domain.ValueObjects;

namespace PosFiap.Domain.Entities;

/// <summary>
/// Aggregate Root que representa um usuário (aluno) autenticado no sistema.
/// A senha nunca é armazenada em texto puro: apenas o hash é persistido,
/// gerado pela camada de infraestrutura (IPasswordHasher) e atribuído ao domínio.
/// </summary>
public class User : BaseEntity, IAggregateRoot
{
    public string Name { get; private set; } = null!;
    public Email Email { get; private set; } = null!;
    public string PasswordHash { get; private set; } = null!;
    public bool IsActive { get; private set; } = true;

    private User() { } // EF Core

    private User(string name, Email email, string passwordHash)
    {
        Name = name;
        Email = email;
        PasswordHash = passwordHash;
    }

    public static User Create(string name, Email email, string passwordHash)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("Nome do usuário é obrigatório.");

        if (string.IsNullOrWhiteSpace(passwordHash))
            throw new DomainException("Hash de senha inválido.");

        return new User(name.Trim(), email, passwordHash);
    }

    public void Deactivate() => IsActive = false;
}
