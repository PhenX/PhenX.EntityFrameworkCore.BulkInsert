using DotNet.Testcontainers.Containers;

using Microsoft.EntityFrameworkCore;

using PhenX.EntityFrameworkCore.BulkInsert.MySql;

using Testcontainers.MySql;

namespace PhenX.EntityFrameworkCore.BulkInsert.Benchmark;

public class LibComparatorMySql : LibComparator
{
    protected override void ConfigureDbContext()
    {
        var connectionString = GetConnectionString() + ";AllowLoadLocalInfile=true;";

        DbContext = new TestDbContext(p => p
            .UseMySql(connectionString, ServerVersion.AutoDetect(connectionString))
            .UseBulkInsertMySql()
        );
    }

    protected override IDatabaseContainer? GetDbContainer()
    {
        return new MySqlBuilder()
            .WithCommand("--log-bin-trust-function-creators=1", "--local-infile=1")
            .Build();
    }
}
