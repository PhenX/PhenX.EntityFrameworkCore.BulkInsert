using System.Data.Common;

using Microsoft.EntityFrameworkCore;

using Npgsql;

namespace EntityFrameworkCore.ExecuteInsert.PostgreSql;

public class PostgreSqlBulkInsertProvider : BulkInsertProviderBase
{
    public override string OpenDelimiter => "\"";
    public override string CloseDelimiter => "\"";

    //language=sql
    protected override string CreateTableCopySql => "CREATE TEMPORARY TABLE {0} AS TABLE {1} WITH NO DATA;";

    //language=sql
    protected override string AddTableCopyBulkInsertId => "ALTER TABLE {0} ADD COLUMN _bulk_insert_id SERIAL PRIMARY KEY;";

    private string GetBinaryImportCommand(DbContext context, Type entityType, string tableName)
    {
        var columns = GetEscapedColumns(context, entityType, false);

        return $"COPY {tableName} ({string.Join(", ", columns)}) FROM STDIN (FORMAT BINARY)";
    }

    protected override async Task BulkImport<T>(DbContext context, DbConnection connection, IEnumerable<T> entities,
        string tableName, PropertyAccessor[] properties, CancellationToken ctk) where T : class
    {
        var importCommand = GetBinaryImportCommand(context, typeof(T), tableName);

        await using (var writer = await ((NpgsqlConnection)connection).BeginBinaryImportAsync(importCommand, ctk))
        {
            foreach (var entity in entities)
            {
                await writer.StartRowAsync(ctk);

                foreach (var property in properties)
                {
                    var value = property.GetValue(entity);

                    await writer.WriteAsync(value, ctk);
                }
            }

            await writer.CompleteAsync(ctk);
        }
    }
}
