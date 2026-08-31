using PosFiap.Domain.Common;
using PosFiap.Domain.Exceptions;

namespace PosFiap.Domain.Entities;

/// <summary>
/// Prova de múltipla escolha gerada pela IA a partir do conteúdo de todas as aulas
/// de uma StudySession. Regra de negócio: deve conter exatamente 20 questões.
/// </summary>
public class Exam : BaseEntity
{
    public const int RequiredQuestionCount = 20;

    private readonly List<Question> _questions = new();

    public Guid StudySessionId { get; private set; }
    public IReadOnlyCollection<Question> Questions => _questions.AsReadOnly();

    private Exam() { } // EF Core

    private Exam(Guid studySessionId)
    {
        StudySessionId = studySessionId;
    }

    public static Exam Create(Guid studySessionId) => new(studySessionId);

    public Question AddQuestion(string statement, string explanation)
    {
        if (_questions.Count >= RequiredQuestionCount)
            throw new DomainException($"A prova já possui o máximo de {RequiredQuestionCount} questões.");

        var question = Question.Create(Id, _questions.Count + 1, statement, explanation);
        _questions.Add(question);
        return question;
    }

    /// <summary>
    /// Valida a integridade da prova completa: quantidade de questões e alternativas.
    /// Deve ser chamado após a montagem via IA, antes da persistência.
    /// </summary>
    public void EnsureIsValid()
    {
        if (_questions.Count != RequiredQuestionCount)
            throw new DomainException(
                $"A prova deve conter exatamente {RequiredQuestionCount} questões, mas contém {_questions.Count}.");

        foreach (var question in _questions)
            question.EnsureIsValid();
    }
}
