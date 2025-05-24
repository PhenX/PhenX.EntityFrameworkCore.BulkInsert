using DotNet.Testcontainers.Containers;

using Microsoft.EntityFrameworkCore;

using PhenX.EntityFrameworkCore.BulkInsert.SqlServer;
using PhenX.EntityFrameworkCore.BulkInsert.Tests.DbContext;

using Testcontainers.MsSql;

namespace PhenX.EntityFrameworkCore.BulkInsert.Tests.DbContainer;

public class TestDbContainerSqlServer<TDbContext> : TestDbContainer<TDbContext>
    where TDbContext : TestDbContextBase, new()
{
    protected override IDatabaseContainer? GetDbContainer()
    {
        return new MsSqlBuilder()
            .WithImage("vibs2006/sql_server_fts") // Geo Geospatial support
            .WithReuse(true)
            .Build();
    }

    protected override void Configure(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder
            .UseSqlServer(GetConnectionString(), o =>
            {
                o.UseNetTopologySuite();
            })
            .UseBulkInsertSqlServer();
    }
}
