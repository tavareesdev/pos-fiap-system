using PosFiap.Domain.Common;
using PosFiap.Domain.Exceptions;

namespace PosFiap.Domain.Entities;

/// <summary>
/// Uma questão de múltipla escolha pertencente a uma Exam (prova).
/// </summary>
public class Question : BaseEntity
{
    private readonly List<QuestionOption> _options = new();

    public Guid ExamId { get; private set; }
    public int Order { get; private set; }
    public string Statement { get; private set; } = null!;
    public string Explanation { get; private set; } = string.Empty;

    public IReadOnlyCollection<QuestionOption> Options => _options.AsReadOnly();

    private Question() { } // EF Core

    private Question(Guid examId, int order, string statement, string explanation)
    {
        ExamId = examId;
        Order = order;
        Statement = statement;
        Explanation = explanation;
    }

    public static Question Create(Guid examId, int order, string statement, string explanation)
    {
        if (string.IsNullOrWhiteSpace(statement))
            throw new DomainException("Enunciado da questão não pode ser vazio.");

        return new Question(examId, order, statement, explanation);
    }

    public void AddOption(string label, string text, bool isCorrect)
    {
        if (_options.Any(o => o.Label == label))
            throw new DomainException($"Já existe uma alternativa com o rótulo '{label}' nesta questão.");

        _options.Add(QuestionOption.Create(Id, label, text, isCorrect));
    }

    public void EnsureIsValid()
    {
        if (_options.Count < 2)
            throw new DomainException($"Questão {Order} precisa ter ao menos 2 alternativas.");

        if (_options.Count(o => o.IsCorrect) != 1)
            throw new DomainException($"Questão {Order} precisa ter exatamente 1 alternativa correta.");
    }
}
