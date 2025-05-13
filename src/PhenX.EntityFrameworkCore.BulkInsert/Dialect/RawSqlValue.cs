namespace PhenX.EntityFrameworkCore.BulkInsert.Dialect;

/// <summary>
/// Represents a raw SQL value.
/// </summary>
/// <param name="sql"></param>
public class RawSqlValue(string sql)
{
    /// <summary>
    /// The raw SQL value.
    /// </summary>
    public string Sql { get; } = sql;
}
