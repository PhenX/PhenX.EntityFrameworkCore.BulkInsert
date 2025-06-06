using System.Text;

using Microsoft.EntityFrameworkCore;

using PhenX.EntityFrameworkCore.BulkInsert.Dialect;
using PhenX.EntityFrameworkCore.BulkInsert.Metadata;
using PhenX.EntityFrameworkCore.BulkInsert.Options;

namespace PhenX.EntityFrameworkCore.BulkInsert.MySql;

internal class MySqlServerDialectBuilder : SqlDialectBuilder
{
    protected override string OpenDelimiter => "`";

    protected override string CloseDelimiter => "`";

    /// <inheritdoc />
    protected override bool SupportsMoveRows => false;

    /// <inheritdoc />
    protected override bool SupportsInsertIntoAlias => false;

    public override string CreateTableCopySql(string tempNameName, TableMetadata tableInfo, IReadOnlyList<ColumnMetadata> columns)
    {
        return $"CREATE TEMPORARY TABLE {tempNameName} SELECT * FROM {tableInfo.QuotedTableName} WHERE 1 = 0;";
    }

    protected override void AppendConflictCondition<T>(
        StringBuilder sql,
        TableMetadata target,
        DbContext context,
        OnConflictOptions<T> onConflictTyped)
    {
        throw new NotSupportedException("Conflict conditions are not supported in MYSQL");
    }

    protected override void AppendOnConflictUpdate(StringBuilder sql, IEnumerable<string> updates)
    {
        sql.AppendLine("UPDATE");

        var i = 0;
        foreach (var update in updates)
        {
            if (i > 0)
            {
                sql.Append(", ");
            }

            sql.Append(update);
            i++;
        }
    }

    protected override void AppendOnConflictStatement(StringBuilder sql)
    {
        sql.Append("ON DUPLICATE KEY");
    }

    protected override void AppendDoNothing(StringBuilder sql, IEnumerable<ColumnMetadata> insertedColumns)
    {
        var columnName = insertedColumns.First().ColumnName;

        sql.Append($"UPDATE {Quote(columnName)} = {GetExcludedColumnName(columnName)}");
    }

    protected override string GetExcludedColumnName(string columnName)
    {
        return $"VALUES({Quote(columnName)})";
    }
}
