using Cms.Application.Interfaces;
using Cms.Domain.Entities;
using Cms.Infrastructure.Data;

namespace Cms.Infrastructure.Services;

public class AuditService : IAuditService
{
    private readonly ApplicationDbContext _context;
    private readonly IDateTimeService _dateTimeService;

    public AuditService(
        ApplicationDbContext context,
        IDateTimeService dateTimeService)
    {
        _context = context;
        _dateTimeService = dateTimeService;
    }

    public async Task LogAsync(
        Guid? userId,
        string action,
        string entityName,
        string? entityId = null,
        string? details = null,
        CancellationToken cancellationToken = default)
    {
        var auditLog = new AuditLog
        {
            UserId = userId,
            Action = action,
            EntityName = entityName,
            EntityId = entityId,
            Details = details,
            CreatedAt = _dateTimeService.UtcNow
        };

        await _context.AuditLogs.AddAsync(
            auditLog,
            cancellationToken);

        await _context.SaveChangesAsync(cancellationToken);
    }
}