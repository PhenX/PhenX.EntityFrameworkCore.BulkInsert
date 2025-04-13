using EntityFrameworkCore.ExecuteInsert.Abstractions;

using Microsoft.EntityFrameworkCore;

namespace EntityFrameworkCore.ExecuteInsert.Oracle;

public static class OracleBulkInsertExtensions
{
    public static DbContextOptionsBuilder UseExecuteInsertOracle(this DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.ReplaceService<IBulkInsertProvider, OracleBulkInsertProvider>();
        return optionsBuilder;
    }
}
