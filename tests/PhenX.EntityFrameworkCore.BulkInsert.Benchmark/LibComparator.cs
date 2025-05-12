using BenchmarkDotNet.Attributes;

using DotNet.Testcontainers.Containers;

using EFCore.BulkExtensions;

using PhenX.EntityFrameworkCore.BulkInsert.Extensions;

namespace PhenX.EntityFrameworkCore.BulkInsert.Benchmark;

public abstract class LibComparator
{
    [Params(100_000/*, 1_000_000/*, 10_000_000*/)]
    public int N;

    private IList<TestEntity> data = [];
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

    protected LibComparator()
    {
        DbContainer = GetDbContainer();
        DbContainer?.StartAsync().GetAwaiter().GetResult();

        ConfigureDbContext();

        DbContext!.Database.EnsureCreated();
    }

    protected abstract void ConfigureDbContext();

    protected virtual string GetConnectionString()
    {
        return DbContainer?.GetConnectionString() ?? string.Empty;
    }

    private IDatabaseContainer? DbContainer { get; }

    protected abstract IDatabaseContainer? GetDbContainer();

    [Benchmark(Baseline = true)]
    public async Task PhenX_EntityFrameworkCore_BulkInsert()
    {
        await DbContext.ExecuteInsertAsync(data);
    }
    //
    // [Benchmark]
    // public async Task PhenX_EntityFrameworkCore_BulkInsertWithIdentity()
    // {
    //     await DbContext.ExecuteInsertWithIdentityAsync(data);
    // }
    //
    // [Benchmark]
    // public async Task PhenX_EntityFrameworkCore_BulkInsertWithIdentityMoveRows()
    // {
    //     await DbContext.ExecuteInsertWithIdentityAsync(data, options => options.MoveRows = true);
    // }

    [Benchmark]
    public async Task Z_EntityFramework_Extensions_EFCore()
    {
        await DbContext.BulkInsertOptimizedAsync(data, options => options.IncludeGraph = false);
    }

    [Benchmark]
    public async Task EFCore_BulkExtensions_MIT()
    {
        await DbContext.BulkInsertAsync(data, options =>
        {
            options.IncludeGraph = false;
            options.PreserveInsertOrder = false;
        });
    }

    [Benchmark]
    public async Task EFCore_SaveChanges()
    {
        DbContext.ChangeTracker.Clear();
        DbContext.AddRange(data);
        await DbContext.SaveChangesAsync();
    }
}
