using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace PhenX.EntityFrameworkCore.BulkInsert.MySql;

/// <summary>
/// DbContext options extension for SQL Server.
/// </summary>
public static class MySqlDbContextOptionsExtensions
{
    /// <summary>
    /// Configures the DbContext to use the MySql bulk insert provider.
    /// </summary>
    public static DbContextOptionsBuilder UseBulkInsertMySql(this DbContextOptionsBuilder optionsBuilder)
    {
        var extension = optionsBuilder.Options.FindExtension<BulkInsertOptionsExtension<MySqlBulkInsertProvider>>() ?? new BulkInsertOptionsExtension<MySqlBulkInsertProvider>();

        ((IDbContextOptionsBuilderInfrastructure)optionsBuilder).AddOrUpdateExtension(extension);

        return optionsBuilder;
    }
}
