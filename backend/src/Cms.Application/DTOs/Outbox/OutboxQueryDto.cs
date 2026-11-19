namespace Cms.Application.DTOs.Outbox;

public class OutboxQueryDto
{
    public string? Type { get; set; }

    public int Page { get; set; } = 1;

    public int PageSize { get; set; } = 50;
}
