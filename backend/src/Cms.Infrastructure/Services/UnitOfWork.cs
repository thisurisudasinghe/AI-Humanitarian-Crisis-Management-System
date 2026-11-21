using Cms.Application.Interfaces;
using Cms.Infrastructure.Data;

namespace Cms.Infrastructure.Services;

public class UnitOfWork : IUnitOfWork
{
    private readonly CmsDbContext _context;

    public UnitOfWork(CmsDbContext context)
    {
        _context = context;
    }

    public async Task<int> SaveChangesAsync(
        CancellationToken cancellationToken = default)
    {
        return await _context.SaveChangesAsync(cancellationToken);
    }
}