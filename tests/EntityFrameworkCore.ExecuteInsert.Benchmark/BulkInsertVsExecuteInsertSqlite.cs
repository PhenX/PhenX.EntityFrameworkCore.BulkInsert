using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;

using DotNet.Testcontainers.Containers;

using EntityFrameworkCore.ExecuteInsert.Sqlite;

using Microsoft.EntityFrameworkCore;

namespace EntityFrameworkCore.ExecuteInsert.Benchmark;

[MinColumn, MaxColumn, BaselineColumn]
[MemoryDiagnoser]
[SimpleJob(RunStrategy.Throughput, launchCount: 1, warmupCount: 0, iterationCount: 5)]
public class BulkInsertVsExecuteInsertSqlite : BulkInsertVsExecuteInsert
{
    protected override void ConfigureDbContext()
    {
        var connectionString = GetConnectionString();

        DbContext = new TestDbContext(p => p
            .UseSqlite(connectionString)
            .UseExecuteInsertSqlite()
        );
    }

    protected override string GetConnectionString()
    {
        return $"Data Source={Guid.NewGuid()}.db";
    }

    protected override IDatabaseContainer? GetDbContainer() => null;
}
