using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

using PhenX.EntityFrameworkCore.BulkInsert.Extensions;

namespace PhenX.EntityFrameworkCore.BulkInsert.SqlServer;

/// <summary>
/// DbContext options extension for SQL Server.
/// </summary>
public static class SqlServerDbContextOptionsExtensions
{
    /// <summary>
    /// Configures the DbContext to use the SQL Server bulk insert provider.
    /// </summary>
    public static DbContextOptionsBuilder UseBulkInsertSqlServer(this DbContextOptionsBuilder optionsBuilder)
    {
        return optionsBuilder.UseProvider<SqlServerBulkInsertProvider>();
    }
}
