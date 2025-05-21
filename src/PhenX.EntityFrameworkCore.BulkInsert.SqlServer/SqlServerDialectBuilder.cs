using System.Text;

using PhenX.EntityFrameworkCore.BulkInsert.Dialect;
using PhenX.EntityFrameworkCore.BulkInsert.Metadata;
using PhenX.EntityFrameworkCore.BulkInsert.Options;

namespace PhenX.EntityFrameworkCore.BulkInsert.SqlServer;

internal class SqlServerDialectBuilder : SqlDialectBuilder
{
    protected override string OpenDelimiter => "[";
    protected override string CloseDelimiter => "]";
    protected override string ConcatOperator => "+";

    protected override bool SupportsMoveRows => false;

    public override string BuildMoveDataSql<T>(
        TableMetadata source,
        string target,
        IReadOnlyList<PropertyMetadata> insertedProperties,
        IReadOnlyList<PropertyMetadata> properties,
        BulkInsertOptions options,
        OnConflictOptions? onConflict = null)
    {
        var insertedColumns = insertedProperties.Select(x => x.QuotedColumName);
        var insertedColumnList = string.Join(", ", insertedColumns);

        var returnedColumns = properties.Select(p => $"INSERTED.{p.ColumnName} AS {p.ColumnName}");
        var columnList = string.Join(", ", returnedColumns);

        var q = new StringBuilder();

        if (options.CopyGeneratedColumns)
        {
            q.AppendLine($"SET IDENTITY_INSERT {target} ON;");
        }

        // Merge handling
        if (onConflict is OnConflictOptions<T> onConflictTyped && onConflictTyped.Match != null)
        {
            var matchColumns = GetColumns(source, onConflictTyped.Match);
            var matchOn = string.Join(" AND ",
                matchColumns.Select(col => $"TARGET.{col} = SOURCE.{col}"));

            var updateSet = onConflictTyped.Update != null
                ? string.Join(", ", GetUpdates(source, insertedProperties, onConflictTyped.Update))
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

        if (options.CopyGeneratedColumns)
        {
            q.AppendLine($"SET IDENTITY_INSERT {target} OFF;");
        }

        return q.ToString();
    }

    protected override string GetExcludedColumnName(string columnName)
    {
        return $"SOURCE.{columnName}";
    }
}
