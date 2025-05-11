using EntityFrameworkCore.ExecuteInsert.Tests.DbContainer;
using EntityFrameworkCore.ExecuteInsert.Tests.DbContext;

using Xunit;

namespace EntityFrameworkCore.ExecuteInsert.Tests.Tests.Recursive;

[Trait("Category", "SqlServer")]
public class ResursiveTestsSqlServer : ResursiveTestsBase
{
    public ResursiveTestsSqlServer() : base(new TestDbContainerSqlServer<TestDbContextWithNavProps>())
    {
    }
}
