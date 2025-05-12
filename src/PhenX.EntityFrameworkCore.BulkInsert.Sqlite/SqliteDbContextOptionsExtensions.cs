using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace PhenX.EntityFrameworkCore.BulkInsert.Sqlite;

public static class SqliteDbContextOptionsExtensions
{
    public static DbContextOptionsBuilder UseExecuteInsertSqlite(this DbContextOptionsBuilder optionsBuilder)
    {
        var extension = optionsBuilder.Options.FindExtension<ExecuteInsertOptionsExtension<SqliteBulkInsertProvider>>() ?? new ExecuteInsertOptionsExtension<SqliteBulkInsertProvider>();
        ((IDbContextOptionsBuilderInfrastructure)optionsBuilder).AddOrUpdateExtension(extension);
        return optionsBuilder;
    }
}

