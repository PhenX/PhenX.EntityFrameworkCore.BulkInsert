using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;

using DotNet.Testcontainers.Containers;

using EntityFrameworkCore.ExecuteInsert.SqlServer;

using Microsoft.EntityFrameworkCore;

using Testcontainers.MsSql;

namespace EntityFrameworkCore.ExecuteInsert.Benchmark;

[MinColumn, MaxColumn, BaselineColumn]
[MemoryDiagnoser]
[SimpleJob(RunStrategy.Throughput, launchCount: 1, warmupCount: 0, iterationCount: 5)]
public class BulkInsertVsExecuteInsertSqlServer : BulkInsertVsExecuteInsert
{
    protected override void ConfigureDbContext()
    {
        var connectionString = GetConnectionString();

        DbContext = new TestDbContext(p => p
            .UseSqlServer(connectionString)
            .UseExecuteInsertSqlServer()
        );
    }

    protected override IDatabaseContainer? GetDbContainer()
    {
        return new MsSqlBuilder().Build();
    }
}
