using Cms.Application.Common;
using Microsoft.EntityFrameworkCore;

namespace Cms.Infrastructure.Data;

/// <summary>EF Core implementation of the generic repository.</summary>
public class EfRepository<TEntity> : IRepository<TEntity> where TEntity : class
{
    private readonly CmsDbContext _context;
    private readonly DbSet<TEntity> _set;

    public EfRepository(CmsDbContext context)
    {
        _context = context;
        _set = context.Set<TEntity>();
    }

    public async Task<TEntity?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _set.FindAsync([id], cancellationToken);
    }

    public async Task<IReadOnlyList<TEntity>> ListAsync(CancellationToken cancellationToken = default)
    {
        return await _set.AsNoTracking().ToListAsync(cancellationToken);
    }

    public async Task AddAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        await _set.AddAsync(entity, cancellationToken);
    }

    public void Update(TEntity entity) => _set.Update(entity);

    public void Remove(TEntity entity) => _set.Remove(entity);

    public IQueryable<TEntity> Query() => _set.AsQueryable();
}
