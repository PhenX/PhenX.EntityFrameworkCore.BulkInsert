using EntityFrameworkCore.ExecuteInsert.Tests.DbContainer;

using Xunit;

namespace EntityFrameworkCore.ExecuteInsert.Tests.Tests;

[Trait("Category", "PostgreSql")]
public class BulkInsertProviderWithNavigationTestsPostgreSql : BulkInsertProviderWithNavigationTestsBase
{
    public BulkInsertProviderWithNavigationTestsPostgreSql() : base(new BulkInsertProviderDbContainerPostgreSql<TestDbContextWithNavigation>())
    {
    }
}
