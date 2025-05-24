using JetBrains.Annotations;

using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;

using PhenX.EntityFrameworkCore.BulkInsert.Metadata;

namespace PhenX.EntityFrameworkCore.BulkInsert.SqlServer;

[UsedImplicitly]
internal class SqlServerBulkInsertProvider(ILogger<SqlServerBulkInsertProvider>? logger) : BulkInsertProviderBase<SqlServerDialectBuilder, SqlServerBulkInsertOptions>(logger)
{
    //language=sql
    /// <inheritdoc />
    protected override string AddTableCopyBulkInsertId => $"ALTER TABLE {{0}} ADD {BulkInsertId} INT IDENTITY PRIMARY KEY;";

    /// <inheritdoc />
    protected override string GetTempTableName(string tableName) => $"#_temp_bulk_insert_{tableName}";

    protected override SqlServerBulkInsertOptions CreateDefaultOptions() => new()
    {
        BatchSize = 50_000,
    };

    /// <inheritdoc />
    protected override async Task BulkInsert<T>(
        bool sync,
        DbContext context,
        TableMetadata tableInfo,
        IEnumerable<T> entities,
        string tableName,
        IReadOnlyList<ColumnMetadata> columns,
        SqlServerBulkInsertOptions options,
        CancellationToken ctk)
    {
        var connection = (SqlConnection) context.Database.GetDbConnection();
        var sqlTransaction = context.Database.CurrentTransaction!.GetDbTransaction() as SqlTransaction;

        using var bulkCopy = new SqlBulkCopy(connection, options.CopyOptions, sqlTransaction);

        bulkCopy.DestinationTableName = tableName;
        bulkCopy.BatchSize = options.BatchSize;
        bulkCopy.BulkCopyTimeout = options.GetCopyTimeoutInSeconds();
        bulkCopy.EnableStreaming = options.EnableStreaming;

        foreach (var column in columns)
        {
            bulkCopy.ColumnMappings.Add(column.PropertyName, column.ColumnName);
        }

        if (sync)
        {
            // ReSharper disable once MethodHasAsyncOverloadWithCancellation
            bulkCopy.WriteToServer(new EnumerableDataReader<T>(entities, columns));
        }
        else
        {
            await bulkCopy.WriteToServerAsync(new EnumerableDataReader<T>(entities, columns), ctk);
        }
    }
}
