using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace EntityFrameworkCore.ExecuteInsert.PostgreSql;

public static class PostgresBulkInsertExtensions
{
    public static DbContextOptionsBuilder UseExecuteInsertPostgres(this DbContextOptionsBuilder optionsBuilder)
    {
        var extension = optionsBuilder.Options.FindExtension<ExecuteInsertOptionsExtension<BulkInsertProvider>>() ?? new ExecuteInsertOptionsExtension<BulkInsertProvider>();

        ((IDbContextOptionsBuilderInfrastructure)optionsBuilder).AddOrUpdateExtension(extension);

        return optionsBuilder;
    }
}
