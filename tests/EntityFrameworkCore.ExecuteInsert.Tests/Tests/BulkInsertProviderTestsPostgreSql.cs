using EntityFrameworkCore.ExecuteInsert.Tests.DbContainer;

using Xunit;

namespace EntityFrameworkCore.ExecuteInsert.Tests.Tests;

[Trait("Category", "PostgreSql")]
public class BulkInsertProviderTestsPostgreSql : BulkInsertProviderTestsBase
{
    public BulkInsertProviderTestsPostgreSql() : base(new BulkInsertProviderDbContainerPostgreSql<TestDbContext>())
    {
    }
}
