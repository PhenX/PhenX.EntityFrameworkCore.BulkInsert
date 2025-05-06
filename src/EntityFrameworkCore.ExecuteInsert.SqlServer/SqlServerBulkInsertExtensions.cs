using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace EntityFrameworkCore.ExecuteInsert.SqlServer;

public static class SqlServerBulkInsertExtensions
{
    public static DbContextOptionsBuilder UseExecuteInsertSqlServer(this DbContextOptionsBuilder optionsBuilder)
    {
        var extension = optionsBuilder.Options.FindExtension<ExecuteInsertOptionsExtension<SqlServerBulkInsertProvider>>() ?? new ExecuteInsertOptionsExtension<SqlServerBulkInsertProvider>();

        ((IDbContextOptionsBuilderInfrastructure)optionsBuilder).AddOrUpdateExtension(extension);

        return optionsBuilder;
    }
}
