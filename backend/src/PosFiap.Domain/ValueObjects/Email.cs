using System.Text.RegularExpressions;
using PosFiap.Domain.Exceptions;

namespace PosFiap.Domain.ValueObjects;

/// <summary>
/// Value Object que garante que um e-mail é sempre válido dentro do domínio.
/// </summary>
public sealed class Email : IEquatable<Email>
{
    private static readonly Regex EmailRegex = new(
        @"^[^@\s]+@[^@\s]+\.[^@\s]+$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public string Value { get; private set; } = null!;

    private Email() { } // EF Core

    private Email(string value) => Value = value;

    public static Email Create(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new DomainException("E-mail não pode ser vazio.");

        value = value.Trim().ToLowerInvariant();

        if (!EmailRegex.IsMatch(value))
            throw new DomainException($"E-mail '{value}' é inválido.");

        return new Email(value);
    }

    public bool Equals(Email? other) => other is not null && Value == other.Value;
    public override bool Equals(object? obj) => Equals(obj as Email);
    public override int GetHashCode() => Value.GetHashCode();
    public override string ToString() => Value;
}
