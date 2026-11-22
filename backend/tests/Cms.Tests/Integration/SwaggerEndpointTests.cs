using System.Net;
using System.Text.Json;

namespace Cms.Tests.Integration;

public sealed class SwaggerEndpointTests : IClassFixture<CmsWebApplicationFactory>
{
    private readonly CmsWebApplicationFactory _factory;

    public SwaggerEndpointTests(CmsWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Swagger_document_generates_with_binary_file_upload_schema()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/swagger/v1/swagger.json");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        var upload = document.RootElement
            .GetProperty("paths")
            .GetProperty("/api/v1/files")
            .GetProperty("post");

        var multipart = upload
            .GetProperty("requestBody")
            .GetProperty("content")
            .GetProperty("multipart/form-data");

        var file = multipart
            .GetProperty("schema")
            .GetProperty("properties")
            .GetProperty("file");

        Assert.Equal("string", file.GetProperty("type").GetString());
        Assert.Equal("binary", file.GetProperty("format").GetString());
    }
}
