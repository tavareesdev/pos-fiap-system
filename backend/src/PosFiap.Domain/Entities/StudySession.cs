using PosFiap.Domain.Common;
using PosFiap.Domain.Enums;
using PosFiap.Domain.Exceptions;

namespace PosFiap.Domain.Entities;

/// <summary>
/// Aggregate Root central do domínio: representa o envio de um .zip com PDFs de aulas
/// de uma pós-graduação. Orquestra o ciclo de vida do processamento: recebimento,
/// extração dos PDFs, geração de resumo e prova via IA.
/// Todas as modificações em Lectures/Summary/Exam devem passar por aqui.
/// </summary>
public class StudySession : BaseEntity, IAggregateRoot
{
    private readonly List<Lecture> _lectures = new();

    public Guid UserId { get; private set; }
    public string OriginalZipFileName { get; private set; } = null!;
    public ProcessingStatus Status { get; private set; }
    public string? ErrorMessage { get; private set; }
    public DateTime? ProcessedAtUtc { get; private set; }

    public IReadOnlyCollection<Lecture> Lectures => _lectures.AsReadOnly();
    public Summary? Summary { get; private set; }
    public Exam? Exam { get; private set; }

    private StudySession() { } // EF Core

    private StudySession(Guid userId, string originalZipFileName)
    {
        UserId = userId;
        OriginalZipFileName = originalZipFileName;
        Status = ProcessingStatus.Recebido;
    }

    public static StudySession Create(Guid userId, string originalZipFileName)
    {
        if (userId == Guid.Empty)
            throw new DomainException("Usuário inválido.");

        if (string.IsNullOrWhiteSpace(originalZipFileName))
            throw new DomainException("Nome do arquivo .zip é obrigatório.");

        if (!originalZipFileName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            throw new DomainException("O arquivo enviado precisa ser um .zip.");

        return new StudySession(userId, originalZipFileName);
    }

    public void RegisterExtractedLecture(string fileName, long fileSizeBytes)
    {
        if (Status != ProcessingStatus.Recebido && Status != ProcessingStatus.ExtraindoArquivos)
            throw new DomainException("Não é possível adicionar aulas fora da etapa de extração.");

        Status = ProcessingStatus.ExtraindoArquivos;
        _lectures.Add(Lecture.Create(Id, fileName, fileSizeBytes));
    }

    public void StartAiProcessing()
    {
        if (_lectures.Count == 0)
            throw new DomainException("Nenhuma aula (PDF) foi encontrada dentro do .zip.");

        Status = ProcessingStatus.ProcessandoComIA;
    }

    public void CompleteWithResults(Summary summary, Exam exam)
    {
        if (Status != ProcessingStatus.ProcessandoComIA)
            throw new DomainException("A sessão não está na etapa de processamento por IA.");

        exam.EnsureIsValid();

        Summary = summary;
        Exam = exam;
        Status = ProcessingStatus.Concluido;
        ProcessedAtUtc = DateTime.UtcNow;
    }

    public void MarkAsFailed(string errorMessage)
    {
        Status = ProcessingStatus.Falhou;
        ErrorMessage = errorMessage;
        ProcessedAtUtc = DateTime.UtcNow;
    }
}
