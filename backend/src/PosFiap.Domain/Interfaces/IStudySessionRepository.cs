using PosFiap.Domain.Entities;

namespace PosFiap.Domain.Interfaces;

/// <summary>
/// Porta de persistência para o agregado StudySession.
/// </summary>
public interface IStudySessionRepository
{
    Task<StudySession?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<StudySession?> GetByIdWithDetailsAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<StudySession>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
    Task AddAsync(StudySession studySession, CancellationToken cancellationToken = default);
    Task UpdateAsync(StudySession studySession, CancellationToken cancellationToken = default);
}
