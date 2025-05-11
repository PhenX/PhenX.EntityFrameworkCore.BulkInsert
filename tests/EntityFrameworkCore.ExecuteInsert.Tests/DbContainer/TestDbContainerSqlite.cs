using System;

using DotNet.Testcontainers.Containers;

using EntityFrameworkCore.ExecuteInsert.Sqlite;
using EntityFrameworkCore.ExecuteInsert.Tests.DbContext;

using Microsoft.EntityFrameworkCore;

namespace EntityFrameworkCore.ExecuteInsert.Tests.DbContainer;

public class TestDbContainerSqlite<TDbContext> : TestDbContainer<TDbContext>
    where TDbContext : TestDbContextBase, new()
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
