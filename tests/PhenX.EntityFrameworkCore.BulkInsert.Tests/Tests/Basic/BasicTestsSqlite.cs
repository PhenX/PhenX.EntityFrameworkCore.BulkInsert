using PhenX.EntityFrameworkCore.BulkInsert.Tests.DbContainer;
using PhenX.EntityFrameworkCore.BulkInsert.Tests.DbContext;

using Xunit;

namespace PhenX.EntityFrameworkCore.BulkInsert.Tests.Tests.Basic;

[Trait("Category", "Sqlite")]
public class BasicTestsSqlite(TestDbContainerSqlite<TestDbContext> dbContainer) : BasicTestsBase<TestDbContainerSqlite<TestDbContext>>(dbContainer)
{
}

