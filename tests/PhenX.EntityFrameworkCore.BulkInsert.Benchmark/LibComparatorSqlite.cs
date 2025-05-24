using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;

using DotNet.Testcontainers.Containers;

using LinqToDB.EntityFrameworkCore;

using Microsoft.EntityFrameworkCore;

using PhenX.EntityFrameworkCore.BulkInsert.Sqlite;

namespace PhenX.EntityFrameworkCore.BulkInsert.Benchmark;

[MinColumn, MaxColumn, BaselineColumn]
[MemoryDiagnoser]
[SimpleJob(RunStrategy.Throughput, launchCount: 1, warmupCount: 0, iterationCount: 5)]
public class LibComparatorSqlite : LibComparator
{
    protected override void ConfigureDbContext()
    {
        var connectionString = GetConnectionString();

        DbContext = new TestDbContext(p => p
            .UseSqlite(connectionString)
            .UseBulkInsertSqlite()
            .UseLinqToDB()
        );
    }

    protected override string GetConnectionString()
    {
        return $"Data Source={Guid.NewGuid()}.db";
    }

    protected override IDatabaseContainer? GetDbContainer() => null;
}
