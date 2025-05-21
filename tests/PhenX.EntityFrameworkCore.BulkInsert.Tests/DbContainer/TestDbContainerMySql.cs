using DotNet.Testcontainers.Containers;

using Microsoft.EntityFrameworkCore;

using PhenX.EntityFrameworkCore.BulkInsert.MySql;
using PhenX.EntityFrameworkCore.BulkInsert.Tests.DbContext;

using Testcontainers.MySql;

namespace PhenX.EntityFrameworkCore.BulkInsert.Tests.DbContainer;

public class TestDbContainerMySql<TDbContext> : TestDbContainer<TDbContext>
    where TDbContext : TestDbContextBase, new()
{
    protected override IDatabaseContainer? GetDbContainer()
    {
        return new MySqlBuilder()
            .WithReuse(true)
            .WithCommand("--log-bin-trust-function-creators=1", "--local-infile=1")
            .Build();
    }

    protected override string GetConnectionString()
    {
        return $"{base.GetConnectionString()};AllowLoadLocalInfile=true;";
    }

    protected override void Configure(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder
            .UseMySql(ServerVersion.AutoDetect(GetConnectionString()))
            .UseBulkInsertMySql();
    }
}
