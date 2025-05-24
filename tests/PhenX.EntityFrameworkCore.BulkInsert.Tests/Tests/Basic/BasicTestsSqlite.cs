using PhenX.EntityFrameworkCore.BulkInsert.Tests.DbContainer;
using PhenX.EntityFrameworkCore.BulkInsert.Tests.DbContext;

using Xunit;

namespace PhenX.EntityFrameworkCore.BulkInsert.Tests.Tests.Basic;

public class BasicTestsSqliteFixture : TestDbContainerSqlite<TestDbContextSqlite>
{
}

[Trait("Category", "Sqlite")]
public class BasicTestsSqlite(BasicTestsSqliteFixture dbContainer) : BasicTestsBase<BasicTestsSqliteFixture, TestDbContextSqlite>(dbContainer)
{
}

