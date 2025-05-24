using DotNet.Testcontainers.Containers;

using Microsoft.EntityFrameworkCore;

using PhenX.EntityFrameworkCore.BulkInsert.PostgreSql;
using PhenX.EntityFrameworkCore.BulkInsert.Tests.DbContext;

using Testcontainers.PostgreSql;

namespace PhenX.EntityFrameworkCore.BulkInsert.Tests.DbContainer;

public abstract class TestDbContainerPostgreSql<TDbContext>(string reuseId) : TestDbContainer<TDbContext>
    where TDbContext : TestDbContextBase, new()
{
    protected override IDatabaseContainer? GetDbContainer()
    {
        return new PostgreSqlBuilder()
            .WithImage("postgis/postgis") // Geo GeoSpatial support.
            .WithReuse(true)
            .WithDatabase("testdb")
            .WithUsername("testuser")
            .WithPassword("testpassword")
            .WithLabel("reuse-id", reuseId)
            .Build();
    }

    protected override void Configure(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder
            .UseNpgsql(GetConnectionString(), o =>
            {
                o.UseNetTopologySuite();
            })
            .UseBulkInsertPostgreSql();
    }
}
