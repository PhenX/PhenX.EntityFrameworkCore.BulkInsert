using BenchmarkDotNet.Attributes;

using DotNet.Testcontainers.Containers;

using EFCore.BulkExtensions;

using LinqToDB.EntityFrameworkCore;

using Microsoft.Data.SqlClient;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

using MySqlConnector;

using Npgsql;

using PhenX.EntityFrameworkCore.BulkInsert.Extensions;

namespace PhenX.EntityFrameworkCore.BulkInsert.Benchmark;

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
            StringEnumValue = (StringEnum)(i % 2),
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
        await DbContext.BulkCopyAsync(data);
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

    // [Benchmark]
    // public async Task EFCore_SaveChanges()
    // {
    //     DbContext.AddRange(data);
    //     await DbContext.SaveChangesAsync();
    // }

    private void RawInsertMySql()
    {
        var connection = (MySqlConnection)DbContext.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
        {
            connection.Open();
        }

        var bulkCopy = new MySqlBulkCopy(connection);

        bulkCopy.DestinationTableName = nameof(TestEntity);
        bulkCopy.BulkCopyTimeout = 60;

        bulkCopy.ColumnMappings.Add(new MySqlBulkCopyColumnMapping(0, "Name"));
        bulkCopy.ColumnMappings.Add(new MySqlBulkCopyColumnMapping(1, "Price"));
        bulkCopy.ColumnMappings.Add(new MySqlBulkCopyColumnMapping(2, "Identifier"));
        bulkCopy.ColumnMappings.Add(new MySqlBulkCopyColumnMapping(3, "CreatedAt"));
        bulkCopy.ColumnMappings.Add(new MySqlBulkCopyColumnMapping(4, "UpdatedAt"));
        bulkCopy.ColumnMappings.Add(new MySqlBulkCopyColumnMapping(5, "StringEnumValue"));
        bulkCopy.ColumnMappings.Add(new MySqlBulkCopyColumnMapping(6, "NumericEnumValue"));

        var dataTable = new DataTable();
        dataTable.Columns.Add("Name", typeof(string));
        dataTable.Columns.Add("Price", typeof(decimal));
        dataTable.Columns.Add("Identifier", typeof(Guid));
        dataTable.Columns.Add("CreatedAt", typeof(DateTime));
        dataTable.Columns.Add("UpdatedAt", typeof(DateTimeOffset));
        dataTable.Columns.Add("StringEnumValue", typeof(string));
        dataTable.Columns.Add("NumericEnumValue", typeof(int));

        foreach (var entity in data)
        {
            var row = dataTable.NewRow();
            row["Name"] = entity.Name;
            row["Price"] = entity.Price;
            row["Identifier"] = entity.Identifier;
            row["CreatedAt"] = entity.CreatedAt;
            row["UpdatedAt"] = entity.UpdatedAt;
            row["StringEnumValue"] = entity.StringEnumValue.ToString();
            row["NumericEnumValue"] = (int)entity.NumericEnumValue;
            dataTable.Rows.Add(row);

            if (dataTable.Rows.Count >= 50_000)
            {
                bulkCopy.WriteToServer(dataTable);
                dataTable.Clear();
            }
        }

        if (dataTable.Rows.Count > 0)
        {
            bulkCopy.WriteToServer(dataTable);
        }
    }
}
