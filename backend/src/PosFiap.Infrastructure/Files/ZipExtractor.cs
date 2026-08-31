using System.IO.Compression;
using PosFiap.Application.Interfaces;

namespace PosFiap.Infrastructure.Files;

/// <summary>
/// Implementação de IZipExtractor usando System.IO.Compression.
/// Extrai apenas os arquivos com extensão .pdf, ignorando pastas e outros tipos.
/// </summary>
public class ZipExtractor : IZipExtractor
{
    public IReadOnlyList<ExtractedFile> ExtractPdfFiles(Stream zipStream)
    {
        var result = new List<ExtractedFile>();

        using var archive = new ZipArchive(zipStream, ZipArchiveMode.Read, leaveOpen: true);

        foreach (var entry in archive.Entries)
        {
            if (string.IsNullOrEmpty(entry.Name)) continue; // diretório
            if (!entry.Name.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase)) continue;

            using var entryStream = entry.Open();
            using var memoryStream = new MemoryStream();
            entryStream.CopyTo(memoryStream);

            result.Add(new ExtractedFile(entry.Name, memoryStream.ToArray()));
        }

        return result;
    }
}
