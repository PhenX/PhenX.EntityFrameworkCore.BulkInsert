namespace EntityFrameworkCore.ExecuteInsert;

public class RawSqlValue(string sql)
{
    public string Sql { get; } = sql;
}
