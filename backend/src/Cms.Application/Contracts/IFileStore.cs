namespace Cms.Application.Contracts;

/// <summary>
/// Opaque evidence storage. Implemented by Member 1 (M1) and consumed by the
/// request and case modules. The store validates the file type and size, writes
/// the bytes under a generated key and never trusts the caller's file name or
/// path.
/// </summary>
public interface IFileStore
{
    /// <summary>Largest accepted upload in bytes.</summary>
    long MaxFileBytes { get; }

    /// <summary>
    /// Validates and persists one file. Throws
    /// <see cref="Common.ValidationException"/> for an empty, oversized or
    /// unsupported file.
    /// </summary>
    Task<StoredFile> SaveAsync(
        Stream content,
        string fileName,
        string? contentType,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Opens a stored file for reading, or returns <c>null</c> when the key is
    /// malformed or unknown. Callers must authorise access before streaming.
    /// </summary>
    Task<StoredFileContent?> OpenReadAsync(
        string key,
        CancellationToken cancellationToken = default);
}

/// <summary>Metadata returned after a successful upload.</summary>
public sealed record StoredFile(
    string Key,
    string FileName,
    string ContentType,
    long SizeBytes);

/// <summary>Readable file content returned by <see cref="IFileStore.OpenReadAsync"/>.</summary>
public sealed record StoredFileContent(Stream Content, string ContentType);
