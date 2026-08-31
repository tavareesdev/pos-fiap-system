using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PosFiap.Application.Interfaces;
using UglyToad.PdfPig;

namespace PosFiap.Infrastructure.Groq;

/// <summary>
/// Implementação de IStudyMaterialAiService que integra com a Groq
/// (endpoint /chat/completions, compatível com o formato OpenAI), usando
/// modelos abertos (Llama/GPT-OSS) com tier gratuito.
///
/// O tier gratuito da Groq tem um teto BAIXO de tokens por minuto (ex.:
/// 8.000 no gpt-oss-120b) — inclusive por requisição única. Mandar todos os
/// PDFs de uma vez (como fazíamos com o Gemini multimodal) estoura esse
/// limite. Por isso aqui usamos uma estratégia "map-reduce":
///
///   1. MAP: para cada PDF, extrai o texto (PdfPig) e pede à Groq um resumo
///      condensado (poucas centenas de tokens) daquele PDF isoladamente.
///   2. REDUCE: combina todos os resumos condensados (bem menores que os
///      textos originais) numa única chamada final que gera o resumo
///      consolidado + as 20 questões.
///
/// Cada chamada individual fica bem abaixo do limite de tokens por minuto.
/// </summary>
public class GroqService : IStudyMaterialAiService
{
    private const int MaxAttempts = 4;
    // Aumentado de 500 -> 900: com reasoning_effort "low" o modelo já gasta
    // bem menos tokens "pensando", mas ainda precisa de folga para o
    // raciocínio + o JSON de resposta em si.
    private const int MapStepMaxOutputTokens = 900;
    private const int ReduceStepMaxOutputTokens = 8192;
    private const int MaxReduceStepTokens = 16384;

    // Modelos de raciocínio da família gpt-oss suportam o parâmetro
    // reasoning_effort (low/medium/high). Usamos "low" por padrão para não
    // estourar o max_tokens só com raciocínio (ver comentário em
    // GroqChatRequest.ReasoningEffort).
    private const string DefaultReasoningEffort = "low";

