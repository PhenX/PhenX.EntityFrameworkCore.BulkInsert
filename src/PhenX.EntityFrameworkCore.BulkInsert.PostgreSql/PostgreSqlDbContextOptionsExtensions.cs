using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

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
        var extension = optionsBuilder.Options.FindExtension<BulkInsertOptionsExtension<PostgreSqlBulkInsertProvider>>() ?? new BulkInsertOptionsExtension<PostgreSqlBulkInsertProvider>();

        ((IDbContextOptionsBuilderInfrastructure)optionsBuilder).AddOrUpdateExtension(extension);

        return optionsBuilder;
    }
}
