using Cms.Application.Cases.DTOs;
using Cms.Application.Cases.Interfaces;
using Cms.Application.Interfaces;
using Cms.Domain.Cases;
using Cms.Domain.Entities.Cases;
using Cms.Domain.Enums.Cases;
using Cms.Domain.Exceptions;
using Cms.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Cms.Infrastructure.Cases;

public sealed class AttachmentService : IAttachmentService
{
    private const long MaxBytes = 5L * 1024 * 1024;
    private static readonly HashSet<string> StaffRoles =
        CasesRoles.Staff.Split(',', StringSplitOptions.RemoveEmptyEntries).ToHashSet();

    private readonly ApplicationDbContext _context;
    private readonly IFileStore _fileStore;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeService _dateTimeService;
    private readonly CaseWriteHelper _writer;

    public AttachmentService(
        ApplicationDbContext context,
        IFileStore fileStore,
        ICurrentUserService currentUser,
        IDateTimeService dateTimeService,
        CaseWriteHelper writer)
    {
        _context = context;
        _fileStore = fileStore;
        _currentUser = currentUser;
        _dateTimeService = dateTimeService;
        _writer = writer;
    }

    public async Task<Guid> UploadAsync(
        Guid requestId,
        UploadedFile file,
        CancellationToken cancellationToken = default)
    {
        var request = await GetVisibleAsync(requestId, cancellationToken);
        var userId = RequireUserId();
        if (!IsStaff() && request.RequesterUserId != userId)
        {
            throw new NotFoundException("REQUEST_NOT_FOUND", $"Request {requestId} was not found.");
        }

        using var buffer = new MemoryStream();
        await file.Content.CopyToAsync(buffer, cancellationToken);
        if (buffer.Length == 0 || buffer.Length > MaxBytes)
        {
            throw new ValidationException(
                "INVALID_ATTACHMENT_SIZE",
                new Dictionary<string, string[]> { ["file"] = new[] { "File must be between 1 byte and 5 MB." } });
        }

        var bytes = buffer.ToArray();
        var detected = Detect(bytes);
        if (detected is null)
        {
            throw new ValidationException(
                "INVALID_ATTACHMENT_TYPE",
                new Dictionary<string, string[]> { ["file"] = new[] { "Only jpg, jpeg, png and pdf files are accepted." } });
        }

        var detectedType = detected.Value;
        var storedFileName = $"{Guid.NewGuid():N}{detectedType.Extension}";
        buffer.Position = 0;
        await _fileStore.SaveAsync(buffer, storedFileName, cancellationToken);

        var entity = new Attachment
        {
            Id = Guid.NewGuid(),
            RequestId = requestId,
            StoredFileName = storedFileName,
            OriginalFileName = SanitizeOriginalName(file.FileName),
            ContentType = detectedType.ContentType,
            SizeBytes = bytes.Length,
            UploadedByUserId = userId,
            CreatedAt = _dateTimeService.UtcNow
        };
        await _context.Set<Attachment>().AddAsync(entity, cancellationToken);
        await _writer.AddAuditAsync(
            "Attachment.Uploaded",
            "Attachment",
            entity.Id,
            new { requestId, contentType = detectedType.ContentType, sizeBytes = bytes.Length },
            userId,
            cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        return entity.Id;
    }

    public async Task<AttachmentDownload> GetForDownloadAsync(
        Guid requestId,
        Guid attachmentId,
        CancellationToken cancellationToken = default)
    {
        await GetVisibleAsync(requestId, cancellationToken);
        var attachment = await _context.Set<Attachment>()
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == attachmentId && a.RequestId == requestId, cancellationToken)
            ?? throw new NotFoundException("ATTACHMENT_NOT_FOUND", $"Attachment {attachmentId} was not found.");

        var stream = await _fileStore.OpenReadAsync(attachment.StoredFileName, cancellationToken);
        await _writer.AddAuditAsync(
            "Attachment.Downloaded",
            "Attachment",
            attachment.Id,
            new { requestId },
            _currentUser.UserId,
            cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);

        return new AttachmentDownload
        {
            Content = stream,
            OriginalFileName = attachment.OriginalFileName,
            ContentType = attachment.ContentType
        };
    }

    private async Task<Cms.Domain.Entities.Cases.Request> GetVisibleAsync(
        Guid requestId,
        CancellationToken cancellationToken)
    {
        var request = await _context.Set<Cms.Domain.Entities.Cases.Request>()
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == requestId, cancellationToken)
            ?? throw new NotFoundException("REQUEST_NOT_FOUND", $"Request {requestId} was not found.");

        var userId = RequireUserId();
        if (!IsStaff() && request.RequesterUserId != userId)
        {
            throw new NotFoundException("REQUEST_NOT_FOUND", $"Request {requestId} was not found.");
        }

        return request;
    }

    private static (string Extension, string ContentType)? Detect(byte[] bytes)
    {
        if (bytes.Length >= 3 && bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF)
        {
            return (".jpg", "image/jpeg");
        }

        if (bytes.Length >= 8 &&
            bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47 &&
            bytes[4] == 0x0D && bytes[5] == 0x0A && bytes[6] == 0x1A && bytes[7] == 0x0A)
        {
            return (".png", "image/png");
        }

        if (bytes.Length >= 5 &&
            bytes[0] == 0x25 && bytes[1] == 0x50 && bytes[2] == 0x44 && bytes[3] == 0x46 && bytes[4] == 0x2D)
        {
            return (".pdf", "application/pdf");
        }

        return null;
    }

    private static string SanitizeOriginalName(string? fileName)
    {
        var normalized = (fileName ?? string.Empty).Replace('\\', '/');
        var safe = normalized[(normalized.LastIndexOf('/') + 1)..].Trim();
        if (string.IsNullOrWhiteSpace(safe))
        {
            safe = "attachment";
        }

        return safe.Length <= 255 ? safe : safe[..255];
    }

    private Guid RequireUserId() =>
        _currentUser.UserId
        ?? throw new ForbiddenException("AUTHENTICATION_REQUIRED", "Authenticated user is required.");

    private bool IsStaff() => _currentUser.Roles.Any(r => StaffRoles.Contains(r));
}
