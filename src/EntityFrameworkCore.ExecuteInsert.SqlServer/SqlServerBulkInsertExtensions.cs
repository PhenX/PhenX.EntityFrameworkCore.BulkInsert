using EntityFrameworkCore.ExecuteInsert.Abstractions;

using Microsoft.EntityFrameworkCore;

namespace EntityFrameworkCore.ExecuteInsert.SqlServer;

public static class SqlServerBulkInsertExtensions
{
    public static DbContextOptionsBuilder UseExecuteInsertSqlServer(this DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.ReplaceService<IBulkInsertProvider, SqlServerBulkInsertProvider>();
        return optionsBuilder;
    }
}
