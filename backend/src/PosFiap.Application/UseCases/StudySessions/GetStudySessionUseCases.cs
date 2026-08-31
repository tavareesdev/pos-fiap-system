using PosFiap.Application.DTOs;
using PosFiap.Domain.Exceptions;
using PosFiap.Domain.Interfaces;

namespace PosFiap.Application.UseCases.StudySessions;

/// <summary>
/// Caso de uso: lista as StudySessions (envios) de um usuário, mais recentes primeiro.
/// </summary>
public class ListStudySessionsUseCase
{
    private readonly IStudySessionRepository _repository;

    public ListStudySessionsUseCase(IStudySessionRepository repository) => _repository = repository;

    public async Task<IReadOnlyList<StudySessionSummaryDto>> ExecuteAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var sessions = await _repository.GetByUserIdAsync(userId, cancellationToken);

        return sessions
            .OrderByDescending(s => s.CreatedAtUtc)
            .Select(s => new StudySessionSummaryDto(
                s.Id,
                s.OriginalZipFileName,
                s.Status.ToString(),
                s.CreatedAtUtc,
                s.ProcessedAtUtc,
                s.ErrorMessage,
                s.Lectures.Count))
            .ToList();
    }
}

/// <summary>
/// Caso de uso: obtém o detalhe completo de uma StudySession (resumo + prova),
/// garantindo que pertence ao usuário autenticado.
/// </summary>
public class GetStudySessionDetailUseCase
{
    private readonly IStudySessionRepository _repository;

    public GetStudySessionDetailUseCase(IStudySessionRepository repository) => _repository = repository;

    public async Task<StudySessionDetailDto> ExecuteAsync(Guid userId, Guid studySessionId, CancellationToken cancellationToken = default)
    {
        var session = await _repository.GetByIdWithDetailsAsync(studySessionId, cancellationToken)
            ?? throw new DomainException("Sessão de estudo não encontrada.");

        if (session.UserId != userId)
            throw new DomainException("Você não tem permissão para acessar esta sessão de estudo.");

        return UploadZipAndGenerateStudyMaterialUseCase.MapToDetailDto(session);
    }
}
