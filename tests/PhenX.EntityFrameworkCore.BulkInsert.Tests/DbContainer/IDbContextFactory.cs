using PhenX.EntityFrameworkCore.BulkInsert.Tests.DbContext;

namespace PhenX.EntityFrameworkCore.BulkInsert.Tests.DbContainer;

public interface IDbContextFactory
{
    Task<TDbContext> CreateContextAsync<TDbContext>(string databaseName)
        where TDbContext : TestDbContextBase, new();
}
