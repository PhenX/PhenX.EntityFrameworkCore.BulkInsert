using DotNet.Testcontainers.Containers;

using EntityFrameworkCore.ExecuteInsert.SqlServer;

using Microsoft.EntityFrameworkCore;

using Testcontainers.MsSql;

namespace EntityFrameworkCore.ExecuteInsert.Tests.DbContainer;

public class BulkInsertProviderDbContainerSqlServer<TDbContext> : BulkInsertProviderDbContainer<TDbContext>
    where TDbContext : BulkDbContext, new()
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
