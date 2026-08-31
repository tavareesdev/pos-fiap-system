namespace PosFiap.Application.DTOs;

public record LectureDto(Guid Id, string FileName, long FileSizeBytes);

public record QuestionOptionDto(string Label, string Text, bool IsCorrect);

public record QuestionDto(int Order, string Statement, string Explanation, IReadOnlyList<QuestionOptionDto> Options);

public record StudySessionSummaryDto(
    Guid Id,
    string OriginalZipFileName,
    string Status,
    DateTime CreatedAtUtc,
    DateTime? ProcessedAtUtc,
    string? ErrorMessage,
    int LectureCount);

public record StudySessionDetailDto(
    Guid Id,
    string OriginalZipFileName,
    string Status,
    DateTime CreatedAtUtc,
    DateTime? ProcessedAtUtc,
    string? ErrorMessage,
    IReadOnlyList<LectureDto> Lectures,
    string? SummaryMarkdown,
    IReadOnlyList<QuestionDto>? Questions);
