using BenchmarkDotNet.Attributes;

using DotNet.Testcontainers.Containers;

using EFCore.BulkExtensions;

using EntityFrameworkCore.ExecuteInsert.Abstractions;

namespace EntityFrameworkCore.ExecuteInsert.Benchmark;

public abstract class BulkInsertVsExecuteInsert
{
    [Params(100_000/*, 1_000_000/*, 10_000_000*/)]
    public int N;

    private IList<TestEntity> data;
    protected TestDbContext DbContext;

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
        DbContainer = GetDbContainer();
        DbContainer?.StartAsync().GetAwaiter().GetResult();

        ConfigureDbContext();

        DbContext.Database.EnsureCreated();
    }

    protected abstract void ConfigureDbContext();

    protected virtual string GetConnectionString()
    {
        return DbContainer?.GetConnectionString() ?? string.Empty;
    }

    public IDatabaseContainer? DbContainer { get; }

    protected abstract IDatabaseContainer? GetDbContainer();

    [Benchmark(Baseline = true)]
    public async Task ExecuteInsert()
    {
        await DbContext.ExecuteInsertAsync(data);
    }
    //
    // [Benchmark]
    // public async Task ExecuteInsertWithIdentity()
    // {
    //     await DbContext.ExecuteInsertWithIdentityAsync(data);
    // }
    //
    // [Benchmark]
    // public async Task ExecuteInsertWithIdentityMoveRows()
    // {
    //     await DbContext.ExecuteInsertWithIdentityAsync(data, options => options.MoveRows = true);
    // }

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
