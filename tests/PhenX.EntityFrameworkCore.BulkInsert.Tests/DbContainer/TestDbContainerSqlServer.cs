using DotNet.Testcontainers.Containers;

using Microsoft.EntityFrameworkCore;

using PhenX.EntityFrameworkCore.BulkInsert.SqlServer;
using PhenX.EntityFrameworkCore.BulkInsert.Tests.DbContext;

using Testcontainers.MsSql;

namespace PhenX.EntityFrameworkCore.BulkInsert.Tests.DbContainer;

public class TestDbContainerSqlServer<TDbContext> : TestDbContainer<TDbContext>
    where TDbContext : TestDbContextBase, new()
{
    protected override IDatabaseContainer? GetDbContainer()
    {
        return new MsSqlBuilder().Build();
    }

    protected override void Configure(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder
            .UseSqlServer()
            .UseBulkInsertSqlServer();
    }
}
