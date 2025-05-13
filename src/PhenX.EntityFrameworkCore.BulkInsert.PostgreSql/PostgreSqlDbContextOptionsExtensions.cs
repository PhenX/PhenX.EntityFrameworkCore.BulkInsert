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
    public static DbContextOptionsBuilder UseExecuteInsertPostgres(this DbContextOptionsBuilder optionsBuilder)
    {
        var extension = optionsBuilder.Options.FindExtension<ExecuteInsertOptionsExtension<PostgreSqlBulkInsertProvider>>() ?? new ExecuteInsertOptionsExtension<PostgreSqlBulkInsertProvider>();

        ((IDbContextOptionsBuilderInfrastructure)optionsBuilder).AddOrUpdateExtension(extension);

        return optionsBuilder;
    }
}
