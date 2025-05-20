using DotNet.Testcontainers.Containers;

using Microsoft.EntityFrameworkCore;

using PhenX.EntityFrameworkCore.BulkInsert.PostgreSql;
using PhenX.EntityFrameworkCore.BulkInsert.Tests.DbContext;

using Testcontainers.PostgreSql;

namespace PhenX.EntityFrameworkCore.BulkInsert.Tests.DbContainer;

public class TestDbContainerPostgreSql<TDbContext> : TestDbContainer<TDbContext>
    where TDbContext : TestDbContextBase, new()
{
    protected override IDatabaseContainer? GetDbContainer()
    {
        return new PostgreSqlBuilder()
            .WithDatabase("testdb")
            .WithUsername("testuser")
            .WithPassword("testpassword")
            .Build();
    }

    protected override void Configure(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder
            .UseNpgsql()
            .UseBulkInsertPostgreSql();
    }
}
