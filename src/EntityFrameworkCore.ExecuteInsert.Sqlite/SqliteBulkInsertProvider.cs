using System.Data.Common;
using System.Linq.Expressions;
using System.Text;
using EntityFrameworkCore.ExecuteInsert.OnConflict;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Data.Sqlite;

namespace EntityFrameworkCore.ExecuteInsert.Sqlite;

public class SqliteBulkInsertProvider : BulkInsertProviderBase
{
    public override string OpenDelimiter => "\"";
    public override string CloseDelimiter => "\"";

    protected override string BulkInsertId => "rowid";

    protected override string CreateTableCopySql => "CREATE TEMP TABLE {0} AS SELECT * FROM {1} WHERE 0;";
    protected override string AddTableCopyBulkInsertId => "--"; // No need to add an ID column in SQLite

    protected override Task AddBulkInsertIdColumn<T>(DbConnection connection, CancellationToken cancellationToken,
        string tempTableName) where T : class
    {
        return Task.CompletedTask;
    }

    private string GetInsertCommand(DbContext context, Type entityType, string tableName)
    {
        var columns = GetEscapedColumns(context, entityType, false);
        var placeholders = string.Join(", ", columns.Select((_, i) => $"@p{i}"));

        return $"INSERT INTO {tableName} ({string.Join(", ", columns)}) VALUES ({placeholders})";
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
        await using var transaction = await connection.BeginTransactionAsync(ctk);

        var insertCommand = GetInsertCommand(context, typeof(T), tableName);
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = insertCommand;

        for (var index = 0; index < properties.Length; index++)
        {
            var param = cmd.CreateParameter();
            param.ParameterName = $"@p{index}";

            cmd.Parameters.Add(param);
        }

        foreach (var entity in entities)
        {
            for (var index = 0; index < properties.Length; index++)
            {
                var value = properties[index].GetValue(entity);
                cmd.Parameters[index].Value = value;
            }

            await cmd.ExecuteNonQueryAsync(ctk);
        }
        await transaction.CommitAsync(ctk);
    }
}

