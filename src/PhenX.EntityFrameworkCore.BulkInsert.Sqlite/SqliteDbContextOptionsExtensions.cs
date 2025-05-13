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
    public static DbContextOptionsBuilder UseExecuteInsertSqlite(this DbContextOptionsBuilder optionsBuilder)
    {
        var extension = optionsBuilder.Options.FindExtension<ExecuteInsertOptionsExtension<SqliteBulkInsertProvider>>() ?? new ExecuteInsertOptionsExtension<SqliteBulkInsertProvider>();
        ((IDbContextOptionsBuilderInfrastructure)optionsBuilder).AddOrUpdateExtension(extension);
        return optionsBuilder;
    }
}

