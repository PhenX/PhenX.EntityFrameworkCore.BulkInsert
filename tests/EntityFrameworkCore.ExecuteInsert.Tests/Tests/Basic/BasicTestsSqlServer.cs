using EntityFrameworkCore.ExecuteInsert.Tests.DbContainer;
using EntityFrameworkCore.ExecuteInsert.Tests.DbContext;

using Xunit;

namespace EntityFrameworkCore.ExecuteInsert.Tests.Tests.Basic;

[Trait("Category", "SqlServer")]
public class BasicTestsSqlServer : BasicTestsBase
{
    public BasicTestsSqlServer() : base(new TestDbContainerSqlServer<TestDbContext>())
    {
    }
}
