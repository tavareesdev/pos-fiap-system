namespace PosFiap.Application.Interfaces;

public record LecturePdfContent(string FileName, byte[] PdfBytes);

public record GeneratedOption(string Label, string Text, bool IsCorrect);

public record GeneratedQuestion(string Statement, string Explanation, IReadOnlyList<GeneratedOption> Options);

public record GeneratedStudyMaterial(string SummaryMarkdown, IReadOnlyList<GeneratedQuestion> Questions);

/// <summary>
/// Abstrai a comunicação com o provedor de IA responsável por gerar o resumo
/// consolidado e as 20 questões de múltipla escolha a partir dos PDFs das aulas.
/// Implementação atual: Groq (modelos abertos como Llama, hospedados pela Groq,
/// com tier gratuito). O texto dos PDFs é extraído localmente (PdfPig) antes de
/// ser enviado, já que os modelos usados via Groq não recebem PDF nativamente
/// como o Gemini multimodal recebia.
/// </summary>
public interface IStudyMaterialAiService
{
    Task<GeneratedStudyMaterial> GenerateSummaryAndExamAsync(
        IReadOnlyList<LecturePdfContent> lectures,
        CancellationToken cancellationToken = default);
}
