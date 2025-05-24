namespace PhenX.EntityFrameworkCore.BulkInsert.Enums;

/// <summary>
/// Enumeration of supported database providers.
/// </summary>
public enum ProviderType
{
    /// <summary>
    /// SQL Server provider.
    /// </summary>
    SqlServer,

    /// <summary>
    /// PostgreSQL provider.
    /// </summary>
    PostgreSql,

    /// <summary>
    /// SQLite provider.
    /// </summary>
    Sqlite,

    /// <summary>
    /// MySQL provider.
    /// </summary>
    MySql,
}
