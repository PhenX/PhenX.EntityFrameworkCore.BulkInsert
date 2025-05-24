using DotNet.Testcontainers.Containers;

using Microsoft.EntityFrameworkCore;

using PhenX.EntityFrameworkCore.BulkInsert.Sqlite;
using PhenX.EntityFrameworkCore.BulkInsert.Tests.DbContext;

namespace PhenX.EntityFrameworkCore.BulkInsert.Tests.DbContainer;

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
            .UseSqlite(GetConnectionString())
            .UseBulkInsertSqlite();
    }

    protected override Task EnsureConnectedAsync(TDbContext context)
    {
        return Task.CompletedTask;
    }
}
