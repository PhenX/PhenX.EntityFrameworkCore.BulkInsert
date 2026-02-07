using System.Data.Common;

using Microsoft.EntityFrameworkCore;

using Oracle.ManagedDataAccess.Client;

using PhenX.EntityFrameworkCore.BulkInsert.Oracle;

using Testcontainers.Oracle;

using Xunit;
using Xunit.Abstractions;

namespace PhenX.EntityFrameworkCore.BulkInsert.Tests.DbContainer;

[CollectionDefinition(Name)]
public class TestDbContainerOracleCollection : ICollectionFixture<TestDbContainerOracle>
{
    public const string Name = "Oracle";
}

public class TestDbContainerOracle(IMessageSink messageSink) : TestDbContainer<OracleBuilder, OracleContainer>(messageSink)
{
    public override DbProviderFactory DbProviderFactory => OracleClientFactory.Instance;

    protected override OracleBuilder CreateBuilder() => new("gvenzl/oracle-free:23-slim-faststart");

    protected override void Configure(DbContextOptionsBuilder optionsBuilder, string databaseName)
    {
        optionsBuilder
            .UseOracle(ConnectionString)
            .UseBulkInsertOracle();
    }
}
