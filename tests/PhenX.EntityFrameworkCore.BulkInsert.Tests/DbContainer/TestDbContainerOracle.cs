using DotNet.Testcontainers.Containers;

using Microsoft.EntityFrameworkCore;

using Oracle.ManagedDataAccess.Client;

using PhenX.EntityFrameworkCore.BulkInsert.Oracle;

using Testcontainers.Oracle;

using Xunit;

namespace PhenX.EntityFrameworkCore.BulkInsert.Tests.DbContainer;

[CollectionDefinition(Name)]
public class TestDbContainerOracleCollection : ICollectionFixture<TestDbContainerOracle>
{
    public const string Name = "Oracle";
}

public class TestDbContainerOracle : TestDbContainer
{
    protected override IDatabaseContainer? GetDbContainer()
    {
        return new OracleBuilder()
            .WithReuse(true)
            .Build();
    }

    protected override void Configure(DbContextOptionsBuilder optionsBuilder, string databaseName)
    {
        optionsBuilder
            .UseOracle(GetConnectionString(databaseName), o =>
            {
                // o.UseNetTopologySuite();
            })
            .UseBulkInsertOracle();
    }

    protected override string GetConnectionString(string databaseName)
    {
        if (DbContainer == null)
        {
            return string.Empty;
        }

        var builder = new OracleConnectionStringBuilder
        {
            ConnectionString = DbContainer.GetConnectionString(),
            DataSource = databaseName,
        };

        return builder.ToString();
    }

    protected override async Task EnsureConnectedAsync<TDbContext>(TDbContext context, string databaseName)
    {
        var container = (OracleContainer)DbContainer!;

        await container.ExecScriptAsync($"CREATE DATABASE \"{databaseName}\";");
        await base.EnsureConnectedAsync(context, databaseName);
    }
}
