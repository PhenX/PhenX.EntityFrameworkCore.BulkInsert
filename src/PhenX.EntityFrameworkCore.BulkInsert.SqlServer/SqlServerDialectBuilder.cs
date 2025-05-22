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

    public override string CreateTableCopySql(string templNameName, TableMetadata tableInfo, IReadOnlyList<PropertyMetadata> columns)
    {
        var q = new StringBuilder();
        q.Append("SELECT");
        q.AppendColumns(columns);
        q.Append($"INTO {templNameName} FROM {tableInfo.QuotedTableName} WHERE 1 = 0;");

        return q.ToString();
    }

    public override string BuildMoveDataSql<T>(
        TableMetadata target,
        string source,
        IReadOnlyList<PropertyMetadata> insertedProperties,
        IReadOnlyList<PropertyMetadata> returnedProperties,
        BulkInsertOptions options,
        OnConflictOptions? onConflict = null)
    {
        var q = new StringBuilder();

        if (options.CopyGeneratedColumns)
        {
            q.AppendLine($"SET IDENTITY_INSERT {target.QuotedTableName} ON;");
        }

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
            q.AppendColumns(insertedProperties);
            q.Append($" FROM {source}) AS SOURCE (");
            q.AppendColumns(insertedProperties);
            q.AppendLine(")");

            q.Append("ON ");
            q.AppendJoin($" AND ", matchColumns, (b, col) => b.Append($"TARGET.{col} = SOURCE.{col}"));
            q.AppendLine();

            if (onConflictTyped.Update != null)
            {
                q.AppendLine($"WHEN MATCHED THEN UPDATE SET ");
                q.AppendJoin(", ", GetUpdates(target, insertedProperties, onConflictTyped.Update));
                q.AppendLine();
            }

            q.Append($"WHEN NOT MATCHED THEN INSERT (");
            q.AppendColumns(insertedProperties);
            q.AppendLine(")");

            q.Append("VALUES (");
            q.AppendJoin(", ", insertedProperties, (b, col) => b.Append($"SOURCE.{col.QuotedColumName}"));
            q.AppendLine(")");

            if (returnedProperties.Count != 0)
            {
                q.Append("OUTPUT ");
                q.AppendJoin($", ", returnedProperties, (b, col) => b.Append($"INSERTED.{col.QuotedColumName} AS {col.QuotedColumName}"));
                q.AppendLine();
            }
        }

        // No conflict handling
        else
        {
            q.Append($"INSERT INTO {target.QuotedTableName} (");
            q.AppendColumns(insertedProperties);
            q.AppendLine(")");

            if (returnedProperties.Count != 0)
            {
                q.Append("OUTPUT ");
                q.AppendJoin($", ", returnedProperties, (b, col) => b.Append($"INSERTED.{col.QuotedColumName} AS {col.QuotedColumName}"));
                q.AppendLine();
            }

            q.Append("SELECT ");
            q.AppendColumns(insertedProperties);
            q.AppendLine();
            q.Append($"FROM {source}");
            q.AppendLine();
        }

        q.AppendLine(";");

        if (options.CopyGeneratedColumns)
        {
            q.AppendLine($"SET IDENTITY_INSERT {target.QuotedTableName} OFF;");
        }

        var x = q.ToString();
        return x;
    }

    protected override string GetExcludedColumnName(string columnName)
    {
        return $"SOURCE.{columnName}";
    }
}
