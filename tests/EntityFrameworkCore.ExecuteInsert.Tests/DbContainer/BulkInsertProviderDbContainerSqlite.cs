using System;

using DotNet.Testcontainers.Containers;

using EntityFrameworkCore.ExecuteInsert.Sqlite;

using Microsoft.EntityFrameworkCore;

namespace EntityFrameworkCore.ExecuteInsert.Tests.DbContainer;

public class BulkInsertProviderDbContainerSqlite<TDbContext> : BulkInsertProviderDbContainer<TDbContext>
    where TDbContext : BulkDbContext, new()
{
    protected override IDatabaseContainer? GetDbContainer() => null;

    protected override string GetConnectionString()
    {
        // return "Data Source=:memory:;Mode=Memory;Cache=Shared";
        return $"Data Source={Guid.NewGuid()}.db";
    }

    protected override void Configure(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder
            .UseSqlite()
            .UseExecuteInsertSqlite();
    }
}
