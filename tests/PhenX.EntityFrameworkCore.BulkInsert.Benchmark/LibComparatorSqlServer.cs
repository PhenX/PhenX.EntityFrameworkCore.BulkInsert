using DotNet.Testcontainers.Containers;

using LinqToDB.EntityFrameworkCore;

using Microsoft.EntityFrameworkCore;

using PhenX.EntityFrameworkCore.BulkInsert.SqlServer;

using Testcontainers.MsSql;

namespace PhenX.EntityFrameworkCore.BulkInsert.Benchmark;

public class LibComparatorSqlServer : LibComparator
{
    protected override void ConfigureDbContext()
    {
        var connectionString = GetConnectionString();

        DbContext = new TestDbContext(p => p
            .UseSqlServer(connectionString)
            .UseBulkInsertSqlServer()
            .UseLinqToDB()
        );
    }

    protected override IDatabaseContainer? GetDbContainer()
    {
        return new MsSqlBuilder().Build();
    }
}
