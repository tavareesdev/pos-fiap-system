namespace PosFiap.Application.Interfaces;

public record ExtractedFile(string FileName, byte[] Content);

/// <summary>
/// Abstrai a extração de arquivos PDF de dentro de um .zip.
/// </summary>
public interface IZipExtractor
{
    IReadOnlyList<ExtractedFile> ExtractPdfFiles(Stream zipStream);
}
