using EntityFrameworkCore.ExecuteInsert.Tests.DbContainer;
using Xunit;

namespace EntityFrameworkCore.ExecuteInsert.Tests.Tests;

[Trait("Category", "Sqlite")]
public class BulkInsertProviderWithNavigationTestsSqlite : BulkInsertProviderWithNavigationTestsBase
{
    public BulkInsertProviderWithNavigationTestsSqlite() : base(new BulkInsertProviderDbContainerSqlite<TestDbContextWithNavigation>())
    {
    }
}

