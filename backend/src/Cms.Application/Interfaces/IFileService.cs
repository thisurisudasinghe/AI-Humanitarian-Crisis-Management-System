using Cms.Application.DTOs.Files;

namespace Cms.Application.Interfaces;

/// <summary>
/// Upload use case for standalone evidence. Persists the file through
/// <see cref="Contracts.IFileStore"/> and records an audit event.
/// </summary>
public interface IFileService
{
    Task<UploadedFileDto> UploadAsync(
        Stream content,
        string fileName,
        string? contentType,
        CancellationToken cancellationToken = default);
}
