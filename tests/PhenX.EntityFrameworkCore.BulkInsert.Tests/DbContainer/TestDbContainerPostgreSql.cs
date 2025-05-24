using DotNet.Testcontainers.Containers;

using Microsoft.EntityFrameworkCore;

using PhenX.EntityFrameworkCore.BulkInsert.PostgreSql;

using Testcontainers.PostgreSql;

using Xunit;

namespace PhenX.EntityFrameworkCore.BulkInsert.Tests.DbContainer;

[CollectionDefinition(Name)]
public class TestDbContainerPostgreSqlCollection : ICollectionFixture<TestDbContainerPostgreSql>
{
    public const string Name = "PostgreSql";
}

public class TestDbContainerPostgreSql : TestDbContainer
{
    protected override IDatabaseContainer? GetDbContainer()
    {
        return new PostgreSqlBuilder()
            .WithImage("postgis/postgis") // Geo GeoSpatial support.
            .WithReuse(true)
            .WithDatabase("testdb")
            .WithUsername("testuser")
            .WithPassword("testpassword")
            .Build();
    }

    protected override void Configure(DbContextOptionsBuilder optionsBuilder, string databaseName)
    {
        optionsBuilder
            .UseNpgsql(GetConnectionString(databaseName), o =>
            {
                o.UseNetTopologySuite();
            })
            .UseBulkInsertPostgreSql();
    }

    protected override async Task EnsureConnectedAsync<TDbContext>(TDbContext context, string databaseName)
    {
        var container = (PostgreSqlContainer)DbContainer!;

        await container.ExecScriptAsync($"CREATE DATABASE \"{databaseName}\"");
        await base.EnsureConnectedAsync(context, databaseName);
    }
}
