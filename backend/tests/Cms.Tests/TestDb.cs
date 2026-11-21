using Cms.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Cms.Tests;

internal static class TestDb
{
    public static CmsDbContext Create(string? databaseName = null)
    {
        var options = new DbContextOptionsBuilder<CmsDbContext>()
            .UseInMemoryDatabase(databaseName ?? Guid.NewGuid().ToString())
            .Options;

        return new CmsDbContext(options);
    }
}
