namespace PosFiap.Infrastructure.Groq;

public class GroqSettings
{
    public string ApiKey { get; set; } = null!;

    // openai/gpt-oss-120b: bom equilíbrio entre qualidade e velocidade,
    // com contexto grande o suficiente para várias aulas em PDF de uma vez.
    public string Model { get; set; } = "openai/gpt-oss-120b";

    public string BaseUrl { get; set; } = "https://api.groq.com/openai/v1";

    public int TimeoutSeconds { get; set; } = 300;

    // Tamanho máximo (em caracteres) de texto extraído por PDF que é enviado
    // ao modelo. Evita estourar a janela de contexto quando um PDF é enorme.
    public int MaxCharsPerLecture { get; set; } = 10_000;
}
