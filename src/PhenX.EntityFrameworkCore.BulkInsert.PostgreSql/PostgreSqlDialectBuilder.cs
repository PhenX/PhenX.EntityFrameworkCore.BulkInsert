using PhenX.EntityFrameworkCore.BulkInsert.Dialect;

namespace PhenX.EntityFrameworkCore.BulkInsert.PostgreSql;

internal class PostgreSqlDialectBuilder : SqlDialectBuilder
{
    protected override string OpenDelimiter => "\"";
    protected override string CloseDelimiter => "\"";
}
