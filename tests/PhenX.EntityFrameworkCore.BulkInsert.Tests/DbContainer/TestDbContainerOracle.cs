using DotNet.Testcontainers.Containers;

using Microsoft.EntityFrameworkCore;

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
            .WithImage("gvenzl/oracle-free:23-slim-faststart")
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

        var port = DbContainer.GetMappedPublicPort(1521);

        return $"Data Source=(DESCRIPTION = (ADDRESS_LIST = (ADDRESS = (PROTOCOL = TCP)(HOST = localhost)(PORT = {port})) ) (CONNECT_DATA = (SERVICE_NAME = FREEPDB1) ) );User ID=oracle;Password=oracle";
    }
}
