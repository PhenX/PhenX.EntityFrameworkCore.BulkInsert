using PhenX.EntityFrameworkCore.BulkInsert.Tests.DbContainer;
using PhenX.EntityFrameworkCore.BulkInsert.Tests.DbContext;

using Xunit;

namespace PhenX.EntityFrameworkCore.BulkInsert.Tests.Tests.Basic;

[Trait("Category", "Sqlite")]
public class BasicTestsSqlite : BasicTestsBase<TestDbContainerSqlite<TestDbContext>>
{
    public BasicTestsSqlite(TestDbContainerSqlite<TestDbContext> dbContainer) : base(dbContainer)
    {
    }
}

