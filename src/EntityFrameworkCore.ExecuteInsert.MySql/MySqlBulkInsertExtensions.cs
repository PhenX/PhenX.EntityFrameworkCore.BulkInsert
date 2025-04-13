using EntityFrameworkCore.ExecuteInsert.Abstractions;

using Microsoft.EntityFrameworkCore;

namespace EntityFrameworkCore.ExecuteInsert.MySql;

public static class MySqlBulkInsertExtensions
{
    public static DbContextOptionsBuilder UseExecuteInsertMySql(this DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.ReplaceService<IBulkInsertProvider, MySqlBulkInsertProvider>();
        return optionsBuilder;
    }
}
