using JetBrains.Annotations;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using Npgsql;

using PhenX.EntityFrameworkCore.BulkInsert.Options;

namespace PhenX.EntityFrameworkCore.BulkInsert.PostgreSql;

[UsedImplicitly]
internal class PostgreSqlBulkInsertProvider : BulkInsertProviderBase<PostgreSqlDialectBuilder>
{
    public PostgreSqlBulkInsertProvider(ILogger<PostgreSqlBulkInsertProvider>? logger = null) : base(logger)
    {
    }

    //language=sql
    /// <inheritdoc />
    protected override string CreateTableCopySql => "CREATE TEMPORARY TABLE {0} AS TABLE {1} WITH NO DATA;";

    //language=sql
    /// <inheritdoc />
    protected override string AddTableCopyBulkInsertId => $"ALTER TABLE {{0}} ADD COLUMN {BulkInsertId} SERIAL PRIMARY KEY;";

    private string GetBinaryImportCommand(DbContext context, Type entityType, string tableName)
    {
        var columns = GetQuotedColumns(context, entityType, false);

        return $"COPY {tableName} ({string.Join(", ", columns)}) FROM STDIN (FORMAT BINARY)";
    }

    /// <inheritdoc />
    protected override async Task BulkInsert<T>(
        bool sync,
        DbContext context,
        IEnumerable<T> entities,
        string tableName,
        PropertyAccessor[] properties,
        BulkInsertOptions options,
        CancellationToken ctk) where T : class
    {
        var connection = (NpgsqlConnection)context.Database.GetDbConnection();

        var importCommand = GetBinaryImportCommand(context, typeof(T), tableName);

        var writer = sync
            // ReSharper disable once MethodHasAsyncOverloadWithCancellation
            ? connection.BeginBinaryImport(importCommand)
            : await connection.BeginBinaryImportAsync(importCommand, ctk);

        foreach (var entity in entities)
        {
            if (sync)
            {
                // ReSharper disable once MethodHasAsyncOverloadWithCancellation
                writer.StartRow();
            }
            else
            {
                await writer.StartRowAsync(ctk);
            }

            foreach (var property in properties)
            {
                var value = property.GetValue(entity);

                if (sync)
                {
                    // ReSharper disable once MethodHasAsyncOverloadWithCancellation
                    writer.Write(value);
                }
                else
                {
                    await writer.WriteAsync(value, ctk);
                }
            }
        }

        if (sync)
        {
            // ReSharper disable once MethodHasAsyncOverloadWithCancellation
            writer.Complete();
            // ReSharper disable once MethodHasAsyncOverload
            writer.Dispose();
        }
        else
        {
            await writer.CompleteAsync(ctk);
            await writer.DisposeAsync();
        }

    }
}
