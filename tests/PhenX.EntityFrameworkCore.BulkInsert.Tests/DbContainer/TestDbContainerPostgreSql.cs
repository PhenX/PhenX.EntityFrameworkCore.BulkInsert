using System.Data.Common;

using Microsoft.EntityFrameworkCore;

using Npgsql;

using PhenX.EntityFrameworkCore.BulkInsert.PostgreSql;

using Testcontainers.PostgreSql;

using Xunit;
using Xunit.Abstractions;

namespace PhenX.EntityFrameworkCore.BulkInsert.Tests.DbContainer;

[CollectionDefinition(Name)]
public class TestDbContainerPostgreSqlCollection : ICollectionFixture<TestDbContainerPostgreSql>
{
    public const string Name = "PostgreSql";
}

public class TestDbContainerPostgreSql(IMessageSink messageSink) : TestDbContainer<PostgreSqlBuilder, PostgreSqlContainer>(messageSink)
{
    public override DbProviderFactory DbProviderFactory => NpgsqlFactory.Instance;

    // GeoSpatial support, using imresamu/postgis instead of postgis/postgis for arm64 support, see https://github.com/postgis/docker-postgis/issues/216#issuecomment-2936824962
    protected override PostgreSqlBuilder CreateBuilder() => new("imresamu/postgis:17-3.5");

    protected override void Configure(DbContextOptionsBuilder optionsBuilder, string databaseName)
    {
        optionsBuilder
            .UseNpgsql(GetConnectionString(databaseName), o =>
            {
                o.UseNetTopologySuite();
            })
            .UseBulkInsertPostgreSql();
    }
}
