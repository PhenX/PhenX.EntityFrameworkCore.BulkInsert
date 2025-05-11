using EntityFrameworkCore.ExecuteInsert.Tests.DbContainer;
using EntityFrameworkCore.ExecuteInsert.Tests.DbContext;

using Xunit;

namespace EntityFrameworkCore.ExecuteInsert.Tests.Tests.Recursive;

[Trait("Category", "Sqlite")]
public class ResursiveTestsSqlite : ResursiveTestsBase
{
    public ResursiveTestsSqlite() : base(new TestDbContainerSqlite<TestDbContextWithNavProps>())
    {
    }
}

