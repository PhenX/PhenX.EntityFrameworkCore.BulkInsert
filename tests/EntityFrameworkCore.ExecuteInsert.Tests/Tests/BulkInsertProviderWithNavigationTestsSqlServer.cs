using EntityFrameworkCore.ExecuteInsert.Tests.DbContainer;

using Xunit;

namespace EntityFrameworkCore.ExecuteInsert.Tests.Tests;

[Trait("Category", "SqlServer")]
public class BulkInsertProviderWithNavigationTestsSqlServer : BulkInsertProviderWithNavigationTestsBase
{
    public BulkInsertProviderWithNavigationTestsSqlServer() : base(new BulkInsertProviderDbContainerSqlServer<TestDbContextWithNavigation>())
    {
    }
}
