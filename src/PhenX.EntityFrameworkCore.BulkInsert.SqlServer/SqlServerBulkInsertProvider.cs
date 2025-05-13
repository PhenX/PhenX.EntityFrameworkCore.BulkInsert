using JetBrains.Annotations;

using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

using PhenX.EntityFrameworkCore.BulkInsert.Options;

namespace PhenX.EntityFrameworkCore.BulkInsert.SqlServer;

[UsedImplicitly]
internal class SqlServerBulkInsertProvider : BulkInsertProviderBase<SqlServerDialectBuilder>
{
    //language=sql
    /// <inheritdoc />
    protected override string CreateTableCopySql => "SELECT {2} INTO {0} FROM {1} WHERE 1 = 0;";

    //language=sql
    /// <inheritdoc />
    protected override string AddTableCopyBulkInsertId => $"ALTER TABLE {{0}} ADD {BulkInsertId} INT IDENTITY PRIMARY KEY;";

    /// <inheritdoc />
    protected override string GetTempTableName(string tableName) => $"#_temp_bulk_insert_{tableName}";

    /// <inheritdoc />
    protected override async Task BulkInsert<T>(DbContext context, IEnumerable<T> entities,
        string tableName,
        PropertyAccessor[] properties, BulkInsertOptions options, CancellationToken ctk)
    {
        var connection = context.Database.GetDbConnection();
        var sqlTransaction = context.Database.CurrentTransaction!.GetDbTransaction() as SqlTransaction;

        using var bulkCopy = new SqlBulkCopy(connection as SqlConnection, SqlBulkCopyOptions.TableLock, sqlTransaction);
        bulkCopy.DestinationTableName = tableName;
        bulkCopy.BatchSize = options.BatchSize ?? 50_000;
        bulkCopy.BulkCopyTimeout = 60;

        foreach (var prop in properties)
        {
            bulkCopy.ColumnMappings.Add(prop.Name, SqlDialect.Quote(prop.ColumnName));
        }

        await bulkCopy.WriteToServerAsync(new EnumerableDataReader<T>(entities, properties), ctk);
    }
}
