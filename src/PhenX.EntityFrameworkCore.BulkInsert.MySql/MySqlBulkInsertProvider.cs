using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;

using MySqlConnector;

using PhenX.EntityFrameworkCore.BulkInsert.Options;

namespace PhenX.EntityFrameworkCore.BulkInsert.MySql;

internal class MySqlBulkInsertProvider : BulkInsertProviderBase<MySqlServerDialectBuilder, MySqlBulkInsertOptions>
{
    public MySqlBulkInsertProvider(ILogger<MySqlBulkInsertProvider>? logger = null) : base(logger)
    {
    }

    //language=sql
    /// <inheritdoc />
    protected override string CreateTableCopySql => "CREATE TEMPORARY TABLE {0} SELECT * FROM {1} WHERE 1 = 0;";

    //language=sql
    /// <inheritdoc />
    protected override string AddTableCopyBulkInsertId => $"ALTER TABLE {{0}} ADD {BulkInsertId} INT AUTO_INCREMENT PRIMARY KEY;";

    /// <inheritdoc />
    protected override string GetTempTableName(string tableName) => $"#_temp_bulk_insert_{tableName}";

    /// <inheritdoc />
    protected override MySqlBulkInsertOptions GetDefaultOptions() => new();

    /// <inheritdoc />
    public override Task<List<T>> BulkInsertReturnEntities<T>(
        bool sync,
        DbContext context,
        IEnumerable<T> entities,
        BulkInsertOptions options,
        OnConflictOptions? onConflict = null,
        CancellationToken ctk = default)
    {
        throw new NotSupportedException("Provider does not support returning entities.");
    }

    /// <inheritdoc />
    protected override async Task BulkInsert<T>(
        bool sync,
        DbContext context,
        IEnumerable<T> entities,
        string tableName,
        PropertyAccessor[] properties,
        MySqlBulkInsertOptions options,
        CancellationToken ctk
    )
    {
        var connection = (MySqlConnection)context.Database.GetDbConnection();

        var sqlTransaction = context.Database.CurrentTransaction?.GetDbTransaction()
            ?? throw new InvalidOperationException("No open transaction found.");

        if (sqlTransaction is not MySqlTransaction mySqlTransaction)
        {
            throw new InvalidOperationException($"Invalid transaction foud, got {sqlTransaction.GetType()}.");
        }

        var bulkCopy = new MySqlBulkCopy(connection, mySqlTransaction)
        {
            DestinationTableName = tableName,
            BulkCopyTimeout = options.GetCopyTimeoutInSeconds(),
        };

        var sourceOrdinal = 0;
        foreach (var prop in properties)
        {
            bulkCopy.ColumnMappings.Add(new MySqlBulkCopyColumnMapping(sourceOrdinal, prop.ColumnName));
            sourceOrdinal++;
        }

        if (sync)
        {
            // ReSharper disable once MethodHasAsyncOverloadWithCancellation
            bulkCopy.WriteToServer(new EnumerableDataReader<T>(entities, properties));
        }
        else
        {
            await bulkCopy.WriteToServerAsync(new EnumerableDataReader<T>(entities, properties), ctk);
        }
    }
}