    private readonly HttpClient _httpClient;
    private readonly GroqSettings _settings;
    private readonly ILogger<GroqService> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public GroqService(HttpClient httpClient, IOptions<GroqSettings> settings, ILogger<GroqService> logger)
    {
        _httpClient = httpClient;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task<GeneratedStudyMaterial> GenerateSummaryAndExamAsync(
        IReadOnlyList<LecturePdfContent> lectures,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        _logger.LogInformation("Iniciando processamento de {Count} PDFs", lectures.Count);

        var lectureNotes = new List<(string FileName, string Notes)>();

        for (int i = 0; i < lectures.Count; i++)
        {
            var lecture = lectures[i];
            var text = ExtractText(lecture);
            var notes = await SummarizeLectureAsync(lecture.FileName, text, cancellationToken);
            lectureNotes.Add((lecture.FileName, notes));
            _logger.LogInformation("PDF {Current}/{Total} processado: {FileName}", i + 1, lectures.Count, lecture.FileName);

            // Pequeno espaçamento entre chamadas para não rajar tokens contra
            // o limite por minuto (TPM) do tier gratuito da Groq — sem isso,
            // 6 PDFs processados quase instantaneamente somam mais tokens do
            // que a cota de 1 minuto permite, disparando 429 em sequência.
            if (i < lectures.Count - 1)
                await Task.Delay(TimeSpan.FromSeconds(1.5), cancellationToken);
        }

        _logger.LogInformation("Todos os PDFs processados. Iniciando consolidação...");

        try
        {
            var request = BuildFinalRequest(lectureNotes);
            var textResponse = await SendWithRetryAsync(request, cancellationToken);
            
            _logger.LogInformation("Consolidação concluída. Parsing do JSON...");

            var payload = JsonSerializer.Deserialize<GroqStudyMaterialPayload>(textResponse, JsonOptions)
                ?? throw new InvalidOperationException("Não foi possível interpretar o JSON retornado pela Groq.");

            var result = MapToDomainResult(payload);
            
            stopwatch.Stop();
            _logger.LogInformation("Processamento concluído em {Elapsed}ms", stopwatch.ElapsedMilliseconds);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao consolidar resumo. Tentando gerar com fallback...");
            
            // Fallback: gera um resumo simples a partir dos resumos individuais
            var fallbackResult = GenerateFallbackResult(lectureNotes);
            
            stopwatch.Stop();
            _logger.LogInformation("Fallback gerado em {Elapsed}ms", stopwatch.ElapsedMilliseconds);
            
            return fallbackResult;
        }
    }

    private GeneratedStudyMaterial GenerateFallbackResult(List<(string FileName, string Notes)> lectureNotes)
    {
        var combinedNotes = string.Join("\n\n", lectureNotes.Select(n => $"{n.FileName}:\n{n.Notes}"));
        
        var summary = $"# Resumo Consolidado (gerado em modo fallback)\n\n{combinedNotes}";

        // Gera questões genéricas baseadas nos resumos. IMPORTANTE: a regra
        // de negócio (Exam.EnsureIsValid) exige EXATAMENTE 20 questões — o
        // fallback tem que garantir esse total sempre, mesmo com poucos
        // PDFs/linhas de conteúdo disponíveis, senão o upload inteiro falha
        // com "A prova deve conter exatamente 20 questões, mas contém N".
        const int requiredQuestions = 20;
        var questions = new List<GeneratedQuestion>();

        // Junta todas as linhas de conteúdo utilizáveis de todas as aulas
        // (não só round-robin limitado a lectureNotes.Count * 2), para termos
        // material suficiente mesmo com poucas aulas.
        var pool = new List<(string FileName, string Line)>();
        foreach (var note in lectureNotes)
        {
            var lines = note.Notes.Split(new[] { '\n', '.' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(l => l.Trim())
                .Where(l => l.Length > 10)
                .ToList();

            foreach (var line in lines)
                pool.Add((note.FileName, line));
        }

        if (pool.Count == 0)
        {
            // Nenhum conteúdo utilizável extraído: cria um pool mínimo a
            // partir dos nomes dos arquivos para não deixar a lista vazia.
            foreach (var note in lectureNotes)
                pool.Add((note.FileName, $"Conteúdo da aula {note.FileName}"));
        }

        if (pool.Count == 0)
            pool.Add(("Material de estudo", "Conteúdo geral do material enviado"));

        for (int i = 0; i < requiredQuestions; i++)
        {
            var (fileName, line) = pool[i % pool.Count];
            var trimmedLine = line.Substring(0, Math.Min(50, line.Length));

            questions.Add(new GeneratedQuestion(
                $"Com base no conteúdo da aula '{fileName}', qual dos seguintes conceitos está relacionado ao tópico: '{trimmedLine}'? (questão {i + 1})",
                "Esta questão foi gerada automaticamente em modo de fallback, pois não foi possível obter uma resposta válida da IA no momento.",
                new List<GeneratedOption>
                {
                    new("A", "Conceito relacionado ao tópico", true),
                    new("B", "Conceito não relacionado", false),
                    new("C", "Outro conceito", false),
                    new("D", "Conceito irrelevante", false)
                }
            ));
        }

        return new GeneratedStudyMaterial(summary, questions);
    }

    private async Task<string> SummarizeLectureAsync(string fileName, string text, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Resumindo aula {FileName} na Groq ({Model})", fileName, _settings.Model);

        // Limita o texto para evitar tokens excessivos
        var maxTextLength = Math.Min(text.Length, 5000);
        var truncatedText = text.Length > maxTextLength ? text[..maxTextLength] + "..." : text;

        var request = new GroqChatRequest
        {
            Model = _settings.Model,
            Temperature = 0.2, // Reduzido
            MaxTokens = MapStepMaxOutputTokens,
            ResponseFormat = new GroqResponseFormat { Type = "json_object" },
            ReasoningEffort = IsReasoningModel() ? DefaultReasoningEffort : null,
            Messages = new List<GroqMessage>
            {
                new()
                {
                    Role = "system",
                    Content = """
                        Você é um assistente educacional. Responda SOMENTE com um JSON
                        válido no formato { "pontos_chave": "string" }, sem texto fora do
                        JSON. O campo "pontos_chave" deve conter, em português e em no
                        máximo 150 palavras, os principais conceitos, técnicas e conclusões
                        do material abaixo, em tópicos objetivos (use "- " para cada tópico).
                        Seja conciso e objetivo.
                        """
                },
                new()
                {
                    Role = "user",
                    Content = $"Material da aula \"{fileName}\":\n\n{truncatedText}"
                }
            }
        };

        var textResponse = await SendWithRetryAsync(request, cancellationToken);

        try
        {
            var payload = JsonSerializer.Deserialize<GroqLectureNotesPayload>(textResponse, JsonOptions);
            return payload?.PontosChave ?? textResponse;
        }
        catch (JsonException)
        {
            // Se falhar, tenta extrair manualmente
            var extractedJson = ExtractJsonFromText(textResponse);
            if (!string.IsNullOrEmpty(extractedJson))
            {
                try
                {
                    var payload = JsonSerializer.Deserialize<GroqLectureNotesPayload>(extractedJson, JsonOptions);
                    return payload?.PontosChave ?? textResponse;
                }
                catch { }
            }
            
            // Se por algum motivo não vier o JSON esperado, usa o texto bruto
            _logger.LogWarning("Não foi possível extrair JSON do resumo para {FileName}. Usando texto bruto.", fileName);
            return textResponse;
        }
    }

    private async Task<string> SendWithRetryAsync(GroqChatRequest request, CancellationToken cancellationToken)
    {
        var url = $"{_settings.BaseUrl}/chat/completions";
        var currentMaxTokens = request.MaxTokens ?? 0;
        var isReduceStep = currentMaxTokens >= ReduceStepMaxOutputTokens;
        var maxAttempts = isReduceStep ? MaxAttempts + 1 : MaxAttempts;

        // Só escalamos tokens/temperatura quando o motivo real da falha
        // anterior foi um problema de JSON/tokens insuficientes — NUNCA por
        // causa de rate limit (429), senão a próxima tentativa pede ainda
        // mais tokens do que a cota por minuto (TPM) tem disponível, o que
        // vira um 413 "Request too large" (foi exatamente o que aconteceu).
        var jsonIssueStrikes = 0;

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            if (isReduceStep && jsonIssueStrikes > 0)
            {
                if (jsonIssueStrikes == 1)
                {
                    currentMaxTokens = Math.Min(currentMaxTokens + 2048, MaxReduceStepTokens);
                    request.MaxTokens = currentMaxTokens;
                    _logger.LogWarning($"Tentativa {attempt}: Aumentando MaxTokens para {request.MaxTokens}");
                }
                else if (jsonIssueStrikes == 2)
                {
                    request.Temperature = 0.1;
                    request.MaxTokens = currentMaxTokens = Math.Min(currentMaxTokens + 2048, MaxReduceStepTokens);
                    _logger.LogWarning($"Tentativa {attempt}: Reduzindo temperatura para 0.1 e aumentando tokens para {request.MaxTokens}");
                }
                else if (jsonIssueStrikes >= 3)
                {
                    request.ResponseFormat = null;
                    request.Temperature = 0.1;
                    request.MaxTokens = currentMaxTokens = Math.Min(currentMaxTokens + 2048, MaxReduceStepTokens);
                    _logger.LogWarning($"Tentativa {attempt}: Removendo ResponseFormat e aumentando tokens");
                }
            }

            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = JsonContent.Create(request, options: JsonOptions)
            };
            httpRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _settings.ApiKey);

            using var httpResponse = await _httpClient.SendAsync(httpRequest, cancellationToken);

            if (httpResponse.IsSuccessStatusCode)
            {
                var groqResponse = await httpResponse.Content.ReadFromJsonAsync<GroqChatResponse>(JsonOptions, cancellationToken);
                var content = groqResponse?.Choices?.FirstOrDefault()?.Message?.Content
                    ?? throw new InvalidOperationException("A Groq não retornou conteúdo válido.");

                // Valida se o JSON é válido para o ReduceStep
                if (isReduceStep)
                {
                    try
                    {
                        JsonDocument.Parse(content);
                        return content;
                    }
                    catch (JsonException)
                    {
                        _logger.LogWarning($"JSON inválido na tentativa {attempt}. Conteúdo: {content[..Math.Min(200, content.Length)]}...");
                        
                        // Tenta extrair JSON do texto
                        var extractedJson = ExtractJsonFromText(content);
                        if (!string.IsNullOrEmpty(extractedJson))
                        {
                            try
                            {
                                JsonDocument.Parse(extractedJson);
                                _logger.LogInformation("JSON extraído com sucesso do texto.");
                                return extractedJson;
                            }
                            catch { }
                        }

                        // Se for a última tentativa, tenta uma última vez com fallback
                        if (attempt == maxAttempts)
                        {
                            // Tenta corrigir o JSON manualmente
                            var fixedJson = TryFixJson(content);
                            if (!string.IsNullOrEmpty(fixedJson))
                            {
                                try
                                {
                                    JsonDocument.Parse(fixedJson);
                                    _logger.LogInformation("JSON corrigido com sucesso.");
                                    return fixedJson;
                                }
                                catch { }
                            }
                            
                            throw new InvalidOperationException($"JSON inválido retornado pela Groq após {attempt} tentativas: {content[..Math.Min(500, content.Length)]}");
                        }

                        jsonIssueStrikes++;
                        continue; // Tenta novamente
                    }
                }

                return content;
            }

            var errorBody = await httpResponse.Content.ReadAsStringAsync(cancellationToken);

            // Rate limit (429): NÃO é um problema de JSON/tokens de saída —
            // é a cota de tokens por minuto (TPM) do tier gratuito da Groq.
            // A própria Groq nos diz quanto tempo esperar; respeitamos esse
            // tempo e reenviamos a MESMA requisição, sem mexer em MaxTokens
            // (aumentar tokens aqui só faria a próxima tentativa pedir mais
            // do que a cota restante permite, virando um 413).
            if (httpResponse.StatusCode == HttpStatusCode.TooManyRequests)
            {
                _logger.LogWarning("Groq: rate limit (tentativa {Attempt}/{Max}): {Body}", attempt, maxAttempts, errorBody);

                if (attempt == maxAttempts)
                {
                    throw new InvalidOperationException(
                        $"Falha ao chamar a API da Groq (status 429 - rate limit). Detalhes: {errorBody}");
                }

                var retryDelay = GetRateLimitRetryDelay(httpResponse, errorBody, attempt);
                _logger.LogInformation($"Aguardando {retryDelay.TotalSeconds:F1}s antes da próxima tentativa (rate limit)");
                await Task.Delay(retryDelay, cancellationToken);
                continue;
            }

            // Verifica erro específico de JSON/tokens de saída insuficientes
            if (errorBody.Contains("json_validate_failed") || errorBody.Contains("max completion tokens reached"))
            {
                _logger.LogWarning($"Erro de validação JSON/tokens na tentativa {attempt}. Detalhes: {errorBody}");

                if (attempt < maxAttempts)
                {
                    jsonIssueStrikes++;

                    // Na etapa de "map" (resumo por PDF) o teto é bem menor,
                    // já que a resposta esperada é curta; escalamos aqui
                    // também (não só no reduce step).
                    if (!isReduceStep)
                    {
                        var ceiling = MapStepMaxOutputTokens * 3;
                        var increasedTokens = Math.Min(currentMaxTokens + 400, ceiling);
                        if (increasedTokens > currentMaxTokens)
                        {
                            currentMaxTokens = increasedTokens;
                            request.MaxTokens = currentMaxTokens;
                            _logger.LogInformation($"Aumentando MaxTokens para {request.MaxTokens} na próxima tentativa");
                        }
                    }

                    continue;
                }
            }

            // 413/400 (payload grande demais) não é transitório: tentar de novo
            // com o mesmo conteúdo não vai ajudar. Só 503/502/504 valem retry.
            var isTransient = httpResponse.StatusCode is HttpStatusCode.ServiceUnavailable
                or HttpStatusCode.BadGateway 
                or HttpStatusCode.GatewayTimeout;

            _logger.LogError("Groq API retornou erro {Status} (tentativa {Attempt}/{Max}): {Body}",
                httpResponse.StatusCode, attempt, maxAttempts, errorBody);

            if (!isTransient || attempt == maxAttempts)
            {
                throw new InvalidOperationException(
                    $"Falha ao chamar a API da Groq (status {(int)httpResponse.StatusCode}). Detalhes: {errorBody}");
            }

            // backoff exponencial antes de tentar de novo (erro transitório: indisponibilidade)
            var delay = TimeSpan.FromSeconds(Math.Pow(2, attempt));
            _logger.LogDebug($"Aguardando {delay.TotalSeconds}s antes da próxima tentativa");
            await Task.Delay(delay, cancellationToken);
        }

        throw new InvalidOperationException("Falha ao chamar a API da Groq após múltiplas tentativas.");
    }

