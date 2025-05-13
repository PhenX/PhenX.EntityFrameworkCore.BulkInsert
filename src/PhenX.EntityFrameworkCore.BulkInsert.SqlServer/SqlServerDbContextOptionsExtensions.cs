using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace PhenX.EntityFrameworkCore.BulkInsert.SqlServer;

/// <summary>
/// DbContext options extension for SQL Server.
/// </summary>
public static class SqlServerDbContextOptionsExtensions
{
    /// <summary>
    /// Configures the DbContext to use the SQL Server bulk insert provider.
    /// </summary>
    public static DbContextOptionsBuilder UseExecuteInsertSqlServer(this DbContextOptionsBuilder optionsBuilder)
    {
        var extension = optionsBuilder.Options.FindExtension<ExecuteInsertOptionsExtension<SqlServerBulkInsertProvider>>() ?? new ExecuteInsertOptionsExtension<SqlServerBulkInsertProvider>();

        ((IDbContextOptionsBuilderInfrastructure)optionsBuilder).AddOrUpdateExtension(extension);

        return optionsBuilder;
    }
}
