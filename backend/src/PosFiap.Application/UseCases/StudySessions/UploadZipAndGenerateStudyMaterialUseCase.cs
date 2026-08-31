using PosFiap.Application.DTOs;
using PosFiap.Application.Interfaces;
using PosFiap.Domain.Entities;
using PosFiap.Domain.Exceptions;
using PosFiap.Domain.Interfaces;

namespace PosFiap.Application.UseCases.StudySessions;

/// <summary>
/// Caso de uso principal do sistema: recebe o .zip com PDFs de aulas, extrai os
/// arquivos, envia para a IA (Groq) gerar o resumo consolidado e a prova de
/// 20 questões, e persiste o resultado como uma StudySession completa.
/// </summary>
public class UploadZipAndGenerateStudyMaterialUseCase
{
    private const long MaxZipSizeBytes = 100 * 1024 * 1024; // 100 MB

    private readonly IZipExtractor _zipExtractor;
    private readonly IStudyMaterialAiService _aiService;
    private readonly IStudySessionRepository _studySessionRepository;
    private readonly IUnitOfWork _unitOfWork;

    public UploadZipAndGenerateStudyMaterialUseCase(
        IZipExtractor zipExtractor,
        IStudyMaterialAiService aiService,
        IStudySessionRepository studySessionRepository,
        IUnitOfWork unitOfWork)
    {
        _zipExtractor = zipExtractor;
        _aiService = aiService;
        _studySessionRepository = studySessionRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<StudySessionDetailDto> ExecuteAsync(
        Guid userId,
        string zipFileName,
        Stream zipStream,
        long zipSizeBytes,
        CancellationToken cancellationToken = default)
    {
        if (zipSizeBytes > MaxZipSizeBytes)
            throw new DomainException("O arquivo .zip excede o tamanho máximo permitido (100MB).");

        // 1. Cria o agregado StudySession (regras de domínio validam nome/extensão)
        var studySession = Domain.Entities.StudySession.Create(userId, zipFileName);

        // 2. Extrai os PDFs do zip
        var extractedFiles = _zipExtractor.ExtractPdfFiles(zipStream);

        if (extractedFiles.Count == 0)
            throw new DomainException("Nenhum arquivo PDF foi encontrado dentro do .zip enviado.");

        foreach (var file in extractedFiles)
            studySession.RegisterExtractedLecture(file.FileName, file.Content.LongLength);

        studySession.StartAiProcessing();
        await _studySessionRepository.AddAsync(studySession, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        try
        {
            // 3. Envia os PDFs (texto extraído) para a IA (Groq) gerar resumo + prova
            var lecturePdfs = extractedFiles
                .Select(f => new LecturePdfContent(f.FileName, f.Content))
                .ToList();

            var generated = await _aiService.GenerateSummaryAndExamAsync(lecturePdfs, cancellationToken);

            // 4. Monta as entidades de domínio Summary e Exam a partir do retorno da IA
            var summary = Summary.Create(studySession.Id, generated.SummaryMarkdown);
            var exam = Exam.Create(studySession.Id);

            foreach (var q in generated.Questions)
            {
                var question = exam.AddQuestion(q.Statement, q.Explanation);
                foreach (var option in q.Options)
                    question.AddOption(option.Label, option.Text, option.IsCorrect);
            }

            // 5. Valida e conclui (regra de negócio: exige exatamente 20 questões válidas)
            studySession.CompleteWithResults(summary, exam);

            await _studySessionRepository.UpdateAsync(studySession, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            studySession.MarkAsFailed(ex.Message);
            await _studySessionRepository.UpdateAsync(studySession, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            throw;
        }

        return MapToDetailDto(studySession);
    }

    internal static StudySessionDetailDto MapToDetailDto(Domain.Entities.StudySession s) => new(
        s.Id,
        s.OriginalZipFileName,
        s.Status.ToString(),
        s.CreatedAtUtc,
        s.ProcessedAtUtc,
        s.ErrorMessage,
        s.Lectures.Select(l => new LectureDto(l.Id, l.FileName, l.FileSizeBytes)).ToList(),
        s.Summary?.ContentMarkdown,
        s.Exam?.Questions
            .OrderBy(q => q.Order)
            .Select(q => new QuestionDto(
                q.Order,
                q.Statement,
                q.Explanation,
                q.Options.Select(o => new QuestionOptionDto(o.Label, o.Text, o.IsCorrect)).ToList()))
            .ToList());
}
