using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PosFiap.Application.DTOs;
using PosFiap.Application.UseCases.StudySessions;

namespace PosFiap.API.Controllers;

[ApiController]
[Authorize]
[Route("api/study-sessions")]
public class StudySessionsController : ControllerBase
{
    private readonly UploadZipAndGenerateStudyMaterialUseCase _uploadUseCase;
    private readonly ListStudySessionsUseCase _listUseCase;
    private readonly GetStudySessionDetailUseCase _detailUseCase;

    public StudySessionsController(
        UploadZipAndGenerateStudyMaterialUseCase uploadUseCase,
        ListStudySessionsUseCase listUseCase,
        GetStudySessionDetailUseCase detailUseCase)
    {
        _uploadUseCase = uploadUseCase;
        _listUseCase = listUseCase;
        _detailUseCase = detailUseCase;
    }

    private Guid CurrentUserId =>
        Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub")!);

    /// <summary>
    /// Recebe o .zip com os PDFs das aulas (drag and drop no frontend), envia
    /// para a IA (Groq) gerar o resumo consolidado e a prova de 20 questões.
    /// </summary>
    [HttpPost("upload")]
    [RequestSizeLimit(100_000_000)]
    [ProducesResponseType(typeof(StudySessionDetailDto), StatusCodes.Status201Created)]
    public async Task<ActionResult<StudySessionDetailDto>> Upload(
        IFormFile file, CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new { message = "Nenhum arquivo foi enviado." });

        await using var stream = file.OpenReadStream();
        var result = await _uploadUseCase.ExecuteAsync(
            CurrentUserId, file.FileName, stream, file.Length, cancellationToken);

        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    /// <summary>Lista o histórico de envios (StudySessions) do usuário autenticado.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<StudySessionSummaryDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<StudySessionSummaryDto>>> GetAll(CancellationToken cancellationToken)
    {
        var result = await _listUseCase.ExecuteAsync(CurrentUserId, cancellationToken);
        return Ok(result);
    }

    /// <summary>Obtém o resumo e a prova gerados para uma StudySession específica.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(StudySessionDetailDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<StudySessionDetailDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await _detailUseCase.ExecuteAsync(CurrentUserId, id, cancellationToken);
        return Ok(result);
    }
}
