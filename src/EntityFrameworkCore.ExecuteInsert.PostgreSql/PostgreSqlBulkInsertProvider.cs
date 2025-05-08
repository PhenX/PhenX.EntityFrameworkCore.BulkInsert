using System.Data.Common;
using System.Linq.Expressions;
using System.Text;

using EntityFrameworkCore.ExecuteInsert.OnConflict;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

using Npgsql;

namespace EntityFrameworkCore.ExecuteInsert.PostgreSql;

public class PostgreSqlBulkInsertProvider : BulkInsertProviderBase
{
    public override string OpenDelimiter => "\"";
    public override string CloseDelimiter => "\"";

    //language=sql
    protected override string CreateTableCopySql => "CREATE TEMPORARY TABLE {0} AS TABLE {1} WITH NO DATA;";

    //language=sql
    protected override string AddTableCopyBulkInsertId => $"ALTER TABLE {{0}} ADD COLUMN {BulkInsertId} SERIAL PRIMARY KEY;";

    private string GetBinaryImportCommand(DbContext context, Type entityType, string tableName)
    {
        var columns = GetEscapedColumns(context, entityType, false);

        return $"COPY {tableName} ({string.Join(", ", columns)}) FROM STDIN (FORMAT BINARY)";
    }

    protected override string BuildInsertSelectQuery<T>(string tableName,
        string targetTableName,
        IProperty[] insertedProperties,
        IProperty[] properties,
        BulkInsertOptions options, OnConflictOptions? onConflict = null)
    {
        var insertedColumns = insertedProperties.Select(p => Escape(p.GetColumnName()));
        var insertedColumnList = string.Join(", ", insertedColumns);

        var returnedColumns = properties.Select(p => $"{Escape(p.GetColumnName())} AS {Escape(p.Name)}");
        var columnList = string.Join(", ", returnedColumns);

        var q = new StringBuilder();

        if (options.MoveRows)
        {
            q.AppendLine($"""
                    WITH moved_rows AS (
                       DELETE FROM {tableName}
                           RETURNING {insertedColumnList}
                    )
                    """);
            tableName = "moved_rows";
        }

        q.AppendLine($"""
                      INSERT INTO {targetTableName} ({insertedColumnList})
                      SELECT {insertedColumnList}
                      FROM {tableName}
                      """);

        if (onConflict is OnConflictOptions<T> onConflictTyped)
        {
            q.AppendLine("ON CONFLICT");

            if (onConflictTyped.Update != null)
            {
                if (onConflictTyped.Match != null)
                {
                    q.AppendLine($"({string.Join(", ", GetColumns(onConflictTyped.Match).Select(Escape))})");
                }

                if (onConflictTyped.Update != null)
                {
                    q.AppendLine($"DO UPDATE SET {string.Join(", ", GetUpdates(onConflictTyped.Update))}");
                }

                if (onConflictTyped.Condition != null)
                {
                    q.AppendLine($"WHERE {onConflictTyped.Condition}");
                }
            }
            else
            {
                q.AppendLine("DO NOTHING");
            }
        }

        if (columnList.Length != 0)
        {
            q.AppendLine($"RETURNING {columnList}");
        }

        q.AppendLine(";");

        return q.ToString();
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
