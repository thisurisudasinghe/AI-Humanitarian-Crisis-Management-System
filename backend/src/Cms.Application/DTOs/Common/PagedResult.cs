namespace Cms.Application.DTOs.Common;

public class PagedResult<T>
{
    public IReadOnlyList<T> Items { get; init; } = Array.Empty<T>();

    public int Total { get; init; }

    public int Page { get; init; } = 1;

    public int PageSize { get; init; } = 20;
}
