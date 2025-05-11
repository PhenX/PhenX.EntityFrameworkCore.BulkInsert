using EntityFrameworkCore.ExecuteInsert.Options;

using JetBrains.Annotations;

using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace EntityFrameworkCore.ExecuteInsert.SqlServer;

[UsedImplicitly]
public class SqlServerBulkInsertProvider : BulkInsertProviderBase<SqlServerDialectBuilder>
{
    //language=sql
    protected override string CreateTableCopySql => "SELECT {2} INTO {0} FROM {1} WHERE 1 = 0;";

    //language=sql
    protected override string AddTableCopyBulkInsertId => $"ALTER TABLE {{0}} ADD {BulkInsertId} INT IDENTITY PRIMARY KEY;";

    protected override string GetTempTableName(string tableName) => $"#_temp_bulk_insert_{tableName}";

    protected override async Task BulkInsert<T>(DbContext context, IEnumerable<T> entities,
        string tableName,
        PropertyAccessor[] properties, BulkInsertOptions options, CancellationToken ctk)
    {
        var connection = context.Database.GetDbConnection();

        await using var t = (SqlTransaction) await connection.BeginTransactionAsync(ctk); // TODO option

        using var bulkCopy = new SqlBulkCopy(connection as SqlConnection, SqlBulkCopyOptions.TableLock, t);
        bulkCopy.DestinationTableName = tableName;
        bulkCopy.BatchSize = options.BatchSize ?? 50_000;
        bulkCopy.BulkCopyTimeout = 60;

        foreach (var prop in properties)
        {
            bulkCopy.ColumnMappings.Add(prop.Name, SqlDialect.Quote(prop.ColumnName));
        }

        await bulkCopy.WriteToServerAsync(new EnumerableDataReader<T>(entities, properties), ctk);

        await t.CommitAsync(ctk);
    }
}
