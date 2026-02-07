using System.Data.Common;

using DotNet.Testcontainers.Images;

using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

using PhenX.EntityFrameworkCore.BulkInsert.SqlServer;

using Testcontainers.MsSql;

using Xunit;
using Xunit.Abstractions;

namespace PhenX.EntityFrameworkCore.BulkInsert.Tests.DbContainer;

[CollectionDefinition(Name)]
public class TestDbContainerSqlServerCollection : ICollectionFixture<TestDbContainerSqlServer>
{
    public const string Name = "SqlServer";
}

public class TestDbContainerSqlServer(IMessageSink messageSink) : TestDbContainer<MsSqlBuilder, MsSqlContainer>(messageSink)
{
    public override DbProviderFactory DbProviderFactory => SqlClientFactory.Instance;

    // GeoSpatial support
    protected override MsSqlBuilder CreateBuilder() => new(new DockerImage("vibs2006/sql_server_fts", new Platform("amd64")));

    protected override void Configure(DbContextOptionsBuilder optionsBuilder, string databaseName)
    {
        optionsBuilder
            .UseSqlServer(GetConnectionString(databaseName), o =>
            {
                o.UseNetTopologySuite();
            })
            .UseBulkInsertSqlServer();
    }

    protected override async Task EnsureDatabaseCreatedAsync(Microsoft.EntityFrameworkCore.DbContext dbContext)
    {
        try
        {
            await base.EnsureDatabaseCreatedAsync(dbContext);
        }
        catch (SqlException ex) when (ex.Number == 1801) // Database '%.*ls' already exists. Choose a different database name.
        {
            // Ignore, it means that the database was already created in the (reused) container
            // https://learn.microsoft.com/en-us/sql/relational-databases/errors-events/database-engine-events-and-errors-1000-to-1999
        }
    }
}
