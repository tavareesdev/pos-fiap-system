using PosFiap.Domain.Common;
using PosFiap.Domain.Exceptions;

namespace PosFiap.Domain.Entities;

/// <summary>
/// Uma alternativa de uma questão de múltipla escolha.
/// </summary>
public class QuestionOption : BaseEntity
{
    public Guid QuestionId { get; private set; }
    public string Label { get; private set; } = null!;   // "A", "B", "C", "D"
    public string Text { get; private set; } = null!;
    public bool IsCorrect { get; private set; }

    private QuestionOption() { } // EF Core

    private QuestionOption(Guid questionId, string label, string text, bool isCorrect)
    {
        QuestionId = questionId;
        Label = label;
        Text = text;
        IsCorrect = isCorrect;
    }

    public static QuestionOption Create(Guid questionId, string label, string text, bool isCorrect)
    {
        if (string.IsNullOrWhiteSpace(text))
            throw new DomainException("Texto da alternativa não pode ser vazio.");

        return new QuestionOption(questionId, label, text, isCorrect);
    }
}
