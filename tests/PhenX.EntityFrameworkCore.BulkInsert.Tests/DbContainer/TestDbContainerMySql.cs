using DotNet.Testcontainers.Containers;

using Microsoft.EntityFrameworkCore;

using PhenX.EntityFrameworkCore.BulkInsert.MySql;

using Testcontainers.MySql;

using Xunit;

namespace PhenX.EntityFrameworkCore.BulkInsert.Tests.DbContainer;

[CollectionDefinition(Name)]
public class TestDbContainerMySqlCollection : ICollectionFixture<TestDbContainerMySql>
{
    public const string Name = "MySql";
}

public class TestDbContainerMySql() : TestDbContainer
{
    protected override IDatabaseContainer? GetDbContainer()
    {
        return new MySqlBuilder()
            .WithCommand("--log-bin-trust-function-creators=1", "--local-infile=1", "--innodb-print-all-deadlocks=ON")
            .WithReuse(true)
            .WithUsername("root")
            .WithPassword("root")
            .Build();
    }

    protected override string GetConnectionString(string databaseName)
    {
        return $"{base.GetConnectionString(databaseName)};AllowLoadLocalInfile=true;";
    }

    protected override void Configure(DbContextOptionsBuilder optionsBuilder, string databaseName)
    {
        var connectionString = GetConnectionString(databaseName);

        optionsBuilder
            .UseMySql(connectionString, ServerVersion.AutoDetect(connectionString), o =>
            {
                o.UseNetTopologySuite();
            })
            .UseBulkInsertMySql();
    }

    protected override async Task EnsureConnectedAsync<TDbContext>(TDbContext context, string databaseName)
    {
        var container = (MySqlContainer)DbContainer!;

        await container.ExecScriptAsync($"CREATE DATABASE `{databaseName}`");
        await base.EnsureConnectedAsync(context, databaseName);
    }
}
