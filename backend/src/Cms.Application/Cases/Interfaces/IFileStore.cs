namespace Cms.Application.Cases.Interfaces;

public interface IFileStore
{
    Task SaveAsync(Stream content, string storedFileName, CancellationToken cancellationToken = default);
    Task<Stream> OpenReadAsync(string storedFileName, CancellationToken cancellationToken = default);
}
