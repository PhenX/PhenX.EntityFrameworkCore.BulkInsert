using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;

using DotNet.Testcontainers.Containers;

using LinqToDB.EntityFrameworkCore;

using Microsoft.EntityFrameworkCore;

using PhenX.EntityFrameworkCore.BulkInsert.SqlServer;

using Testcontainers.MsSql;

namespace PhenX.EntityFrameworkCore.BulkInsert.Benchmark;

[MinColumn, MaxColumn, BaselineColumn]
[MemoryDiagnoser]
[SimpleJob(RunStrategy.Throughput, launchCount: 1, warmupCount: 0, iterationCount: 5)]
public class LibComparatorSqlServer : LibComparator
{
    protected override void ConfigureDbContext()
    {
        var connectionString = GetConnectionString();

        DbContext = new TestDbContext(p => p
            .UseSqlServer(connectionString)
            .UseBulkInsertSqlServer()
            .UseLinqToDB()
        );
    }

    protected override IDatabaseContainer? GetDbContainer()
    {
        return new MsSqlBuilder().Build();
    }
}
