using Microsoft.EntityFrameworkCore;

using PhenX.EntityFrameworkCore.BulkInsert.Extensions;

namespace PhenX.EntityFrameworkCore.BulkInsert.MySql;

/// <summary>
/// DbContext options extension for MySql.
/// </summary>
public static class MySqlDbContextOptionsExtensions
{
    /// <summary>
    /// Configures the DbContext to use the MySql bulk insert provider.
    /// </summary>
    public static DbContextOptionsBuilder UseBulkInsertMySql(this DbContextOptionsBuilder optionsBuilder)
    {
        return optionsBuilder.UseProvider<MySqlBulkInsertProvider>();
    }
}
