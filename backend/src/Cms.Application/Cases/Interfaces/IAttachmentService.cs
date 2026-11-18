using Cms.Application.Cases.DTOs;

namespace Cms.Application.Cases.Interfaces;

public interface IAttachmentService
{
    Task<Guid> UploadAsync(
        Guid requestId,
        UploadedFile file,
        CancellationToken cancellationToken = default);
    Task<AttachmentDownload> GetForDownloadAsync(
        Guid requestId,
        Guid attachmentId,
        CancellationToken cancellationToken = default);
}
