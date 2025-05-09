using EntityFrameworkCore.ExecuteInsert.Tests.DbContainer;
using Xunit;

namespace EntityFrameworkCore.ExecuteInsert.Tests.Tests;

[Trait("Category", "Sqlite")]
public class BulkInsertProviderTestsSqlite : BulkInsertProviderTestsBase
{
    public BulkInsertProviderTestsSqlite() : base(new BulkInsertProviderDbContainerSqlite<TestDbContext>())
    {
    }
}

