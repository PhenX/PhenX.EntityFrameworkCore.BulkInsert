using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace EntityFrameworkCore.ExecuteInsert.PostgreSql;

public static class PostgreSqlDbContextOptionsExtensions
{
    public static DbContextOptionsBuilder UseExecuteInsertPostgres(this DbContextOptionsBuilder optionsBuilder)
    {
        var extension = optionsBuilder.Options.FindExtension<ExecuteInsertOptionsExtension<PostgreSqlBulkInsertProvider>>() ?? new ExecuteInsertOptionsExtension<PostgreSqlBulkInsertProvider>();

        ((IDbContextOptionsBuilderInfrastructure)optionsBuilder).AddOrUpdateExtension(extension);

        return optionsBuilder;
    }
}
