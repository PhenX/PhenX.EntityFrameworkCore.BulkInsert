using DotNet.Testcontainers.Containers;

using EntityFrameworkCore.ExecuteInsert.SqlServer;
using EntityFrameworkCore.ExecuteInsert.Tests.DbContext;

using Microsoft.EntityFrameworkCore;

using Testcontainers.MsSql;

namespace EntityFrameworkCore.ExecuteInsert.Tests.DbContainer;

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
            .UseExecuteInsertSqlServer();
    }
}
