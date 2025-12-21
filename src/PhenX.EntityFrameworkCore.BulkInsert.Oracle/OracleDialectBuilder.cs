using System.Text;

using Microsoft.EntityFrameworkCore;

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
        return CreateTableCopySqlBase(tempTableName, columns);
    }

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

        // Merge handling
        if (onConflict is OnConflictOptions<T> onConflictTyped)
        {
            // Oracle MERGE doesn't support returning entities
            if (returnedColumns.Count != 0)
            {
                throw new NotSupportedException("Oracle MERGE does not support returning entities. Use ExecuteBulkInsertAsync without returning results when using conflict resolution.");
            }

            IReadOnlyList<string> matchColumns;
            if (onConflictTyped.Match != null)
            {
                matchColumns = GetColumns(target, onConflictTyped.Match).ToList();
            }
            else if (target.PrimaryKey.Length > 0)
            {
                matchColumns = target.PrimaryKey.Select(x => x.QuotedColumName).ToList();
            }
            else
            {
                throw new InvalidOperationException("Table has no primary key that can be used for conflict detection.");
            }

            // Validate that all match columns are available in the source subquery
            var insertedColumnNames = insertedColumns.Select(c => c.QuotedColumName).ToHashSet();
            var missingMatchColumns = matchColumns.Where(c => !insertedColumnNames.Contains(c)).ToList();
            if (missingMatchColumns.Count != 0)
            {
                throw new InvalidOperationException(
                    $"Oracle MERGE requires match columns to be present in the source data. " +
                    $"The following match columns are not available: {string.Join(", ", missingMatchColumns)}. " +
                    $"This can happen when using auto-generated primary key columns for conflict detection. " +
                    $"Use the 'Match' option to specify non-generated columns for conflict detection, " +
                    $"or set 'CopyGeneratedColumns = true' if the generated column values are provided.");
            }

            // Oracle MERGE syntax does NOT use AS for table aliases
            q.AppendLine($"MERGE INTO {target.QuotedTableName} {PseudoTableInserted}");

            q.Append("USING (SELECT ");
            q.AppendColumns(insertedColumns);
            // Oracle MERGE syntax does NOT use AS for subquery aliases
            q.Append($" FROM {source}) {PseudoTableExcluded}");
            q.AppendLine();

            // Oracle requires ON clause conditions to be wrapped in parentheses
            q.Append("ON (");
            q.AppendJoin(" AND ", matchColumns, (b, col) => b.Append($"{PseudoTableInserted}.{col} = {PseudoTableExcluded}.{col}"));
            q.AppendLine(")");

            // Oracle MERGE syntax: WHEN NOT MATCHED clause for inserts, followed by WHEN MATCHED clause for updates
            q.Append("WHEN NOT MATCHED THEN INSERT (");
            q.AppendColumns(insertedColumns);
            q.AppendLine(")");

            q.Append("VALUES (");
            q.AppendJoin(", ", insertedColumns, (b, col) => b.Append($"{PseudoTableExcluded}.{col.QuotedColumName}"));
            q.AppendLine(")");

            if (onConflictTyped.Update != null)
            {
                q.Append("WHEN MATCHED ");

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
                // Oracle MERGE: columns in ON clause cannot be updated, so exclude match columns
                // Use insertedColumns instead of all columns because the USING subquery only contains insertedColumns
                var matchColumnSet = matchColumns.ToHashSet();
                var updateableColumns = insertedColumns.Where(c => !matchColumnSet.Contains(c.QuotedColumName)).ToList();
                if (updateableColumns.Count == 0)
                {
                    throw new InvalidOperationException(
                        "Oracle MERGE cannot update any columns because all available columns are used in the ON clause for conflict detection. " +
                        "Specify different columns in the 'Match' option or use specific columns in the 'Update' expression.");
                }
                q.AppendJoin(", ", GetUpdates(context, target, updateableColumns, onConflictTyped.Update));
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

        return q.ToString();
    }
}
