using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;

using DotNet.Testcontainers.Containers;

using EFCore.BulkExtensions;

using EntityFrameworkCore.ExecuteInsert.Abstractions;
using EntityFrameworkCore.ExecuteInsert.PostgreSql;

using Microsoft.EntityFrameworkCore;

using Testcontainers.PostgreSql;

namespace EntityFrameworkCore.ExecuteInsert.Benchmark;

[MinColumn, MaxColumn, BaselineColumn]
[MemoryDiagnoser]
[SimpleJob(RunStrategy.Throughput, launchCount: 1, warmupCount: 0, iterationCount: 5)]
public class BulkInsertVsExecuteInsertPostgreSql : BulkInsertVsExecuteInsert
{
    protected override void ConfigureDbContext()
    {
        var connectionString = DbContainer.GetConnectionString() + ";Include Error Detail=true";

        DbContext = new TestDbContext(p => p
            .UseNpgsql(connectionString)
            .UseExecuteInsertPostgres()
        );
    }

    protected override IDatabaseContainer GetDbContainer()
    {
        return new PostgreSqlBuilder()
            .WithDatabase("testdb")
            .WithUsername("testuser")
            .WithPassword("testpassword")
            .Build();
    }
}
