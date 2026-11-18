using Cms.Application.Cases.DTOs;
using Cms.Domain.Entities.Cases;
using Cms.Domain.Enums.Cases;
using Cms.Domain.Exceptions;
using Cms.Infrastructure.Cases;
using Cms.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Cms.Tests.Cases;

public class AttachmentServiceTests
{
    private static readonly byte[] PngMagic =
        { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x01, 0x02 };

    [Fact]
    public async Task Upload_detects_magic_bytes_and_neutralises_traversal_name()
    {
        using var context = TestContext.Create();
        var userId = Guid.NewGuid();
        var request = TestContext.NewRequest(userId);
        await context.Set<Request>().AddAsync(request);
        await context.SaveChangesAsync();
        var store = new MemoryFileStore();
        var service = CreateService(context, store, userId);

        var id = await service.UploadAsync(request.Id, new UploadedFile
        {
            Content = new MemoryStream(PngMagic),
            FileName = "..\\..\\evil.png",
            ContentType = "application/octet-stream"
        });

        var attachment = await context.Set<Attachment>().SingleAsync(a => a.Id == id);
        Assert.Equal("image/png", attachment.ContentType);
        Assert.Equal(".png", Path.GetExtension(attachment.StoredFileName));
        Assert.Equal("evil.png", attachment.OriginalFileName);
        Assert.True(store.Files.ContainsKey(attachment.StoredFileName));
    }

    [Fact]
    public async Task Renamed_executable_with_image_extension_is_rejected()
    {
        using var context = TestContext.Create();
        var userId = Guid.NewGuid();
        var request = TestContext.NewRequest(userId);
        await context.Set<Request>().AddAsync(request);
        await context.SaveChangesAsync();
        var service = CreateService(context, new MemoryFileStore(), userId);

        await Assert.ThrowsAsync<ValidationException>(() => service.UploadAsync(request.Id, new UploadedFile
        {
            Content = new MemoryStream(new byte[] { 0x4D, 0x5A, 0x90, 0x00 }),
            FileName = "malware.png",
            ContentType = "image/png"
        }));
    }

    [Fact]
    public async Task File_larger_than_five_megabytes_is_rejected()
    {
        using var context = TestContext.Create();
        var userId = Guid.NewGuid();
        var request = TestContext.NewRequest(userId);
        await context.Set<Request>().AddAsync(request);
        await context.SaveChangesAsync();
        var bytes = new byte[(5 * 1024 * 1024) + 1];
        PngMagic.CopyTo(bytes, 0);
        var service = CreateService(context, new MemoryFileStore(), userId);

        var ex = await Assert.ThrowsAsync<ValidationException>(() => service.UploadAsync(request.Id, new UploadedFile
        {
            Content = new MemoryStream(bytes),
            FileName = "large.png",
            ContentType = "image/png"
        }));

        Assert.Equal("INVALID_ATTACHMENT_SIZE", ex.Code);
        Assert.Empty(await context.Set<Attachment>().ToListAsync());
    }

    [Fact]
    public async Task Another_citizen_cannot_download_attachment()
    {
        using var context = TestContext.Create();
        var ownerId = Guid.NewGuid();
        var request = TestContext.NewRequest(ownerId);
        var attachment = new Attachment
        {
            Id = Guid.NewGuid(),
            RequestId = request.Id,
            StoredFileName = Guid.NewGuid() + ".png",
            OriginalFileName = "evidence.png",
            ContentType = "image/png",
            SizeBytes = 1,
            UploadedByUserId = ownerId
        };
        await context.Set<Request>().AddAsync(request);
        await context.Set<Attachment>().AddAsync(attachment);
        await context.SaveChangesAsync();
        var service = CreateService(context, new MemoryFileStore(), Guid.NewGuid());

        await Assert.ThrowsAsync<NotFoundException>(
            () => service.GetForDownloadAsync(request.Id, attachment.Id));
    }

    private static AttachmentService CreateService(
        ApplicationDbContext context,
        MemoryFileStore store,
        Guid userId)
    {
        var now = DateTime.UtcNow;
        var date = new FixedDateTimeService(now);
        return new AttachmentService(
            context,
            store,
            new FakeCurrentUser { UserId = userId, Roles = new[] { Cms.Domain.Cases.CasesRoles.Citizen } },
            date,
            new CaseWriteHelper(context, date, TestContext.Options()));
    }
}
