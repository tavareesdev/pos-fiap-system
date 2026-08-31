using System.Text.Json.Serialization;

namespace PosFiap.Infrastructure.Groq;

// Modelos internos que espelham o contrato REST compatível com OpenAI que a
// Groq expõe (/openai/v1/chat/completions). Não vazam para fora da camada de
// Infraestrutura.

internal class GroqChatRequest
{
    [JsonPropertyName("model")]
    public string Model { get; set; } = null!;

    [JsonPropertyName("messages")]
    public List<GroqMessage> Messages { get; set; } = new();

    [JsonPropertyName("temperature")]
    public double Temperature { get; set; } = 0.4;

    [JsonPropertyName("response_format")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public GroqResponseFormat? ResponseFormat { get; set; }

    // Limita o tamanho da resposta. Importante no tier gratuito da Groq, que
    // tem um teto de tokens por minuto (ex.: 8.000 no gpt-oss-120b) contando
    // input + output de CADA requisição — sem isso, uma resposta grande pode
    // sozinha estourar o limite.
    [JsonPropertyName("max_tokens")]
    public int? MaxTokens { get; set; }

    // Modelos de raciocínio (openai/gpt-oss-20b e openai/gpt-oss-120b) gastam
    // parte do max_tokens "pensando" antes de gerar a resposta final. Com
    // reasoning_effort no padrão ("medium"), esse raciocínio pode consumir
    // TODO o orçamento de tokens, deixando a resposta final vazia — o que faz
    // a Groq devolver 400 "json_validate_failed" com failed_generation vazio,
    // mesmo com response_format=json_object. Usar "low" reduz drasticamente
    // os tokens gastos em raciocínio, sobrando espaço para o JSON de fato.
    [JsonPropertyName("reasoning_effort")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ReasoningEffort { get; set; }
}

internal class GroqMessage
{
    [JsonPropertyName("role")]
    public string Role { get; set; } = "user";

    [JsonPropertyName("content")]
    public string Content { get; set; } = null!;
}

internal class GroqResponseFormat
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "json_object";
}

internal class GroqChatResponse
{
    [JsonPropertyName("choices")]
    public List<GroqChoice>? Choices { get; set; }
}

internal class GroqChoice
{
    [JsonPropertyName("message")]
    public GroqMessage? Message { get; set; }

    [JsonPropertyName("finish_reason")]
    public string? FinishReason { get; set; }
}

// Modelos usados apenas para desserializar o JSON estruturado que pedimos ao
// modelo via prompt (Groq não tem um "responseSchema" nativo como o Gemini;
// usamos response_format=json_object + instruções explícitas no prompt).

internal class GroqStudyMaterialPayload
{
    [JsonPropertyName("resumo")]
    public string Resumo { get; set; } = null!;

    [JsonPropertyName("questoes")]
    public List<GroqQuestionPayload> Questoes { get; set; } = new();
}

internal class GroqQuestionPayload
{
    [JsonPropertyName("enunciado")]
    public string Enunciado { get; set; } = null!;

    [JsonPropertyName("explicacao")]
    public string Explicacao { get; set; } = string.Empty;

    [JsonPropertyName("alternativas")]
    public List<GroqOptionPayload> Alternativas { get; set; } = new();
}

internal class GroqOptionPayload
{
    [JsonPropertyName("rotulo")]
    public string Rotulo { get; set; } = null!;

    [JsonPropertyName("texto")]
    public string Texto { get; set; } = null!;

    [JsonPropertyName("correta")]
    public bool Correta { get; set; }
}

// Payload retornado pela etapa de "map": resumo condensado de UM PDF, usado
// depois como entrada (bem menor) da etapa final de "reduce".
internal class GroqLectureNotesPayload
{
    [JsonPropertyName("pontos_chave")]
    public string PontosChave { get; set; } = null!;
}