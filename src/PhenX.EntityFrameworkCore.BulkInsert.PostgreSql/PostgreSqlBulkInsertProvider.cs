using System.Text;

using JetBrains.Annotations;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using Npgsql;

using PhenX.EntityFrameworkCore.BulkInsert.Metadata;
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
    protected override string AddTableCopyBulkInsertId => $"ALTER TABLE {{0}} ADD COLUMN {BulkInsertId} SERIAL PRIMARY KEY;";

    /// <inheritdoc />
    protected override string CreateTableCopySql(string tempNameName, TableMetadata tableInfo, IReadOnlyList<PropertyMetadata> columns)
    {
        return $"CREATE TEMPORARY TABLE {tempNameName} AS TABLE {tableInfo.QuotedTableName} WITH NO DATA;";
    }

    private static string GetBinaryImportCommand(TableMetadata tableInfo, string tableName)
    {
        var columns = tableInfo.GetProperties(false).Select(X => X.QuotedColumName);

        var sql = new StringBuilder();
        sql.Append($"COPY {tableName} (");
        sql.AppendColumns(tableInfo.GetProperties(false));
        sql.Append(") FROM STDIN (FORMAT BINARY)");
        return sql.ToString();
    }

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
        var connection = (NpgsqlConnection)context.Database.GetDbConnection();

        var importCommand = GetBinaryImportCommand(tableInfo, tableName);

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
