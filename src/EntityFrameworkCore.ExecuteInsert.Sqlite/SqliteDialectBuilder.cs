namespace EntityFrameworkCore.ExecuteInsert.Sqlite;

public class SqliteDialectBuilder : SqlDialectBuilder
{
    protected override string OpenDelimiter => "\"";
    protected override string CloseDelimiter => "\"";

    protected override bool SupportsMoveRows => false;
}
