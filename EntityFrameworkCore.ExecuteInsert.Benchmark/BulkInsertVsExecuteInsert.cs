using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;

using EFCore.BulkExtensions;

using EntityFrameworkCore.ExecuteInsert.Abstractions;

using Microsoft.EntityFrameworkCore;

using Testcontainers.PostgreSql;

namespace EntityFrameworkCore.ExecuteInsert.Benchmark;

[MinColumn, MaxColumn, BaselineColumn]
[MemoryDiagnoser]
[SimpleJob(RunStrategy.Throughput, launchCount: 1, warmupCount: 0, iterationCount: 5)]
public class BulkInsertVsExecuteInsert
{
    [Params(100_000/*, 1_000_000/*, 10_000_000*/)]
    public int N;

    private IList<TestEntity> data;
    private TestDbContext DbContext;

    [GlobalSetup]
    public void GlobalSetup()
    {
        data = Enumerable.Range(1, N).Select(i => new TestEntity
        {
            Name = $"Entity{i}",
            Price = (decimal)(i * 0.1),
            Identifier = Guid.NewGuid(),
            StringEnumValue = (StringEnum)(i % 2),
            NumericEnumValue = (NumericEnum)(i % 2),
        }).ToList();
    }

    public BulkInsertVsExecuteInsert()
    {
        PostgresContainer = GetPostgresContainer();
        PostgresContainer.StartAsync().GetAwaiter().GetResult();

        var connectionString = PostgresContainer.GetConnectionString() + ";Include Error Detail=true";

        DbContext = new TestDbContext();
        DbContext.Database.SetConnectionString(connectionString);
        DbContext.Database.EnsureDeleted();
        DbContext.Database.EnsureCreated();
    }

    public PostgreSqlContainer PostgresContainer { get; }

    private static PostgreSqlContainer GetPostgresContainer()
    {
        return new PostgreSqlBuilder()
            .WithDatabase("testdb")
            .WithUsername("testuser")
            .WithPassword("testpassword")
            .Build();
    }

    [Benchmark(Baseline = true)]
    public async Task ExecuteInsert()
    {
        await DbContext.ExecuteInsertAsync(data);
    }

    [Benchmark]
    public async Task ExecuteInsertWithIdentity()
    {
        await DbContext.ExecuteInsertWithIdentityAsync(data);
    }

    [Benchmark]
    public async Task ExecuteInsertWithIdentityMoveRows()
    {
        await DbContext.ExecuteInsertWithIdentityAsync(data, options => options.MoveRows = true);
    }

    [Benchmark]
    public async Task BulkInsertZEf()
    {
        await DbContext.BulkInsertOptimizedAsync(data, options => options.IncludeGraph = false);
    }

    [Benchmark]
    public async Task BulkInsert()
    {
        await DbContextBulkExtensions.BulkInsertAsync(DbContext, data);
    }
}
