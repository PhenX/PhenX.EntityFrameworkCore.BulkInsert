using System.Text;

using PhenX.EntityFrameworkCore.BulkInsert.Dialect;
using PhenX.EntityFrameworkCore.BulkInsert.Metadata;
using PhenX.EntityFrameworkCore.BulkInsert.Options;

namespace PhenX.EntityFrameworkCore.BulkInsert.PostgreSql;

internal class PostgreSqlDialectBuilder : SqlDialectBuilder
{
    protected override string OpenDelimiter => "\"";
    protected override string CloseDelimiter => "\"";

    public override string CreateTableCopySql(string tempNameName, TableMetadata tableInfo, IReadOnlyList<ColumnMetadata> columns)
    {
        return $"CREATE TEMPORARY TABLE {tempNameName} AS TABLE {tableInfo.QuotedTableName} WITH NO DATA;";
    }

    protected override void AppendConflictMatch<T>(StringBuilder sql, TableMetadata target, OnConflictOptions<T> conflict)
    {
        if (conflict.Match != null)
        {
            base.AppendConflictMatch(sql, target, conflict);
        }
        else if (target.PrimaryKey.Length > 0)
        {
            sql.Append(' ');
            sql.AppendLine("(");
            sql.AppendColumns(target.PrimaryKey);
            sql.AppendLine(")");
        }
        else
        {
            throw new InvalidOperationException("Table has no primary key that can be used for conflict detection.");
        }
    }
}
