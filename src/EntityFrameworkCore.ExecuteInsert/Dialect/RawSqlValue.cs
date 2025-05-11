namespace EntityFrameworkCore.ExecuteInsert.Dialect;

/// <summary>
/// Represents a raw SQL value.
/// </summary>
/// <param name="sql"></param>
public class RawSqlValue(string sql)
{
    public string Sql { get; } = sql;
}
