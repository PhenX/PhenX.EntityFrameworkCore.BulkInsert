using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

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
        var extension = optionsBuilder.Options.FindExtension<BulkInsertOptionsExtension<SqliteBulkInsertProvider>>() ?? new BulkInsertOptionsExtension<SqliteBulkInsertProvider>();
        ((IDbContextOptionsBuilderInfrastructure)optionsBuilder).AddOrUpdateExtension(extension);
        return optionsBuilder;
    }
}

