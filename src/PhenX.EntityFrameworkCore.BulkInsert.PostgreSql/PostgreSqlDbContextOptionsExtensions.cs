using Microsoft.EntityFrameworkCore;

using PhenX.EntityFrameworkCore.BulkInsert.Extensions;

namespace PhenX.EntityFrameworkCore.BulkInsert.PostgreSql;

/// <summary>
/// DbContext options extension for PostgreSQL.
/// </summary>
public static class PostgreSqlDbContextOptionsExtensions
{
    /// <summary>
    /// Configures the DbContext to use the PostgreSQL bulk insert provider.
    /// </summary>
    public static DbContextOptionsBuilder UseBulkInsertPostgreSql(this DbContextOptionsBuilder optionsBuilder)
    {
        return optionsBuilder.UseProvider<PostgreSqlBulkInsertProvider>();
    }
}
