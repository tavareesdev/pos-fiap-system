namespace PosFiap.Domain.Interfaces;

/// <summary>
/// Abstrai a unidade de trabalho (transação) para persistir mudanças feitas
/// através dos repositórios de forma atômica.
/// </summary>
public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
