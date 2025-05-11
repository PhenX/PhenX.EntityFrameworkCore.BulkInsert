using EntityFrameworkCore.ExecuteInsert.Tests.DbContainer;
using EntityFrameworkCore.ExecuteInsert.Tests.DbContext;

using Xunit;

namespace EntityFrameworkCore.ExecuteInsert.Tests.Tests.Basic;

[Trait("Category", "Sqlite")]
public class BasicTestsSqlite : BasicTestsBase
{
    public BasicTestsSqlite() : base(new TestDbContainerSqlite<TestDbContext>())
    {
    }
}

