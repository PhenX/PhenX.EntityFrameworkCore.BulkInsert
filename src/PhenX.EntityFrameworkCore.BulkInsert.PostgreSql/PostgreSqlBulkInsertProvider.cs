using JetBrains.Annotations;

using Microsoft.EntityFrameworkCore;

using Npgsql;

using PhenX.EntityFrameworkCore.BulkInsert.Options;

namespace PhenX.EntityFrameworkCore.BulkInsert.PostgreSql;

[UsedImplicitly]
public class PostgreSqlBulkInsertProvider : BulkInsertProviderBase<PostgreSqlDialectBuilder>
{
    //language=sql
    protected override string CreateTableCopySql => "CREATE TEMPORARY TABLE {0} AS TABLE {1} WITH NO DATA;";

    //language=sql
    protected override string AddTableCopyBulkInsertId => $"ALTER TABLE {{0}} ADD COLUMN {BulkInsertId} SERIAL PRIMARY KEY;";

    private string GetBinaryImportCommand(DbContext context, Type entityType, string tableName)
    {
        var columns = GetQuotedColumns(context, entityType, false);

        return $"COPY {tableName} ({string.Join(", ", columns)}) FROM STDIN (FORMAT BINARY)";
    }

    protected override async Task BulkInsert<T>(
        DbContext context,
        IEnumerable<T> entities,
        string tableName,
        PropertyAccessor[] properties,
        BulkInsertOptions options,
        CancellationToken ctk) where T : class
    {
        var connection = (NpgsqlConnection)context.Database.GetDbConnection();

        var importCommand = GetBinaryImportCommand(context, typeof(T), tableName);

        await using var writer = await connection.BeginBinaryImportAsync(importCommand, ctk);

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
