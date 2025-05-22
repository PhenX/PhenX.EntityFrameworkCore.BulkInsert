using Microsoft.EntityFrameworkCore;

using PhenX.EntityFrameworkCore.BulkInsert.Extensions;

namespace PhenX.EntityFrameworkCore.BulkInsert.Sqlite;

/// <summary>
/// DbContext options extension for SQLite.
/// </summary>
public static class SqliteDbContextOptionsExtensions
{
    /// <summary>
    /// Configures the DbContext to use the SQLite bulk insert provider.
    /// </summary>
    public static DbContextOptionsBuilder UseBulkInsertSqlite(this DbContextOptionsBuilder optionsBuilder)
    {
        return optionsBuilder.UseProvider<SqliteBulkInsertProvider>();
    }
}

