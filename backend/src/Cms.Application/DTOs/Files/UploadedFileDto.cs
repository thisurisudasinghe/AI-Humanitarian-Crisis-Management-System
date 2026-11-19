namespace Cms.Application.DTOs.Files;

/// <summary>Result of a standalone evidence upload.</summary>
public class UploadedFileDto
{
    /// <summary>
    /// Opaque storage key. Persist it with the owning record; it is the only
    /// value needed to read the file back through <c>IFileStore</c>.
    /// </summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>Original file name, stripped of any directory component.</summary>
    public string FileName { get; set; } = string.Empty;

    public string ContentType { get; set; } = string.Empty;

    public long SizeBytes { get; set; }
}
