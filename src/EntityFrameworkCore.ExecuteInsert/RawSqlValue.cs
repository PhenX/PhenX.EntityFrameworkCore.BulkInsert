namespace EntityFrameworkCore.ExecuteInsert;

public class RawSqlValue
{
    public string Sql { get; }

    public RawSqlValue(string sql)
    {
        Sql = sql;
    }
}
