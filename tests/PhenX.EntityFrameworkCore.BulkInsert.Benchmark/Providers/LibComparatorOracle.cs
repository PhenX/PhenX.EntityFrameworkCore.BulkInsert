using DotNet.Testcontainers.Containers;

using LinqToDB.EntityFrameworkCore;

using Microsoft.EntityFrameworkCore;

using PhenX.EntityFrameworkCore.BulkInsert.Oracle;

using Testcontainers.Oracle;

namespace PhenX.EntityFrameworkCore.BulkInsert.Benchmark.Providers;

public class LibComparatorOracle : LibComparator
{
    protected override void ConfigureDbContext()
    {
        var connectionString = GetConnectionString();

        DbContext = new TestDbContext(p => p
            .UseOracle(connectionString)
            .UseBulkInsertOracle()
            .UseLinqToDB()
        );
    }

    protected override IDatabaseContainer? GetDbContainer()
    {
        return new OracleBuilder("gvenzl/oracle-free:23-slim-faststart")
            .Build();
    }
}
