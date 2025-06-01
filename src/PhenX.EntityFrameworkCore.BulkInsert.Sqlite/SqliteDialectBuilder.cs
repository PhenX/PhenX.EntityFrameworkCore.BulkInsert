using PhenX.EntityFrameworkCore.BulkInsert.Dialect;
using PhenX.EntityFrameworkCore.BulkInsert.Metadata;

namespace PhenX.EntityFrameworkCore.BulkInsert.Sqlite;

internal class SqliteDialectBuilder : SqlDialectBuilder
{
    protected override string OpenDelimiter => "\"";
    protected override string CloseDelimiter => "\"";

    protected override bool SupportsMoveRows => false;

    /// <inheritdoc />
    public override string CreateTableCopySql(string tempNameName, TableMetadata tableInfo, IReadOnlyList<ColumnMetadata> columns)
    {
        return $"CREATE TEMP TABLE {tempNameName} AS SELECT * FROM {tableInfo.QuotedTableName} WHERE 0;";
    }

    protected override string Trim(string lhs) => $"TRIM({lhs})";
}
