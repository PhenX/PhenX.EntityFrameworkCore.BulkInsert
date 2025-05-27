using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;

using DotNet.Testcontainers.Containers;

using EFCore.BulkExtensions;

using LinqToDB.Data;
using LinqToDB.EntityFrameworkCore;

using PhenX.EntityFrameworkCore.BulkInsert.Extensions;

namespace PhenX.EntityFrameworkCore.BulkInsert.Benchmark;

[MemoryDiagnoser]
[SimpleJob(RunStrategy.ColdStart, launchCount: 1, warmupCount: 0, iterationCount: 5)]
public abstract partial class LibComparator
{
    [Params(500_000/*, 1_000_000/*, 10_000_000*/)]
    public int N;

    private IList<TestEntity> data = [];
    protected TestDbContext DbContext { get; set; } = null!;

    [IterationSetup]
    public void IterationSetup()
    {
        data = Enumerable.Range(1, N).Select(i => new TestEntity
        {
            Name = $"Entity{i}",
            Price = (decimal)(i * 0.1),
            Identifier = Guid.NewGuid(),
            NumericEnumValue = (NumericEnum)(i % 2),
        }).ToList();

        ConfigureDbContext();
        DbContext.Database.EnsureCreated();
    }

    protected LibComparator()
    {
        DbContainer = GetDbContainer();
        DbContainer?.StartAsync().GetAwaiter().GetResult();
        LinqToDBForEFTools.Initialize();
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
        await DbContext.ExecuteBulkInsertAsync(data);
    }
    //
    // [Benchmark]
    // public void PhenX_EntityFrameworkCore_BulkInsert_Sync()
    // {
    //     DbContext.ExecuteBulkInsert(data);
    // }

    [Benchmark]
    public void RawInsert()
    {
        if (DbContext.Database.ProviderName!.Contains("SqlServer", StringComparison.InvariantCultureIgnoreCase))
        {
            // Use SqlBulkCopy for SQL Server
            RawInsertSqlServer();
        }
        else if (DbContext.Database.ProviderName!.Contains("Sqlite", StringComparison.InvariantCultureIgnoreCase))
        {
            // Use raw sql insert statements for SQLite
            RawInsertSqlite();
        }
        else if (DbContext.Database.ProviderName!.Contains("Npgsql", StringComparison.InvariantCultureIgnoreCase))
        {
            // Use BeginBinaryImport for PostgreSQL
            RawInsertPostgreSql();
        }
        else if (DbContext.Database.ProviderName!.Contains("MySql", StringComparison.InvariantCultureIgnoreCase))
        {
            // Use MySqlBulkCopy for PostgreSQL
            RawInsertMySql();
        }
    }

    [Benchmark]
    public async Task Linq2Db()
    {
        await DbContext.BulkCopyAsync(new BulkCopyOptions
        {
            BulkCopyType = BulkCopyType.ProviderSpecific,
        }, data);
    }

    [Benchmark]
    public async Task Z_EntityFramework_Extensions_EFCore()
    {
        await DbContext.BulkInsertOptimizedAsync(data, options => options.IncludeGraph = false);
    }

    // [Benchmark]
    // public void Z_EntityFramework_Extensions_EFCore_Sync()
    // {
    //     DbContext.BulkInsertOptimized(data, options => options.IncludeGraph = false);
    // }

    [Benchmark]
    public async Task EFCore_BulkExtensions()
    {
        await DbContext.BulkInsertAsync(data, options =>
        {
            options.IncludeGraph = false;
            options.PreserveInsertOrder = false;
        });
    }

    // [Benchmark]
    // public void EFCore_BulkExtensions_Sync()
    // {
    //     DbContext.BulkInsert(data, options =>
    //     {
    //         options.IncludeGraph = false;
    //         options.PreserveInsertOrder = false;
    //     });
    // }

    [Benchmark]
    public async Task EFCore_SaveChanges()
    {
        DbContext.AddRange(data);
        await DbContext.SaveChangesAsync();
    }
}
