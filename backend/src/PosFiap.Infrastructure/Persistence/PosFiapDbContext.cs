using Microsoft.EntityFrameworkCore;
using PosFiap.Domain.Entities;
using PosFiap.Domain.Interfaces;

namespace PosFiap.Infrastructure.Persistence;

/// <summary>
/// DbContext do EF Core. Implementa IUnitOfWork para persistir todas as
/// alterações feitas via repositórios de forma atômica.
/// </summary>
public class PosFiapDbContext : DbContext, IUnitOfWork
{
    public PosFiapDbContext(DbContextOptions<PosFiapDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<StudySession> StudySessions => Set<StudySession>();
    public DbSet<Lecture> Lectures => Set<Lecture>();
    public DbSet<Summary> Summaries => Set<Summary>();
    public DbSet<Exam> Exams => Set<Exam>();
    public DbSet<Question> Questions => Set<Question>();
    public DbSet<QuestionOption> QuestionOptions => Set<QuestionOption>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(PosFiapDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
