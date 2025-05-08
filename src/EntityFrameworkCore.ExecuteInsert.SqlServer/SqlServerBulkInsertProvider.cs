using System.Data.Common;
using System.Linq.Expressions;
using System.Text;

using EntityFrameworkCore.ExecuteInsert.Helpers;
using EntityFrameworkCore.ExecuteInsert.OnConflict;

using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace EntityFrameworkCore.ExecuteInsert.SqlServer;

public class SqlServerBulkInsertProvider : BulkInsertProviderBase
{
    public override string OpenDelimiter => "[";
    public override string CloseDelimiter => "]";

    //language=sql
    protected override string CreateTableCopySql => "SELECT {2} INTO {0} FROM {1} WHERE 1 = 0;";

    //language=sql
    protected override string AddTableCopyBulkInsertId => $"ALTER TABLE {{0}} ADD {BulkInsertId} INT IDENTITY PRIMARY KEY;";

    protected override string GetTempTableName(string tableName) => $"#_temp_bulk_insert_{tableName}";

    protected override async Task BulkImport<T>(DbContext context, DbConnection connection, IEnumerable<T> entities, string tableName,
        PropertyAccessor[] properties, CancellationToken ctk)
    {
        await using var t = (SqlTransaction) await connection.BeginTransactionAsync(ctk); // TODO option

        using var bulkCopy = new SqlBulkCopy(connection as SqlConnection, SqlBulkCopyOptions.TableLock, t);
        bulkCopy.DestinationTableName = tableName;
        bulkCopy.BatchSize = 50_000; // TODO option
        bulkCopy.BulkCopyTimeout = 60;

        foreach (var prop in properties)
        {
            bulkCopy.ColumnMappings.Add(prop.Name, DatabaseHelper.GetEscapedColumnName(prop.Name, OpenDelimiter, CloseDelimiter));
        }

        await bulkCopy.WriteToServerAsync(new EnumerableDataReader<T>(entities, properties), ctk);

        await t.CommitAsync(ctk);
    }

    protected override string BuildInsertSelectQuery<T>(string tableName,
        string targetTableName,
        IProperty[] insertedProperties,
        IProperty[] properties,
        BulkInsertOptions options, OnConflictOptions? onConflict = null)
    {
        var insertedColumns = insertedProperties.Select(p => Escape(p.GetColumnName())).ToArray();
        var insertedColumnList = string.Join(", ", insertedColumns);

        var returnedColumns = properties.Select(p => $"INSERTED.{p.GetColumnName()} AS [{p.Name}]");
        var columnList = string.Join(", ", returnedColumns);

        var q = new StringBuilder();

//         if (options.MoveRows)
//         {
//             var deletedColumnList = string.Join(", ", insertedColumns.Select(c => $"DELETED.{c}"));
//
//             q.AppendLine($"""
//                 DELETE FROM {tableName}
//                 OUTPUT {deletedColumnList}
//                 """);
//         }

        // Merge handling
        if (onConflict is OnConflictOptions<T> onConflictTyped && onConflictTyped.Match != null)
        {
            var matchColumns = GetColumns(onConflictTyped.Match);
            var matchOn = string.Join(" AND ",
                matchColumns.Select(col => $"TARGET.{Escape(col)} = SOURCE.{Escape(col)}"));

            var updateSet = onConflictTyped.Update != null
                ? string.Join(", ", GetUpdates(onConflictTyped.Update))
                : null;

            q.AppendLine($"MERGE INTO {targetTableName} AS TARGET");
            q.AppendLine(
                $"USING (SELECT {string.Join(", ", insertedColumns)} FROM {tableName}) AS SOURCE ({insertedColumnList})");
            q.AppendLine($"ON {matchOn}");

            if (updateSet != null)
            {
                q.AppendLine($"WHEN MATCHED THEN UPDATE SET {updateSet}");
            }

            q.AppendLine(
                $"WHEN NOT MATCHED THEN INSERT ({insertedColumnList}) VALUES ({string.Join(", ", insertedColumns.Select(c => $"SOURCE.{c}"))})");

            if (columnList.Length != 0)
            {
                q.AppendLine($"OUTPUT {columnList}");
            }
        }

        // No conflict handling
        else
        {
            q.AppendLine($"INSERT INTO {targetTableName} ({insertedColumnList})");

            if (columnList.Length != 0)
            {
                q.AppendLine($"OUTPUT {columnList}");
            }

            q.AppendLine($"""
                          SELECT {insertedColumnList}
                          FROM {tableName}
                          """);
        }

        q.AppendLine(";");

        return q.ToString();
    }

    protected override string GetExcludedColumnName(MemberExpression member)
    {
        var prefix = "SOURCE";
        return $"{prefix}.{Escape(member.Member.Name)}";
    }

    protected override string ConcatOperator => "+";
}
