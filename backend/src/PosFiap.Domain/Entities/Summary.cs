using PosFiap.Domain.Common;
using PosFiap.Domain.Exceptions;

namespace PosFiap.Domain.Entities;

/// <summary>
/// Resumo consolidado, em Markdown, gerado pela IA a partir de todas as aulas
/// de uma StudySession.
/// </summary>
public class Summary : BaseEntity
{
    public Guid StudySessionId { get; private set; }
    public string ContentMarkdown { get; private set; } = null!;

    private Summary() { } // EF Core

    private Summary(Guid studySessionId, string contentMarkdown)
    {
        StudySessionId = studySessionId;
        ContentMarkdown = contentMarkdown;
    }

    public static Summary Create(Guid studySessionId, string contentMarkdown)
    {
        if (string.IsNullOrWhiteSpace(contentMarkdown))
            throw new DomainException("Conteúdo do resumo não pode ser vazio.");

        return new Summary(studySessionId, contentMarkdown);
    }
}
