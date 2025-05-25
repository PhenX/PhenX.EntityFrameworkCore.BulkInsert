using Microsoft.EntityFrameworkCore;

using PhenX.EntityFrameworkCore.BulkInsert.Extensions;

namespace PhenX.EntityFrameworkCore.BulkInsert.Oracle;

/// <summary>
/// DbContext options extension for Oracle.
/// </summary>
public static class OracleDbContextOptionsExtensions
{
    /// <summary>
    /// Configures the DbContext to use the Oracle bulk insert provider.
    /// </summary>
    public static DbContextOptionsBuilder UseBulkInsertOracle(this DbContextOptionsBuilder optionsBuilder)
    {
        return optionsBuilder.UseProvider<OracleBulkInsertProvider>();
    }
}
