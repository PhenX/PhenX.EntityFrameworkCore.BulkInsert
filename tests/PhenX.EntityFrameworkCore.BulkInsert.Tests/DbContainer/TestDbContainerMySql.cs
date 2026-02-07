using System.Data.Common;

using Microsoft.EntityFrameworkCore;

using MySqlConnector;

using PhenX.EntityFrameworkCore.BulkInsert.MySql;

using Pomelo.EntityFrameworkCore.MySql.Infrastructure;

using Testcontainers.MySql;

using Xunit;
using Xunit.Abstractions;

namespace PhenX.EntityFrameworkCore.BulkInsert.Tests.DbContainer;

[CollectionDefinition(Name)]
public class TestDbContainerMySqlCollection : ICollectionFixture<TestDbContainerMySql>
{
    public const string Name = "MySql";
}

public class TestDbContainerMySql(IMessageSink messageSink) : TestDbContainer<MySqlBuilder, MySqlContainer>(messageSink)
{
    private static readonly ServerVersion MySqlServerVersion = ServerVersion.Create(new Version(8, 0), ServerType.MySql);

    public override DbProviderFactory DbProviderFactory => MySqlConnectorFactory.Instance;

    protected override MySqlBuilder CreateBuilder() => new($"{MySqlServerVersion.TypeIdentifier}:{MySqlServerVersion.Version}");

    protected override string DbmsName => MySqlServerVersion.Type.ToString();

    protected override MySqlBuilder Configure()
    {
        return base.Configure()
            .WithCommand("--log-bin-trust-function-creators=1", "--local-infile=1", "--innodb-print-all-deadlocks=ON")
            .WithUsername("root")
            .WithPassword("root");
    }

    protected override string GetConnectionString(string databaseName)
    {
        return $"{base.GetConnectionString(databaseName)};AllowLoadLocalInfile=true;";
    }

    protected override void Configure(DbContextOptionsBuilder optionsBuilder, string databaseName)
    {
        var connectionString = GetConnectionString(databaseName);

        optionsBuilder
            .UseMySql(connectionString, MySqlServerVersion, o =>
            {
                o.UseNetTopologySuite();
            })
            .UseBulkInsertMySql();
    }
}
