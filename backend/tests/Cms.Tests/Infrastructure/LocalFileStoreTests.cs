using Cms.Application.Common;
using Cms.Infrastructure.Storage;

namespace Cms.Tests.Infrastructure;

public sealed class LocalFileStoreTests : IDisposable
{
    private static readonly byte[] Png =
        [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x0D];

    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "cms-filestore-tests",
        Guid.NewGuid().ToString("N"));

    private readonly LocalFileStore _store;

    public LocalFileStoreTests()
    {
        _store = new LocalFileStore(_root);
    }

    [Fact]
    public async Task Save_writes_a_generated_key_and_round_trips_the_content()
    {
        await using var input = new MemoryStream(Png);

        var stored = await _store.SaveAsync(input, @"..\..\evidence.png", "image/png");

        Assert.Matches("^[0-9a-f]{32}\\.png$", stored.Key);
        Assert.Equal("evidence.png", stored.FileName);
        Assert.Equal("image/png", stored.ContentType);
        Assert.Equal(Png.Length, stored.SizeBytes);
        Assert.True(File.Exists(Path.Combine(_root, stored.Key)));

        var opened = await _store.OpenReadAsync(stored.Key);
        Assert.NotNull(opened);

        await using var content = opened!.Content;
        using var copy = new MemoryStream();
        await content.CopyToAsync(copy);

        Assert.Equal(Png, copy.ToArray());
        Assert.Equal("image/png", opened.ContentType);
    }

    [Theory]
    [InlineData("../escape.png")]
    [InlineData(@"C:\Windows\win.ini")]
    [InlineData("0123456789abcdef0123456789abcdef.png/../escape.png")]
    [InlineData("not-a-key.png")]
    public async Task OpenRead_rejects_keys_outside_the_store(string key)
    {
        Assert.Null(await _store.OpenReadAsync(key));
    }

    [Fact]
    public async Task Save_rejects_content_that_does_not_match_the_declared_type()
    {
        await using var input = new MemoryStream("not a png"u8.ToArray());

        var error = await Assert.ThrowsAsync<ValidationException>(
            () => _store.SaveAsync(input, "fake.png", "image/png"));

        Assert.Equal("FILE_TYPE_MISMATCH", error.Code);
    }

    [Fact]
    public async Task Save_rejects_unsupported_content_types()
    {
        await using var input = new MemoryStream(Png);

        var error = await Assert.ThrowsAsync<ValidationException>(
            () => _store.SaveAsync(input, "script.txt", "text/plain"));

        Assert.Equal("FILE_TYPE_NOT_ALLOWED", error.Code);
    }

    [Fact]
    public async Task Save_rejects_oversized_content()
    {
        var oversized = new byte[LocalFileStore.MaxUploadBytes + 1];
        Png.CopyTo(oversized, 0);

        await using var input = new MemoryStream(oversized);

        var error = await Assert.ThrowsAsync<ValidationException>(
            () => _store.SaveAsync(input, "big.png", "image/png"));

        Assert.Equal("FILE_TOO_LARGE", error.Code);
        Assert.False(Directory.Exists(_root));
    }

    [Fact]
    public async Task Save_infers_the_type_from_the_extension_when_omitted()
    {
        await using var input = new MemoryStream("%PDF-1.7"u8.ToArray());

        var stored = await _store.SaveAsync(input, "notice.pdf", null);

        Assert.Equal("application/pdf", stored.ContentType);
        Assert.EndsWith(".pdf", stored.Key);
    }

    [Fact]
    public async Task Save_rejects_empty_files()
    {
        await using var input = new MemoryStream();

        var error = await Assert.ThrowsAsync<ValidationException>(
            () => _store.SaveAsync(input, "empty.png", "image/png"));

        Assert.Equal("FILE_EMPTY", error.Code);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }
}