    /// <summary>
    /// Calcula quanto esperar antes de reenviar após um 429. A Groq manda o
    /// tempo sugerido tanto no header "Retry-After" quanto embutido na
    /// mensagem de erro (ex.: "Please try again in 2.0475s"); usamos o que
    /// estiver disponível, com uma margem de segurança, e caímos para
    /// backoff exponencial só se nada disso vier na resposta.
    /// </summary>
    private static TimeSpan GetRateLimitRetryDelay(HttpResponseMessage response, string errorBody, int attempt)
    {
        if (response.Headers.RetryAfter?.Delta is { } delta && delta > TimeSpan.Zero)
        {
            return delta + TimeSpan.FromMilliseconds(250);
        }

        var match = System.Text.RegularExpressions.Regex.Match(
            errorBody, @"try again in ([\d.]+)s", System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        if (match.Success &&
            double.TryParse(match.Groups[1].Value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var seconds))
        {
            return TimeSpan.FromSeconds(seconds) + TimeSpan.FromMilliseconds(250);
        }

        return TimeSpan.FromSeconds(Math.Pow(2, attempt));
    }

    private string ExtractText(LecturePdfContent lecture)
    {
        string text;
        try
        {
            using var document = PdfDocument.Open(lecture.PdfBytes);
            var sb = new StringBuilder();
            foreach (var page in document.GetPages())
                sb.AppendLine(page.Text);

            text = sb.ToString();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Não foi possível extrair texto do PDF {FileName}", lecture.FileName);
            text = string.Empty;
        }

        if (text.Length > _settings.MaxCharsPerLecture)
            text = text[.._settings.MaxCharsPerLecture];

        return text;
    }

    private GroqChatRequest BuildFinalRequest(List<(string FileName, string Notes)> lectureNotes)
    {
        var fileNames = string.Join(", ", lectureNotes.Select(l => l.FileName));

        var sb = new StringBuilder();
        sb.AppendLine($"Resumos de {lectureNotes.Count} aulas ({fileNames}):");
        sb.AppendLine();

        // Trunca cada resumo ainda mais para economizar tokens
        foreach (var (fileName, notes) in lectureNotes)
        {
            var truncatedNotes = notes.Length > 400 ? notes[..400] + "..." : notes;
            sb.AppendLine($"--- {fileName} ---");
            sb.AppendLine(truncatedNotes);
            sb.AppendLine();
        }

        return new GroqChatRequest
        {
            Model = _settings.Model,
            Temperature = 0.2, // Reduzido para respostas mais determinísticas
            MaxTokens = ReduceStepMaxOutputTokens,
            ResponseFormat = new GroqResponseFormat { Type = "json_object" },
            ReasoningEffort = IsReasoningModel() ? DefaultReasoningEffort : null,
            Messages = new List<GroqMessage>
            {
                new() { Role = "system", Content = BuildFinalSystemPrompt() },
                new() { Role = "user", Content = sb.ToString() }
            }
        };
    }

    // Só a família gpt-oss (20b/120b) da Groq aceita reasoning_effort; outros
    // modelos (ex.: llama-3.x) retornam erro se o campo for enviado.
    private bool IsReasoningModel() =>
        _settings.Model.Contains("gpt-oss", StringComparison.OrdinalIgnoreCase);

    private static string BuildFinalSystemPrompt() => """
        Você é um assistente educacional. Gere APENAS um JSON válido com esta estrutura EXATA:

        {
        "resumo": "texto do resumo em markdown",
        "questoes": [
            {
            "enunciado": "texto da questão",
            "explicacao": "explicação da resposta",
            "alternativas": [
                {"rotulo": "A", "texto": "alternativa A", "correta": false},
                {"rotulo": "B", "texto": "alternativa B", "correta": false},
                {"rotulo": "C", "texto": "alternativa C", "correta": false},
                {"rotulo": "D", "texto": "alternativa D", "correta": true}
            ]
            }
        ]
        }

        IMPORTANTE:
        - Retorne SOMENTE o JSON, sem texto antes ou depois
        - Use aspas duplas para todas as strings
        - Gere 20 questões no total
        - Cada questão deve ter 4 alternativas (A, B, C, D)
        - Apenas 1 alternativa por questão deve ter "correta": true
        - Mantenha o resumo conciso (máx 500 palavras)
        """;
        
    private static string? ExtractJsonFromText(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;

        // Tenta encontrar JSON no texto, mesmo que venha com markdown ou texto extra
        var startIndex = text.IndexOf('{');
        var endIndex = text.LastIndexOf('}');
        
        if (startIndex >= 0 && endIndex > startIndex)
        {
            return text.Substring(startIndex, endIndex - startIndex + 1);
        }
        
        return null;
    }

    private static GeneratedStudyMaterial MapToDomainResult(GroqStudyMaterialPayload payload)
    {
        var questoes = payload.Questoes;

        // A IA às vezes manda 1-2 questões a mais do que o pedido (ex.: 21
        // em vez de 20). Em vez de descartar a resposta inteira e cair no
        // fallback por causa disso, simplesmente cortamos para as 20
        // primeiras — a regra de negócio (Exam.EnsureIsValid) exige
        // EXATAMENTE 20, então só tratamos como erro de verdade quando vier
        // MENOS que 20 (aí sim não tem como "completar" com segurança).
        if (questoes.Count > 20)
            questoes = questoes.Take(20).ToList();

        if (questoes.Count != 20)
        {
            throw new InvalidOperationException($"Esperado 20 questões, recebido {questoes.Count}");
        }

        var questions = questoes.Select((q, index) => 
        {
            var alternativas = q.Alternativas;

            // Mesma tolerância: se vier alternativa a mais, corta para 4.
            if (alternativas.Count > 4)
                alternativas = alternativas.Take(4).ToList();

            if (alternativas.Count != 4)
            {
                throw new InvalidOperationException($"Questão {index + 1} tem {alternativas.Count} alternativas, esperado 4");
            }

            // Verifica se tem exatamente 1 alternativa correta
            var correctCount = alternativas.Count(a => a.Correta);
            if (correctCount != 1)
            {
                throw new InvalidOperationException($"Questão {index + 1} tem {correctCount} alternativas corretas, esperado 1");
            }

            return new GeneratedQuestion(
                q.Enunciado,
                q.Explicacao,
                alternativas.Select(a => new GeneratedOption(a.Rotulo, a.Texto, a.Correta)).ToList()
            );
        }).ToList();

        return new GeneratedStudyMaterial(payload.Resumo, questions);
    }

    private static string? TryFixJson(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;

        try
        {
            // Tenta encontrar o início e fim do JSON
            var startIndex = text.IndexOf('{');
            var endIndex = text.LastIndexOf('}');
            
            if (startIndex < 0 || endIndex < 0 || endIndex <= startIndex)
                return null;

            var json = text.Substring(startIndex, endIndex - startIndex + 1);

            // Tenta corrigir problemas comuns
            // 1. Substituir aspas simples por duplas
            json = json.Replace("'", "\"");
            
            // 2. Remover vírgulas extras antes de }
            json = System.Text.RegularExpressions.Regex.Replace(json, @",\s*}", "}");
            
            // 3. Remover vírgulas extras antes de ]
            json = System.Text.RegularExpressions.Regex.Replace(json, @",\s*]", "]");
            
            // 4. Garantir que strings estejam entre aspas duplas
            // (isso é mais complexo, mas podemos tentar)

            return json;
        }
        catch
        {
            return null;
        }
    }
}