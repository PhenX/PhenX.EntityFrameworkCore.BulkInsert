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
    public override bool SupportsOutputInsertedIds => false;

    /// <inheritdoc />
    /// <summary>
    /// The temporary table name is generated with a random 8-character suffix to ensure uniqueness, and is limited to less than 30 characters,
    /// because Oracle prior to 12.2 has a limit of 30 characters for identifiers.
    /// </summary>
    protected override string GetTempTableName(string tableName) => $"#temp_bulk_insert_{Helpers.RandomString(8)}";

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

        bulkCopy.DestinationTableName = tableName;
        bulkCopy.BatchSize = options.BatchSize;
        bulkCopy.BulkCopyTimeout = options.GetCopyTimeoutInSeconds();

        // Handle progress notifications
        if (options is { NotifyProgressAfter: not null, OnProgress: not null })
        {
            bulkCopy.NotifyAfter = options.NotifyProgressAfter.Value;

            bulkCopy.OracleRowsCopied += (sender, e) =>
            {
                options.OnProgress(e.RowsCopied);

                if (ctk.IsCancellationRequested)
                {
                    e.Abort = true;
                }
            };
        }

        // If no progress notification is set, we still need to handle cancellation.
        else
        {
            bulkCopy.OracleRowsCopied += (sender, e) =>
            {
                if (ctk.IsCancellationRequested)
                {
                    e.Abort = true;
                }
            };
        }

        foreach (var column in columns)
        {
            bulkCopy.ColumnMappings.Add(column.PropertyName, column.QuotedColumName);
        }

        var dataReader = new EnumerableDataReader<T>(entities, columns, options);

        bulkCopy.WriteToServer(dataReader);

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    protected override async Task DropTempTableAsync(bool sync, DbContext dbContext, string tableName)
    {
        var commandText = $"""
                           BEGIN
                               EXECUTE IMMEDIATE 'DROP TABLE {tableName}';
                           EXCEPTION
                               WHEN OTHERS THEN
                                   IF SQLCODE != -942 THEN -- ORA-00942: table or view does not exist
                                       RAISE;
                                   END IF;
                           END;
                           """;

        await ExecuteAsync(sync, dbContext, commandText, CancellationToken.None);
    }
}
