using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;

using DotNet.Testcontainers.Containers;

using Microsoft.EntityFrameworkCore;

using PhenX.EntityFrameworkCore.BulkInsert.PostgreSql;

using Testcontainers.PostgreSql;

namespace PhenX.EntityFrameworkCore.BulkInsert.Benchmark;

[MinColumn, MaxColumn, BaselineColumn]
[MemoryDiagnoser]
[SimpleJob(RunStrategy.Throughput, launchCount: 1, warmupCount: 0, iterationCount: 5)]
public class LibComparatorPostgreSql : LibComparator
{
    protected override void ConfigureDbContext()
    {
        var connectionString = GetConnectionString() + ";Include Error Detail=true";

        DbContext = new TestDbContext(p => p
            .UseNpgsql(connectionString)
            .UseBulkInsertPostgreSql()
        );
    }

    protected override IDatabaseContainer? GetDbContainer()
    {
        return new PostgreSqlBuilder()
            .WithDatabase("testdb")
            .WithUsername("testuser")
            .WithPassword("testpassword")
            .Build();
    }
}
