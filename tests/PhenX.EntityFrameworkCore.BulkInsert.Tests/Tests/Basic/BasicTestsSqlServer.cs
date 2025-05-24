using PhenX.EntityFrameworkCore.BulkInsert.Tests.DbContainer;
using PhenX.EntityFrameworkCore.BulkInsert.Tests.DbContext;

using Xunit;

namespace PhenX.EntityFrameworkCore.BulkInsert.Tests.Tests.Basic;

public class BasicTestsSqlServerFixture : TestDbContainerSqlServer<TestDbContextSqlServer>
{
    public BasicTestsSqlServerFixture() : base("basic-sqlserver")
    {
    }
}

[Trait("Category", "SqlServer")]
public class BasicTestsSqlServer(BasicTestsSqlServerFixture dbContainer) : BasicTestsBase<BasicTestsSqlServerFixture, TestDbContextSqlServer>(dbContainer)
{
}
