using DotNet.Testcontainers.Containers;

using Microsoft.EntityFrameworkCore;

using PhenX.EntityFrameworkCore.BulkInsert.SqlServer;

using Testcontainers.MsSql;

using Xunit;

namespace PhenX.EntityFrameworkCore.BulkInsert.Tests.DbContainer;

[CollectionDefinition(Name)]
public class TestDbContainerSqlServerCollection : ICollectionFixture<TestDbContainerSqlServer>
{
    public const string Name = "SqlServer";
}

public class TestDbContainerSqlServer : TestDbContainer
{
    protected override IDatabaseContainer? GetDbContainer()
    {
        return new MsSqlBuilder()
            .WithImage("vibs2006/sql_server_fts") // Geo Geospatial support
            .WithReuse(true)
            .Build();
    }

    protected override void Configure(DbContextOptionsBuilder optionsBuilder, string databaseName)
    {
        optionsBuilder
            .UseSqlServer(GetConnectionString(databaseName), o =>
            {
                o.UseNetTopologySuite();
            })
            .UseBulkInsertSqlServer();
    }

    protected override async Task EnsureConnectedAsync<TDbContext>(TDbContext context, string databaseName)
    {
        var container = (MsSqlContainer)DbContainer!;

        await container.ExecScriptAsync($"CREATE DATABASE [{databaseName}]");
        await base.EnsureConnectedAsync(context, databaseName);
    }
}
