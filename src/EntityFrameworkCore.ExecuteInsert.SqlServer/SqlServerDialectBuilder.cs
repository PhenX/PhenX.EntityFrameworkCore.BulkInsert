using System.Linq.Expressions;
using System.Text;

using EntityFrameworkCore.ExecuteInsert.Dialect;
using EntityFrameworkCore.ExecuteInsert.Options;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace EntityFrameworkCore.ExecuteInsert.SqlServer;

public class SqlServerDialectBuilder : SqlDialectBuilder
{
    protected override string OpenDelimiter => "[";
    protected override string CloseDelimiter => "]";

    public override string BuildMoveDataSql<T>(string source,
        string target,
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

            q.AppendLine($"MERGE INTO {target} AS TARGET");
            q.AppendLine(
                $"USING (SELECT {string.Join(", ", insertedColumns)} FROM {source}) AS SOURCE ({insertedColumnList})");
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
            q.AppendLine($"INSERT INTO {target} ({insertedColumnList})");

            if (columnList.Length != 0)
            {
                q.AppendLine($"OUTPUT {columnList}");
            }

            q.AppendLine($"""
                          SELECT {insertedColumnList}
                          FROM {source}
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
