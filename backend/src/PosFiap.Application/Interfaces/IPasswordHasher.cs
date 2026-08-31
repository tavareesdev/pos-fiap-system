namespace PosFiap.Application.Interfaces;

/// <summary>
/// Abstrai o algoritmo de hashing de senha usado pela camada de Infraestrutura.
/// </summary>
public interface IPasswordHasher
{
    string Hash(string plainPassword);
    bool Verify(string plainPassword, string passwordHash);
}
