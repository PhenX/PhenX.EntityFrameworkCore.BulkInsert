using System.Text;

using JetBrains.Annotations;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using Npgsql;

using PhenX.EntityFrameworkCore.BulkInsert.Metadata;
using PhenX.EntityFrameworkCore.BulkInsert.Options;

namespace PhenX.EntityFrameworkCore.BulkInsert.PostgreSql;

[UsedImplicitly]
internal class PostgreSqlBulkInsertProvider : BulkInsertProviderBase<PostgreSqlDialectBuilder, BulkInsertOptions>
{
    public PostgreSqlBulkInsertProvider(ILogger<PostgreSqlBulkInsertProvider>? logger = null) : base(logger)
    {
    }

    //language=sql
    /// <inheritdoc />
    protected override string AddTableCopyBulkInsertId => $"ALTER TABLE {{0}} ADD COLUMN {BulkInsertId} SERIAL PRIMARY KEY;";

    private static string GetBinaryImportCommand(IReadOnlyList<ColumnMetadata> properties, string tableName)
    {
        var sql = new StringBuilder();
        sql.Append($"COPY {tableName} (");
        sql.AppendColumns(properties);
        sql.Append(") FROM STDIN (FORMAT BINARY)");
        return sql.ToString();
    }

    /// <inheritdoc />
    protected override BulkInsertOptions CreateDefaultOptions() => new()
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
        BulkInsertOptions options,
        CancellationToken ctk)
    {
        var connection = (NpgsqlConnection)context.Database.GetDbConnection();
        var command = GetBinaryImportCommand(columns, tableName);

        var writer = sync
            // ReSharper disable once MethodHasAsyncOverloadWithCancellation
            ? connection.BeginBinaryImport(command)
            : await connection.BeginBinaryImportAsync(command, ctk);

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

            foreach (var column in columns)
            {
                var value = column.GetValue(entity);

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
