using System.Text;

using JetBrains.Annotations;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using Npgsql;
using Npgsql.EntityFrameworkCore.PostgreSQL.Storage.Internal.Mapping;

using NpgsqlTypes;

using PhenX.EntityFrameworkCore.BulkInsert.Metadata;
using PhenX.EntityFrameworkCore.BulkInsert.Options;

namespace PhenX.EntityFrameworkCore.BulkInsert.PostgreSql;

[UsedImplicitly]
internal class PostgreSqlBulkInsertProvider(ILogger<PostgreSqlBulkInsertProvider>? logger) : BulkInsertProviderBase<PostgreSqlDialectBuilder, BulkInsertOptions>(logger)
{
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

        // The type mapping can be null for obvious types like string.
        var columnTypes = columns.Select(GetPostgreSqlType).ToArray();

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

            var columnIndex = 0;
            foreach (var column in columns)
            {
                var value = column.GetValue(entity);

                // Get the actual type, so that the writer can do the conversation to the target type automatically.
                var type = columnTypes[columnIndex];

                if (sync)
                {
                    if (type != null)
                    {
                        // ReSharper disable once MethodHasAsyncOverloadWithCancellation
                        writer.Write(value, type.Value);
                    }
                    else
                    {
                        // ReSharper disable once MethodHasAsyncOverloadWithCancellation
                        writer.Write(value);
                    }
                }
                else
                {
                    if (type != null)
                    {
                        await writer.WriteAsync(value, type.Value, ctk);
                    }
                    else
                    {
                        await writer.WriteAsync(value, ctk);
                    }
                }

                columnIndex++;
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

    private static NpgsqlDbType? GetPostgreSqlType(ColumnMetadata column)
    {
        var mapping = column.Property.GetRelationalTypeMapping() as NpgsqlTypeMapping;

        return mapping?.NpgsqlDbType;
    }
}
