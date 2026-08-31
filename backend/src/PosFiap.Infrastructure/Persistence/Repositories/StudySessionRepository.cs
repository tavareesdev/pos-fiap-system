using Microsoft.EntityFrameworkCore;
using PosFiap.Domain.Entities;
using PosFiap.Domain.Interfaces;

namespace PosFiap.Infrastructure.Persistence.Repositories;

public class StudySessionRepository : IStudySessionRepository
{
    private readonly PosFiapDbContext _context;

    public StudySessionRepository(PosFiapDbContext context) => _context = context;

    public Task<StudySession?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        _context.StudySessions.FirstOrDefaultAsync(s => s.Id == id, cancellationToken);

    public Task<StudySession?> GetByIdWithDetailsAsync(Guid id, CancellationToken cancellationToken = default) =>
        _context.StudySessions
            .Include(s => s.Lectures)
            .Include(s => s.Summary)
            .Include(s => s.Exam).ThenInclude(e => e!.Questions).ThenInclude(q => q.Options)
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken);

    public async Task<IReadOnlyList<StudySession>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default) =>
        await _context.StudySessions
            .Include(s => s.Lectures)
            .Where(s => s.UserId == userId)
            .ToListAsync(cancellationToken);

    public async Task AddAsync(StudySession studySession, CancellationToken cancellationToken = default) =>
        await _context.StudySessions.AddAsync(studySession, cancellationToken);

    /// <summary>
    /// IMPORTANTE: neste sistema o mesmo StudySession normalmente já está
    /// sendo rastreado pelo DbContext (ele foi criado via AddAsync +
    /// SaveChanges no início do fluxo, dentro do MESMO escopo/requisição, e
    /// depois mutado com Summary/Exam/Questions e passado de novo pra cá).
    ///
    /// Chamar `_context.StudySessions.Update(studySession)` nesse cenário
    /// marca o GRAFO INTEIRO como Modified — inclusive Exam/Questions/
    /// QuestionOptions/Summary que acabaram de ser criados em memória e
    /// ainda NÃO existem no banco. Isso faz o EF tentar um UPDATE em linhas
    /// inexistentes, e o Npgsql provider reporta "0 rows affected" como
    /// DbUpdateConcurrencyException.
    ///
    /// A correção óbvia seria só marcar como Added as entidades que ainda
    /// estão "Detached" no ChangeTracker — só que isso NÃO FUNCIONA aqui:
    /// assim que acessamos `_context.Entry(studySession)` (a raiz, já
    /// rastreada), o EF roda automaticamente um DetectChanges() que percorre
    /// o grafo de navegação inteiro. Nesse processo, como Summary/Exam/
    /// Question/QuestionOption usam chave Guid GERADA NO CLIENTE (não pelo
    /// banco), o EF assume — incorretamente — que qualquer entidade nova
    /// alcançável com uma PK já preenchida é uma linha JÁ EXISTENTE no banco,
    /// e marca ela como "Modified"/"Unchanged" em vez de "Added". Isso
    /// acontece ANTES de qualquer verificação nossa, então checar
    /// "State == Detached" nunca é verdadeiro para essas entidades — é um
    /// comportamento conhecido do Change Tracker do EF Core com chaves
    /// client-generated.
    ///
    /// Como este repositório só é usado neste único fluxo (upload → gera
    /// resultado da IA → completa a StudySession), sabemos com certeza que,
    /// sempre que Summary/Exam estão presentes aqui, são objetos RECÉM-
    /// CRIADOS nesta mesma requisição — nunca uma edição de algo já
    /// persistido (não existe nenhum método de domínio para "editar" um
    /// Exam existente). Por isso, é seguro forçar o estado para Added
    /// diretamente, sobrescrevendo o que o EF adivinhou errado.
    /// </summary>
    public Task UpdateAsync(StudySession studySession, CancellationToken cancellationToken = default)
    {
        var rootEntry = _context.Entry(studySession);
        if (rootEntry.State == EntityState.Detached)
        {
            // Só acontece se vier de um DbContext diferente (ex.: testes).
            // Nesse caso, Update() no grafo inteiro é o comportamento certo.
            _context.StudySessions.Update(studySession);
            return Task.CompletedTask;
        }

        if (studySession.Summary is not null)
            MarkAsNew(studySession.Summary);

        if (studySession.Exam is not null)
        {
            MarkAsNew(studySession.Exam);

            foreach (var question in studySession.Exam.Questions)
            {
                MarkAsNew(question);
                foreach (var option in question.Options)
                    MarkAsNew(option);
            }
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Força o estado da entidade para Added, independente do que o EF
    /// tenha inferido automaticamente (ver comentário acima em UpdateAsync).
    /// </summary>
    private void MarkAsNew(object entity)
    {
        var entry = _context.Entry(entity);
        if (entry.State != EntityState.Added)
            entry.State = EntityState.Added;
    }
}
