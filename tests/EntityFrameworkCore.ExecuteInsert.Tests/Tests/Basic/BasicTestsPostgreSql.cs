using EntityFrameworkCore.ExecuteInsert.Tests.DbContainer;
using EntityFrameworkCore.ExecuteInsert.Tests.DbContext;

using Xunit;

namespace EntityFrameworkCore.ExecuteInsert.Tests.Tests.Basic;

[Trait("Category", "PostgreSql")]
public class BasicTestsPostgreSql : BasicTestsBase
{
    public BasicTestsPostgreSql() : base(new TestDbContainerPostgreSql<TestDbContext>())
    {
    }
}
