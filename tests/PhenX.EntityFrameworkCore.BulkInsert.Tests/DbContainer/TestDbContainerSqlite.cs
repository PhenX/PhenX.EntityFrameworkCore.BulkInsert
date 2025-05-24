using DotNet.Testcontainers.Containers;

using Microsoft.EntityFrameworkCore;

using PhenX.EntityFrameworkCore.BulkInsert.Sqlite;

using Xunit;

namespace PhenX.EntityFrameworkCore.BulkInsert.Tests.DbContainer;

[CollectionDefinition(Name)]
public class TestDbContainerSqliteCollection : ICollectionFixture<TestDbContainerSqlite>
{
    public const string Name = "Sqlite";
}

public class TestDbContainerSqlite : TestDbContainer
{
    protected override IDatabaseContainer? GetDbContainer() => null;

    protected override string GetConnectionString(string databaseName)
    {
        return $"Data Source={Guid.NewGuid()}.db";
    }

    protected override void Configure(DbContextOptionsBuilder optionsBuilder, string databaseName)
    {
        optionsBuilder
            .UseSqlite(GetConnectionString(databaseName))
            .UseBulkInsertSqlite();
    }

    protected override Task EnsureConnectedAsync<TDbContext>(TDbContext context, string databaseName)
    {
        return Task.CompletedTask;
    }
}
