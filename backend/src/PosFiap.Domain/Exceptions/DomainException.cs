namespace PosFiap.Domain.Exceptions;

/// <summary>
/// Exceção lançada quando uma regra de negócio do domínio é violada.
/// </summary>
public class DomainException : Exception
{
    public DomainException(string message) : base(message) { }
}
