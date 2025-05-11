using DotNet.Testcontainers.Containers;

using EntityFrameworkCore.ExecuteInsert.PostgreSql;
using EntityFrameworkCore.ExecuteInsert.Tests.DbContext;

using Microsoft.EntityFrameworkCore;

using Testcontainers.PostgreSql;

namespace EntityFrameworkCore.ExecuteInsert.Tests.DbContainer;

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
            .UseExecuteInsertPostgres();
    }
}
