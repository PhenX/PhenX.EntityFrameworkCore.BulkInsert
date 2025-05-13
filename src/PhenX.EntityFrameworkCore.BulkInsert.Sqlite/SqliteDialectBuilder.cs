using PhenX.EntityFrameworkCore.BulkInsert.Dialect;

namespace PhenX.EntityFrameworkCore.BulkInsert.Sqlite;

internal class SqliteDialectBuilder : SqlDialectBuilder
{
    protected override string OpenDelimiter => "\"";
    protected override string CloseDelimiter => "\"";

    protected override bool SupportsMoveRows => false;
}
