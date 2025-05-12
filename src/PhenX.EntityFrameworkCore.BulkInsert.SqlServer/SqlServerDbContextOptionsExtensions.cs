using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace PhenX.EntityFrameworkCore.BulkInsert.SqlServer;

public static class SqlServerDbContextOptionsExtensions
{
    public static DbContextOptionsBuilder UseExecuteInsertSqlServer(this DbContextOptionsBuilder optionsBuilder)
    {
        var extension = optionsBuilder.Options.FindExtension<ExecuteInsertOptionsExtension<SqlServerBulkInsertProvider>>() ?? new ExecuteInsertOptionsExtension<SqlServerBulkInsertProvider>();

        ((IDbContextOptionsBuilderInfrastructure)optionsBuilder).AddOrUpdateExtension(extension);

        return optionsBuilder;
    }
}
