using System.Text;

using Microsoft.EntityFrameworkCore;

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

    public override string CreateTableCopySql(string tempTableName, TableMetadata tableInfo, IReadOnlyList<ColumnMetadata> columns)
    {
        return CreateTableCopySqlBase(tempTableName, columns);
    }

    protected override string Trim(string lhs) => $"TRIM({lhs})";

    public override string BuildMoveDataSql<T>(
        DbContext context,
        TableMetadata target,
        string source,
        IReadOnlyList<ColumnMetadata> insertedColumns,
        IReadOnlyList<ColumnMetadata> returnedColumns,
        BulkInsertOptions options,
        OnConflictOptions? onConflict = null)
    {
        var q = new StringBuilder();

        var identityInsert = options.CopyGeneratedColumns && insertedColumns.Any(x => x.IsGenerated);
        if (identityInsert)
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
            else if (target.PrimaryKey.Length > 0)
            {
                matchColumns = target.PrimaryKey.Select(x => x.QuotedColumName);
            }
            else
            {
                throw new InvalidOperationException("Table has no primary key that can be used for conflict detection.");
            }

            q.AppendLine($"MERGE INTO {target.QuotedTableName} AS {PseudoTableInserted}");

            q.Append("USING (SELECT ");
            q.AppendColumns(insertedColumns);
            q.Append($" FROM {source}) AS {PseudoTableExcluded} (");
            q.AppendColumns(insertedColumns);
            q.AppendLine(")");

            q.Append("ON ");
            q.AppendJoin(" AND ", matchColumns, (b, col) => b.Append($"{PseudoTableInserted}.{col} = {PseudoTableExcluded}.{col}"));
            q.AppendLine();

            if (onConflictTyped.Update != null)
            {
                var columns = target.GetColumns(false);

                q.AppendLine("WHEN MATCHED ");

                if (onConflictTyped.RawWhere != null || onConflictTyped.Where != null)
                {
                    if (onConflictTyped is { RawWhere: not null, Where: not null })
                    {
                        throw new ArgumentException("Cannot specify both RawWhere and Where in OnConflictOptions.");
                    }

                    q.Append("AND ");
                    AppendConflictCondition(q, target, context, onConflictTyped);
                }

                q.AppendLine("THEN UPDATE SET ");
                q.AppendJoin(", ", GetUpdates(context, target, columns, onConflictTyped.Update));
                q.AppendLine();
            }

            q.Append("WHEN NOT MATCHED THEN INSERT (");
            q.AppendColumns(insertedColumns);
            q.AppendLine(")");

            q.Append("VALUES (");
            q.AppendJoin(", ", insertedColumns, (b, col) => b.Append($"{PseudoTableExcluded}.{col.QuotedColumName}"));
            q.AppendLine(")");

            if (returnedColumns.Count != 0)
            {
                q.Append("OUTPUT ");
                q.AppendJoin(", ", returnedColumns, (b, col) => b.Append($"{PseudoTableInserted}.{col.QuotedColumName} AS {col.QuotedColumName}"));
                q.AppendLine();
            }
        }

        // No conflict handling
        else
        {
            q.Append($"INSERT INTO {target.QuotedTableName} (");
            q.AppendColumns(insertedColumns);
            q.AppendLine(")");

            if (returnedColumns.Count != 0)
            {
                q.Append("OUTPUT ");
                q.AppendJoin(", ", returnedColumns, (b, col) => b.Append($"{PseudoTableInserted}.{col.QuotedColumName} AS {col.QuotedColumName}"));
                q.AppendLine();
            }

            q.Append("SELECT ");
            q.AppendColumns(insertedColumns);
            q.AppendLine();
            q.Append($"FROM {source}");
            q.AppendLine();
        }

        q.AppendLine(";");

        if (identityInsert)
        {
            q.AppendLine($"SET IDENTITY_INSERT {target.QuotedTableName} OFF;");
        }

        return q.ToString();
    }
}
