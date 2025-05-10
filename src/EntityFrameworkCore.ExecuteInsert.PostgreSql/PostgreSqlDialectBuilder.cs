using EntityFrameworkCore.ExecuteInsert.Dialect;

namespace EntityFrameworkCore.ExecuteInsert.PostgreSql;

public class PostgreSqlDialectBuilder : SqlDialectBuilder
{
    protected override string OpenDelimiter => "\"";
    protected override string CloseDelimiter => "\"";
}
