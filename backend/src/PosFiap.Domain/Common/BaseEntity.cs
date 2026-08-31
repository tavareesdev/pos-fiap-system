namespace PosFiap.Domain.Common;

/// <summary>
/// Base class para todas as entidades do domínio. Garante identidade por Id.
/// </summary>
public abstract class BaseEntity
{
    public Guid Id { get; protected set; } = Guid.NewGuid();
    public DateTime CreatedAtUtc { get; protected set; } = DateTime.UtcNow;

    protected BaseEntity() { }

    public override bool Equals(object? obj)
    {
        if (obj is not BaseEntity other) return false;
        if (ReferenceEquals(this, other)) return true;
        if (GetType() != other.GetType()) return false;
        return Id == other.Id;
    }

    public override int GetHashCode() => (GetType().ToString() + Id).GetHashCode();
}

/// <summary>
/// Marca uma entidade como raiz de agregado (Aggregate Root), único ponto de entrada
/// para modificações consistentes dentro do agregado (padrão DDD).
/// </summary>
public interface IAggregateRoot
{
}
