using System.Text.RegularExpressions;
using Cms.Application.Common;
using Cms.Application.Contracts;
using Microsoft.Extensions.Logging;

namespace Cms.Infrastructure.Storage;

/// <summary>
/// Local disk implementation of <see cref="IFileStore"/>. Files are written
/// under a generated key with an extension derived from the validated content
/// type, so a caller-supplied name or path can never influence the target path.
/// </summary>
public sealed class LocalFileStore : IFileStore
{
    /// <summary>
    /// Matches the API body limit (1 MiB) with headroom for multipart framing.
    /// </summary>
    public const long MaxUploadBytes = 1_000_000;

    private static readonly Regex KeyPattern = new(
        @"^[0-9a-f]{32}\.(png|jpg|webp|pdf)$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Dictionary<string, string> AllowedTypes =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["image/png"] = ".png",
            ["image/jpeg"] = ".jpg",
            ["image/webp"] = ".webp",
            ["application/pdf"] = ".pdf"
        };

    private static readonly Dictionary<string, string> ExtensionTypes =
        new(StringComparer.OrdinalIgnoreCase)
        {
            [".png"] = "image/png",
            [".jpg"] = "image/jpeg",
            [".jpeg"] = "image/jpeg",
            [".webp"] = "image/webp",
            [".pdf"] = "application/pdf"
        };

    private static readonly byte[] PngSignature =
        [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    private readonly string _root;
    private readonly ILogger<LocalFileStore>? _logger;

    public LocalFileStore(string rootPath, ILogger<LocalFileStore>? logger = null)
    {
        if (string.IsNullOrWhiteSpace(rootPath))
        {
            throw new ArgumentException("A storage root path is required.", nameof(rootPath));
        }

        _root = Path.GetFullPath(rootPath);
        _logger = logger;
    }

    public long MaxFileBytes => MaxUploadBytes;

    public async Task<StoredFile> SaveAsync(
        Stream content,
        string fileName,
        string? contentType,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);

        var safeName = NormalizeFileName(fileName);
        var resolvedType = ResolveContentType(contentType, safeName);
        var extension = AllowedTypes[resolvedType];

        using var buffer = new MemoryStream();
        var chunk = new byte[81920];
        long size = 0;
        int read;

        while ((read = await content.ReadAsync(chunk.AsMemory(), cancellationToken)) > 0)
        {
            size += read;

            if (size > MaxUploadBytes)
            {
                throw new ValidationException(
                    $"Files larger than {MaxUploadBytes} bytes are rejected.",
                    FieldErrors("file", $"The file must be {MaxUploadBytes} bytes or smaller."),
                    "FILE_TOO_LARGE");
            }

            await buffer.WriteAsync(chunk.AsMemory(0, read), cancellationToken);
        }

        if (size == 0)
        {
            throw new ValidationException(
                "The file is empty.",
                FieldErrors("file", "Select a non-empty file."),
                "FILE_EMPTY");
        }

        var bytes = buffer.ToArray();

        if (!MatchesSignature(resolvedType, bytes))
        {
            throw new ValidationException(
                "The file content does not match its declared type.",
                FieldErrors("file", "Upload a valid PNG, JPEG, WebP or PDF file."),
                "FILE_TYPE_MISMATCH");
        }

        var key = $"{Guid.NewGuid():N}{extension}";
        Directory.CreateDirectory(_root);
        var path = Path.Combine(_root, key);

        try
        {
            await using var target = new FileStream(
                path,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                81920,
                FileOptions.Asynchronous);

            await target.WriteAsync(bytes.AsMemory(), cancellationToken);
            await target.FlushAsync(cancellationToken);
        }
        catch
        {
            TryDelete(path);
            throw;
        }

        _logger?.LogInformation(
            "Stored evidence {Key} ({SizeBytes} bytes, {ContentType})",
            key,
            size,
            resolvedType);

        return new StoredFile(key, safeName, resolvedType, size);
    }

    public Task<StoredFileContent?> OpenReadAsync(
        string key,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(key) || !KeyPattern.IsMatch(key))
        {
            return Task.FromResult<StoredFileContent?>(null);
        }

        var path = Path.GetFullPath(Path.Combine(_root, key));

        if (!path.StartsWith(_root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
            || !File.Exists(path))
        {
            return Task.FromResult<StoredFileContent?>(null);
        }

        var extension = Path.GetExtension(key);

        if (!ExtensionTypes.TryGetValue(extension, out var contentType))
        {
            return Task.FromResult<StoredFileContent?>(null);
        }

        Stream stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            81920,
            FileOptions.Asynchronous);

        return Task.FromResult<StoredFileContent?>(
            new StoredFileContent(stream, contentType));
    }

    private static string NormalizeFileName(string fileName)
    {
        var trimmed = (fileName ?? string.Empty).Trim();

        if (trimmed.Length == 0)
        {
            throw new ValidationException(
                "A file name is required.",
                FieldErrors("file", "A file name is required."),
                "FILE_NAME_REQUIRED");
        }

        var leaf = trimmed.Replace('\\', '/');
        leaf = leaf[(leaf.LastIndexOf('/') + 1)..].Trim();

        if (leaf.Length == 0 || leaf is "." or "..")
        {
            throw new ValidationException(
                "A valid file name is required.",
                FieldErrors("file", "A valid file name is required."),
                "FILE_NAME_INVALID");
        }

        if (leaf.Length > 255 || leaf.Any(char.IsControl))
        {
            throw new ValidationException(
                "The file name is not valid.",
                FieldErrors("file", "Use a name of at most 255 characters without control characters."),
                "FILE_NAME_INVALID");
        }

        return leaf;
    }

    private static string ResolveContentType(string? contentType, string fileName)
    {
        var candidate = contentType?.Split(';', 2)[0].Trim().ToLowerInvariant()
            ?? string.Empty;

        if (candidate.Length > 0
            && !string.Equals(candidate, "application/octet-stream", StringComparison.Ordinal))
        {
            if (AllowedTypes.ContainsKey(candidate))
            {
                return candidate;
            }

            throw new ValidationException(
                $"Unsupported file type '{candidate}'.",
                FieldErrors("file", "Allowed types: PNG, JPEG, WebP and PDF."),
                "FILE_TYPE_NOT_ALLOWED");
        }

        var extension = Path.GetExtension(fileName);

        if (extension.Length > 0 && ExtensionTypes.TryGetValue(extension, out var inferred))
        {
            return inferred;
        }

        throw new ValidationException(
            "A supported content type is required.",
            FieldErrors("file", "Allowed types: PNG, JPEG, WebP and PDF."),
            "FILE_TYPE_REQUIRED");
    }

    private static bool MatchesSignature(string contentType, ReadOnlySpan<byte> bytes) =>
        contentType switch
        {
            "image/png" =>
                bytes.Length >= 8 && bytes[..8].SequenceEqual(PngSignature),
            "image/jpeg" =>
                bytes.Length >= 3 && bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF,
            "image/webp" =>
                bytes.Length >= 12
                && bytes[..4].SequenceEqual("RIFF"u8)
                && bytes[8..12].SequenceEqual("WEBP"u8),
            "application/pdf" =>
                bytes.Length >= 5 && bytes[..5].SequenceEqual("%PDF-"u8),
            _ => false
        };

    private static Dictionary<string, string[]> FieldErrors(string field, string message) =>
        new() { [field] = [message] };

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (IOException)
        {
            // Best effort: a leftover partial file is harmless.
        }
        catch (UnauthorizedAccessException)
        {
            // Best effort: a leftover partial file is harmless.
        }
    }
}
