using EntityFrameworkCore.ExecuteInsert.Tests.DbContainer;

using Xunit;

namespace EntityFrameworkCore.ExecuteInsert.Tests.Tests;

[Trait("Category", "SqlServer")]
public class BulkInsertProviderTestsSqlServer : BulkInsertProviderTestsBase
{
    public BulkInsertProviderTestsSqlServer() : base(new BulkInsertProviderDbContainerSqlServer<TestDbContext>())
    {
    }
}
