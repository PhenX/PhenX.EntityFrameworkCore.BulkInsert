using DotNet.Testcontainers.Containers;

using Microsoft.Data.SqlClient;
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
            .WithName(GetRandomContainerName())
            .Build();
    }

    protected override string GetConnectionString()
    {
        var connectionString = new SqlConnectionStringBuilder(base.GetConnectionString())
        {
            InitialCatalog = Guid.NewGuid().ToString("D")
        };

        return connectionString.ToString();
    }

    protected override void Configure(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder
            .UseSqlServer()
            .UseBulkInsertSqlServer();
    }
}
