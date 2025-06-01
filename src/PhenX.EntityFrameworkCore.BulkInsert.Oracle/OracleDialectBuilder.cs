using System.Text;

using PhenX.EntityFrameworkCore.BulkInsert.Dialect;
using PhenX.EntityFrameworkCore.BulkInsert.Metadata;
using PhenX.EntityFrameworkCore.BulkInsert.Options;

namespace PhenX.EntityFrameworkCore.BulkInsert.Oracle;

internal class OracleDialectBuilder : SqlDialectBuilder
{
    protected override string OpenDelimiter => "\"";
    protected override string CloseDelimiter => "\"";
    protected override string ConcatOperator => "||";

    protected override bool SupportsMoveRows => false;

    public override string CreateTableCopySql(string tempTableName, TableMetadata tableInfo, IReadOnlyList<ColumnMetadata> columns)
    {
        var q = new StringBuilder();
        q.Append($"CREATE TABLE {tempTableName} (");

        foreach (var column in columns)
        {
            q.Append($"{column.QuotedColumName} {column.StoreDefinition}");
            if (column != columns[^1])
            {
                q.Append(',');
            }
            q.AppendLine();
        }

        q.AppendLine(")");

        return q.ToString();
    }

    public override string BuildMoveDataSql<T>(
        TableMetadata target,
        string source,
        IReadOnlyList<ColumnMetadata> insertedColumns,
        IReadOnlyList<ColumnMetadata> returnedColumns,
        BulkInsertOptions options,
        OnConflictOptions? onConflict = null)
    {
        var q = new StringBuilder();

        // Merge handling
        if (onConflict is OnConflictOptions<T> onConflictTyped)
        {
            IEnumerable<string> matchColumns;
            if (onConflictTyped.Match != null)
            {
                matchColumns = GetColumns(target, onConflictTyped.Match);
            }
            else if (target.PrimaryKey.Count > 0)
            {
                matchColumns = target.PrimaryKey.Select(x => x.QuotedColumName);
            }
            else
            {
                throw new InvalidOperationException("Table has no primary key that can be used for conflict detection.");
            }

            q.AppendLine($"MERGE INTO {target.QuotedTableName} AS TARGET");

            q.Append("USING (SELECT ");
            q.AppendColumns(insertedColumns);
            q.Append($" FROM {source}) AS SOURCE (");
            q.AppendColumns(insertedColumns);
            q.AppendLine(")");

            q.Append("ON ");
            q.AppendJoin(" AND ", matchColumns, (b, col) => b.Append($"TARGET.{col} = SOURCE.{col}"));
            q.AppendLine();

            if (onConflictTyped.Update != null)
            {
                var columns = target.GetColumns(false);

                q.AppendLine("WHEN MATCHED THEN UPDATE SET ");
                q.AppendJoin(", ", GetUpdates(target, columns, onConflictTyped.Update));
                q.AppendLine();
            }

            q.Append("WHEN NOT MATCHED THEN INSERT (");
            q.AppendColumns(insertedColumns);
            q.AppendLine(")");

            q.Append("VALUES (");
            q.AppendJoin(", ", insertedColumns, (b, col) => b.Append($"SOURCE.{col.QuotedColumName}"));
            q.AppendLine(")");

            if (returnedColumns.Count != 0)
            {
                q.Append("OUTPUT ");
                q.AppendJoin(", ", returnedColumns, (b, col) => b.Append($"INSERTED.{col.QuotedColumName} AS {col.QuotedColumName}"));
                q.AppendLine();
            }
        }

        // No conflict handling
        else
        {
            q.Append($"INSERT INTO {target.QuotedTableName} (");
            q.AppendColumns(insertedColumns);
            q.AppendLine(")");
            q.Append("SELECT ");
            q.AppendColumns(insertedColumns);
            q.AppendLine();
            q.Append($"FROM {source}");
            q.AppendLine();

            if (returnedColumns.Count != 0)
            {
                q.Append("RETURNING ");
                q.AppendJoin(", ", returnedColumns, (b, col) => b.Append(col.QuotedColumName));
                q.Append(" INTO ");
                q.AppendJoin(", ", returnedColumns, (b, col) => b.Append($":{col.ColumnName}"));
                q.AppendLine();
            }
        }

        q.AppendLine(";");

        var result = q.ToString();
        return result;
    }

    protected override string GetExcludedColumnName(string columnName)
    {
        return $"SOURCE.{columnName}";
    }
}
