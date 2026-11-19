using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Cms.Application.DTOs;
using Cms.Application.DTOs.Audit;
using Cms.Application.DTOs.Files;
using Cms.Infrastructure.Storage;

namespace Cms.Tests.Integration;

public sealed class FileUploadTests : IClassFixture<CmsWebApplicationFactory>
{
    private static readonly byte[] Png =
        [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x0D];

    private readonly CmsWebApplicationFactory _factory;

    public FileUploadTests(CmsWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Anonymous_upload_is_rejected()
    {
        using var client = _factory.CreateClient();
        using var form = BuildForm(Png, "evidence.png", "image/png");

        var response = await client.PostAsync("/api/v1/files", form);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Authenticated_upload_is_stored_and_audited()
    {
        using var citizen = await _factory.CreateAuthenticatedClientAsync(
            CmsWebApplicationFactory.CitizenEmail);
        using var form = BuildForm(Png, "evidence.png", "image/png");

        var response = await citizen.PostAsync("/api/v1/files", form);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var uploaded = await response.Content.ReadFromJsonAsync<UploadedFileDto>();
        Assert.NotNull(uploaded);
        Assert.Matches("^[0-9a-f]{32}\\.png$", uploaded!.Key);
        Assert.Equal("evidence.png", uploaded.FileName);
        Assert.Equal("image/png", uploaded.ContentType);
        Assert.Equal(Png.Length, uploaded.SizeBytes);
        Assert.True(File.Exists(Path.Combine(_factory.StorageRoot, uploaded.Key)));

        using var admin = await _factory.CreateAuthenticatedClientAsync(
            CmsWebApplicationFactory.AdminEmail);

        var auditResponse = await admin.GetAsync(
            "/api/v1/audit-events?action=File.Uploaded&pageSize=50");
        Assert.Equal(HttpStatusCode.OK, auditResponse.StatusCode);

        var audit = await auditResponse.Content
            .ReadFromJsonAsync<PagedResultDto<AuditEventDto>>();
        Assert.NotNull(audit);
        Assert.Contains(audit!.Items, item => item.EntityId == uploaded.Key);
    }

    [Fact]
    public async Task Upload_rejects_unsupported_types_and_spoofed_content()
    {
        using var citizen = await _factory.CreateAuthenticatedClientAsync(
            CmsWebApplicationFactory.CitizenEmail);

        using var textForm = BuildForm("hello"u8.ToArray(), "notes.txt", "text/plain");
        var textResponse = await citizen.PostAsync("/api/v1/files", textForm);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, textResponse.StatusCode);

        using var spoofedForm = BuildForm("not a png"u8.ToArray(), "fake.png", "image/png");
        var spoofedResponse = await citizen.PostAsync("/api/v1/files", spoofedForm);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, spoofedResponse.StatusCode);
    }

    [Fact]
    public async Task Upload_rejects_files_over_the_size_limit()
    {
        using var citizen = await _factory.CreateAuthenticatedClientAsync(
            CmsWebApplicationFactory.CitizenEmail);

        var oversized = new byte[LocalFileStore.MaxUploadBytes + 1];
        Png.CopyTo(oversized, 0);

        using var form = BuildForm(oversized, "big.png", "image/png");
        var response = await citizen.PostAsync("/api/v1/files", form);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task Upload_requires_a_file_field()
    {
        using var citizen = await _factory.CreateAuthenticatedClientAsync(
            CmsWebApplicationFactory.CitizenEmail);
        using var form = new MultipartFormDataContent();

        var response = await citizen.PostAsync("/api/v1/files", form);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    private static MultipartFormDataContent BuildForm(
        byte[] content,
        string fileName,
        string contentType)
    {
        var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(content);
        file.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        form.Add(file, "file", fileName);

        return form;
    }
}
