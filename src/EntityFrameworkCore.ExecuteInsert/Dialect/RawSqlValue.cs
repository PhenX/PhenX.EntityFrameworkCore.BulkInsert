namespace EntityFrameworkCore.ExecuteInsert.Dialect;

public class RawSqlValue(string sql)
{
    public string Sql { get; } = sql;
}
