using JetBrains.Annotations;

using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;

using PhenX.EntityFrameworkCore.BulkInsert.Metadata;
using PhenX.EntityFrameworkCore.BulkInsert.Options;

namespace PhenX.EntityFrameworkCore.BulkInsert.SqlServer;

[UsedImplicitly]
internal class SqlServerBulkInsertProvider : BulkInsertProviderBase<SqlServerDialectBuilder>
{
    public SqlServerBulkInsertProvider(ILogger<SqlServerBulkInsertProvider>? logger = null) : base(logger)
    {
    }

    //language=sql
    /// <inheritdoc />
    protected override string CreateTableCopySql => "SELECT {2} INTO {0} FROM {1} WHERE 1 = 0;";

    //language=sql
    /// <inheritdoc />
    protected override string AddTableCopyBulkInsertId => $"ALTER TABLE {{0}} ADD {BulkInsertId} INT IDENTITY PRIMARY KEY;";

    /// <inheritdoc />
    protected override string GetTempTableName(string tableName) => $"#_temp_bulk_insert_{tableName}";

    /// <inheritdoc />
    protected override async Task BulkInsert<T>(
        bool sync,
        DbContext context,
        TableMetadata tableInfo,
        IEnumerable<T> entities,
        string tableName,
        IReadOnlyList<PropertyMetadata> properties,
        BulkInsertOptions options,
        CancellationToken ctk)
    {
        var connection = (SqlConnection) context.Database.GetDbConnection();
        var sqlTransaction = context.Database.CurrentTransaction!.GetDbTransaction() as SqlTransaction;

        using var bulkCopy = new SqlBulkCopy(connection, SqlBulkCopyOptions.TableLock, sqlTransaction);
        bulkCopy.DestinationTableName = tableName;
        bulkCopy.BatchSize = options.BatchSize ?? 50_000;
        bulkCopy.BulkCopyTimeout = 60;

        foreach (var prop in properties)
        {
            bulkCopy.ColumnMappings.Add(prop.Name, SqlDialect.Quote(prop.ColumnName));
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
