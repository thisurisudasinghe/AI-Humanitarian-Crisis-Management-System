using Cms.Application.Cases.Common;
using Cms.Application.Cases.Interfaces;

namespace Cms.Infrastructure.Cases;

public sealed class LocalFileStore : IFileStore
{
    private readonly string _root;

    public LocalFileStore(CasesOptions options)
    {
        _root = Path.GetFullPath(options.UploadRoot);
    }

    public async Task SaveAsync(
        Stream content,
        string storedFileName,
        CancellationToken cancellationToken = default)
    {
        var path = Resolve(storedFileName);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        await using var output = new FileStream(
            path,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            81920,
            useAsync: true);
        await content.CopyToAsync(output, cancellationToken);
    }

    public Task<Stream> OpenReadAsync(
        string storedFileName,
        CancellationToken cancellationToken = default)
    {
        var path = Resolve(storedFileName);
        if (!File.Exists(path))
        {
            throw new FileNotFoundException("Stored attachment was not found.", storedFileName);
        }

        Stream stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            81920,
            useAsync: true);
        return Task.FromResult(stream);
    }

    private string Resolve(string storedFileName)
    {
        if (!Guid.TryParse(Path.GetFileNameWithoutExtension(storedFileName), out _))
        {
            throw new InvalidOperationException("Stored file name must be a GUID-based name.");
        }

        var path = Path.GetFullPath(Path.Combine(_root, storedFileName));
        var rootWithSeparator = _root.EndsWith(Path.DirectorySeparatorChar)
            ? _root
            : _root + Path.DirectorySeparatorChar;

        if (!path.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Resolved file path escaped the upload root.");
        }

        return path;
    }
}
