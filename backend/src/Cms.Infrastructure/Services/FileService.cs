using Cms.Application.Common;
using Cms.Application.Contracts;
using Cms.Application.DTOs.Files;
using Cms.Application.Interfaces;

namespace Cms.Infrastructure.Services;

/// <summary>
/// Stores standalone evidence uploads and records an audit event for every
/// accepted file. Validation and byte-level checks live in <see cref="IFileStore"/>.
/// </summary>
public sealed class FileService : IFileService
{
    private readonly IFileStore _fileStore;
    private readonly IAuditLogger _auditLogger;

    public FileService(IFileStore fileStore, IAuditLogger auditLogger)
    {
        _fileStore = fileStore;
        _auditLogger = auditLogger;
    }

    public async Task<UploadedFileDto> UploadAsync(
        Stream content,
        string fileName,
        string? contentType,
        CancellationToken cancellationToken = default)
    {
        var stored = await _fileStore.SaveAsync(
            content,
            fileName,
            contentType,
            cancellationToken);

        await _auditLogger.LogAsync(
            AuditActions.FileUploaded,
            "common.File",
            stored.Key,
            new
            {
                stored.Key,
                stored.ContentType,
                stored.SizeBytes
            },
            cancellationToken);

        return new UploadedFileDto
        {
            Key = stored.Key,
            FileName = stored.FileName,
            ContentType = stored.ContentType,
            SizeBytes = stored.SizeBytes
        };
    }
}
