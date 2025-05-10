using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace EntityFrameworkCore.ExecuteInsert.Sqlite;

public static class SqliteDbContextOptionsExtensions
{
    public static DbContextOptionsBuilder UseExecuteInsertSqlite(this DbContextOptionsBuilder optionsBuilder)
    {
        var extension = optionsBuilder.Options.FindExtension<ExecuteInsertOptionsExtension<SqliteBulkInsertProvider>>() ?? new ExecuteInsertOptionsExtension<SqliteBulkInsertProvider>();
        ((IDbContextOptionsBuilderInfrastructure)optionsBuilder).AddOrUpdateExtension(extension);
        return optionsBuilder;
    }
}

