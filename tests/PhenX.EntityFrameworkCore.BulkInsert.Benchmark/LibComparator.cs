using System.Data;

using BenchmarkDotNet.Attributes;

using DotNet.Testcontainers.Containers;

using EFCore.BulkExtensions;

using Microsoft.Data.SqlClient;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

using Npgsql;

using PhenX.EntityFrameworkCore.BulkInsert.Extensions;

namespace PhenX.EntityFrameworkCore.BulkInsert.Benchmark;

public abstract class LibComparator
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

    private void RawInsertPostgreSql()
    {
        using var connection = (NpgsqlConnection)DbContext.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
        {
            connection.Open();
        }

        const string copyCommand = $"""
                                    COPY "{nameof(TestEntity)}" (
                                        "Name",
                                        "Price",
                                        "Identifier",
                                        "CreatedAt",
                                        "UpdatedAt",
                                        "StringEnumValue",
                                        "NumericEnumValue"
                                    ) FROM STDIN (FORMAT BINARY)
                                    """;

        using var writer = connection.BeginBinaryImport(copyCommand);
        foreach (var entity in data)
        {
            writer.StartRow();
            writer.Write(entity.Name);
            writer.Write(entity.Price);
            writer.Write(entity.Identifier);
            writer.Write(entity.CreatedAt);
            writer.Write(entity.UpdatedAt);
            writer.Write(entity.StringEnumValue.ToString());
            writer.Write((int)entity.NumericEnumValue);
        }

        writer.Complete();
    }

    private void RawInsertSqlite()
    {
        var connection = (SqliteConnection)DbContext.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
        {
            connection.Open();
        }
        using var transaction = connection.BeginTransaction();
        using var command = connection.CreateCommand();
        command.CommandText = $"""
                               INSERT INTO "{nameof(TestEntity)}" (
                                   "Name",
                                   "Price",
                                   "Identifier",
                                   "CreatedAt",
                                   "UpdatedAt",
                                   "StringEnumValue",
                                   "NumericEnumValue"
                               ) VALUES (@Name, @Price, @Identifier, @CreatedAt, @UpdatedAt, @StringEnumValue, @NumericEnumValue)
                               """;

        command.Parameters.Add(new SqliteParameter("@Name", DbType.String));
        command.Parameters.Add(new SqliteParameter("@Price", DbType.Decimal));
        command.Parameters.Add(new SqliteParameter("@Identifier", DbType.Guid));
        command.Parameters.Add(new SqliteParameter("@CreatedAt", DbType.DateTime));
        command.Parameters.Add(new SqliteParameter("@UpdatedAt", DbType.DateTime2));
        command.Parameters.Add(new SqliteParameter("@StringEnumValue", DbType.String));
        command.Parameters.Add(new SqliteParameter("@NumericEnumValue", DbType.Int32));

        foreach (var entity in data)
        {
            command.Parameters["@Name"].Value = entity.Name;
            command.Parameters["@Price"].Value = entity.Price;
            command.Parameters["@Identifier"].Value = entity.Identifier;
            command.Parameters["@CreatedAt"].Value = entity.CreatedAt;
            command.Parameters["@UpdatedAt"].Value = entity.UpdatedAt;
            command.Parameters["@StringEnumValue"].Value = entity.StringEnumValue.ToString();
            command.Parameters["@NumericEnumValue"].Value = (int)entity.NumericEnumValue;

            command.ExecuteNonQuery();
        }

        transaction.Commit();
    }

    private void RawInsertSqlServer()
    {
        var connection = (SqlConnection)DbContext.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
        {
            connection.Open();
        }

        using var bulkCopy = new SqlBulkCopy(connection);

        bulkCopy.DestinationTableName = nameof(TestEntity);
        bulkCopy.BatchSize = 50_000;
        bulkCopy.BulkCopyTimeout = 60;

        bulkCopy.ColumnMappings.Add("Name", "Name");
        bulkCopy.ColumnMappings.Add("Price", "Price");
        bulkCopy.ColumnMappings.Add("Identifier", "Identifier");
        bulkCopy.ColumnMappings.Add("CreatedAt", "CreatedAt");
        bulkCopy.ColumnMappings.Add("UpdatedAt", "UpdatedAt");
        bulkCopy.ColumnMappings.Add("StringEnumValue", "StringEnumValue");
        bulkCopy.ColumnMappings.Add("NumericEnumValue", "NumericEnumValue");

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
