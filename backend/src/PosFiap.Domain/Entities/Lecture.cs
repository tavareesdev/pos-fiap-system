using PosFiap.Domain.Common;
using PosFiap.Domain.Exceptions;

namespace PosFiap.Domain.Entities;

/// <summary>
/// Representa um PDF de aula extraído do arquivo .zip enviado pelo usuário.
/// Entidade filha do agregado StudySession — não deve ser manipulada fora dele.
/// </summary>
public class Lecture : BaseEntity
{
    public Guid StudySessionId { get; private set; }
    public string FileName { get; private set; } = null!;
    public long FileSizeBytes { get; private set; }

    private Lecture() { } // EF Core

    private Lecture(Guid studySessionId, string fileName, long fileSizeBytes)
    {
        StudySessionId = studySessionId;
        FileName = fileName;
        FileSizeBytes = fileSizeBytes;
    }

    public static Lecture Create(Guid studySessionId, string fileName, long fileSizeBytes)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            throw new DomainException("Nome do arquivo de aula é obrigatório.");

        if (!fileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
            throw new DomainException($"Arquivo '{fileName}' não é um PDF.");

        return new Lecture(studySessionId, fileName, fileSizeBytes);
    }
}
