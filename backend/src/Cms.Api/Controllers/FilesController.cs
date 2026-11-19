using Cms.Application.Common;
using Cms.Application.DTOs.Files;
using Cms.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Cms.Api.Controllers;

/// <summary>
/// Standalone evidence uploads (FR-CIT-03). Any authenticated user may upload a
/// PNG, JPEG, WebP or PDF of at most 1 MB. The response key is opaque and must
/// be persisted by the owning module; reading a file back goes through
/// <c>IFileStore.OpenReadAsync</c> in an endpoint that authorises the caller
/// against the owning record.
/// </summary>
[ApiController]
[Route("api/v1/files")]
[Authorize]
[Produces("application/json")]
[EnableRateLimiting("uploads")]
public sealed class FilesController : ControllerBase
{
    private readonly IFileService _fileService;

    public FilesController(IFileService fileService)
    {
        _fileService = fileService;
    }

    /// <summary>Uploads one evidence file and returns its storage key.</summary>
    [HttpPost]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(UploadedFileDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status413PayloadTooLarge)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<UploadedFileDto>> Upload(
        [FromForm] IFormFile? file,
        CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
        {
            throw new ValidationException(
                "A file is required.",
                new Dictionary<string, string[]>
                {
                    ["file"] = ["Select a non-empty file to upload."]
                },
                "FILE_REQUIRED");
        }

        await using var stream = file.OpenReadStream();

        var uploaded = await _fileService.UploadAsync(
            stream,
            file.FileName,
            file.ContentType,
            cancellationToken);

        return StatusCode(StatusCodes.Status201Created, uploaded);
    }
}
