using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PosFiap.Domain.Entities;

namespace PosFiap.Infrastructure.Persistence.Configurations;

public class StudySessionConfiguration : IEntityTypeConfiguration<StudySession>
{
    public void Configure(EntityTypeBuilder<StudySession> builder)
    {
        builder.ToTable("StudySessions");
        builder.HasKey(s => s.Id);

        builder.Property(s => s.OriginalZipFileName).IsRequired().HasMaxLength(500);
        builder.Property(s => s.Status).IsRequired().HasConversion<string>().HasMaxLength(50);
        builder.Property(s => s.ErrorMessage).HasMaxLength(4000);
        builder.Property(s => s.CreatedAtUtc).IsRequired();

        builder.HasIndex(s => s.UserId);

        // Navegação privada _lectures mapeada via campo de backing (EF Core Backing Fields)
        builder.Metadata.FindNavigation(nameof(StudySession.Lectures))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);

        builder.HasMany(s => s.Lectures)
            .WithOne()
            .HasForeignKey(l => l.StudySessionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(s => s.Summary)
            .WithOne()
            .HasForeignKey<Summary>(sm => sm.StudySessionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(s => s.Exam)
            .WithOne()
            .HasForeignKey<Exam>(e => e.StudySessionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class LectureConfiguration : IEntityTypeConfiguration<Lecture>
{
    public void Configure(EntityTypeBuilder<Lecture> builder)
    {
        builder.ToTable("Lectures");
        builder.HasKey(l => l.Id);
        builder.Property(l => l.FileName).IsRequired().HasMaxLength(500);
        builder.Property(l => l.FileSizeBytes).IsRequired();
        builder.Property(l => l.CreatedAtUtc).IsRequired();
    }
}

public class SummaryConfiguration : IEntityTypeConfiguration<Summary>
{
    public void Configure(EntityTypeBuilder<Summary> builder)
    {
        builder.ToTable("Summaries");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.ContentMarkdown).IsRequired().HasColumnType("text");
        builder.Property(s => s.CreatedAtUtc).IsRequired();
    }
}

public class ExamConfiguration : IEntityTypeConfiguration<Exam>
{
    public void Configure(EntityTypeBuilder<Exam> builder)
    {
        builder.ToTable("Exams");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.CreatedAtUtc).IsRequired();

        builder.Metadata.FindNavigation(nameof(Exam.Questions))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);

        builder.HasMany(e => e.Questions)
            .WithOne()
            .HasForeignKey(q => q.ExamId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class QuestionConfiguration : IEntityTypeConfiguration<Question>
{
    public void Configure(EntityTypeBuilder<Question> builder)
    {
        builder.ToTable("Questions");
        builder.HasKey(q => q.Id);
        builder.Property(q => q.Order).IsRequired();
        builder.Property(q => q.Statement).IsRequired().HasColumnType("text");
        builder.Property(q => q.Explanation).HasColumnType("text");
        builder.Property(q => q.CreatedAtUtc).IsRequired();

        builder.Metadata.FindNavigation(nameof(Question.Options))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);

        builder.HasMany(q => q.Options)
            .WithOne()
            .HasForeignKey(o => o.QuestionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class QuestionOptionConfiguration : IEntityTypeConfiguration<QuestionOption>
{
    public void Configure(EntityTypeBuilder<QuestionOption> builder)
    {
        builder.ToTable("QuestionOptions");
        builder.HasKey(o => o.Id);
        builder.Property(o => o.Label).IsRequired().HasMaxLength(5);
        builder.Property(o => o.Text).IsRequired().HasColumnType("text");
        builder.Property(o => o.IsCorrect).IsRequired();
        builder.Property(o => o.CreatedAtUtc).IsRequired();
    }
}
