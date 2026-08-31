namespace PosFiap.Domain.Enums;

/// <summary>
/// Representa o estágio do pipeline de processamento de um envio de material de aula.
/// </summary>
public enum ProcessingStatus
{
    Recebido = 0,
    ExtraindoArquivos = 1,
    ProcessandoComIA = 2,
    Concluido = 3,
    Falhou = 4
}
