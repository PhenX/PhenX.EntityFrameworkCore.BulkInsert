using JetBrains.Annotations;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using Oracle.ManagedDataAccess.Client;

using PhenX.EntityFrameworkCore.BulkInsert.Metadata;
using PhenX.EntityFrameworkCore.BulkInsert.Options;

namespace PhenX.EntityFrameworkCore.BulkInsert.Oracle;

[UsedImplicitly]
internal class OracleBulkInsertProvider(ILogger<OracleBulkInsertProvider>? logger) : BulkInsertProviderBase<OracleDialectBuilder, OracleBulkInsertOptions>(logger)
{
    /// <inheritdoc />
    protected override string BulkInsertId => "ROWID";

    /// <inheritdoc />
    protected override string AddTableCopyBulkInsertId => ""; // No need to add an ID column in Oracle

    /// <inheritdoc />
    protected override string GetTempTableName(string tableName) => $"#temp_bulk_insert_{Guid.NewGuid().ToString("N")[..8]}";

    protected override OracleBulkInsertOptions CreateDefaultOptions() => new()
    {
        BatchSize = 50_000,
    };

    /// <inheritdoc />
    protected override IAsyncEnumerable<T> BulkInsertReturnEntities<T>(
        bool sync,
        DbContext context,
        TableMetadata tableInfo,
        IEnumerable<T> entities,
        OracleBulkInsertOptions options,
        OnConflictOptions<T>? onConflict,
        CancellationToken ctk)
    {
        throw new NotSupportedException("Provider does not support returning entities.");
    }

    /// <inheritdoc />
    protected override Task BulkInsert<T>(
        bool sync,
        DbContext context,
        TableMetadata tableInfo,
        IEnumerable<T> entities,
        string tableName,
        IReadOnlyList<ColumnMetadata> columns,
        OracleBulkInsertOptions options,
        CancellationToken ctk)
    {
        var connection = (OracleConnection) context.Database.GetDbConnection();

        using var bulkCopy = new OracleBulkCopy(connection, options.CopyOptions);

        bulkCopy.DestinationTableName = tableInfo.QuotedTableName;
        bulkCopy.BatchSize = options.BatchSize;
        bulkCopy.BulkCopyTimeout = options.GetCopyTimeoutInSeconds();

        foreach (var column in columns)
        {
            bulkCopy.ColumnMappings.Add(column.PropertyName, column.QuotedColumName);
        }

        var dataReader = new EnumerableDataReader<T>(entities, columns, options);

        bulkCopy.WriteToServer(dataReader);

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    protected override Task DropTempTableAsync(bool sync, DbContext dbContext, string tableName)
    {
        return ExecuteAsync(sync, dbContext, $"DROP TABLE IF EXISTS {tableName}", default);
    }
}
