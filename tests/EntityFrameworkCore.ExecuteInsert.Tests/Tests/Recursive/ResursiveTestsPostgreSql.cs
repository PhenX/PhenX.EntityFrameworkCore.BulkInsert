using EntityFrameworkCore.ExecuteInsert.Tests.DbContainer;
using EntityFrameworkCore.ExecuteInsert.Tests.DbContext;

using Xunit;

namespace EntityFrameworkCore.ExecuteInsert.Tests.Tests.Recursive;

[Trait("Category", "PostgreSql")]
public class ResursiveTestsPostgreSql : ResursiveTestsBase
{
    public ResursiveTestsPostgreSql() : base(new TestDbContainerPostgreSql<TestDbContextWithNavProps>())
    {
    }
}
