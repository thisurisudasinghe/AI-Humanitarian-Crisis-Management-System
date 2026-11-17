namespace Cms.Application.DTOs;

public class PagedResultDto<T>
{
    public IReadOnlyCollection<T> Items { get; set; } = Array.Empty<T>();

    public int Page { get; set; } = 1;

    public int PageSize { get; set; } = 50;

    public int TotalCount { get; set; }

    public int TotalPages => PageSize <= 0
        ? 0
        : (int)Math.Ceiling(TotalCount / (double)PageSize);
}
