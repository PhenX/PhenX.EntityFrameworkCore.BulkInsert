using PhenX.EntityFrameworkCore.BulkInsert.Tests.DbContainer;
using PhenX.EntityFrameworkCore.BulkInsert.Tests.DbContext;

using Xunit;

namespace PhenX.EntityFrameworkCore.BulkInsert.Tests.Tests.Basic;

public class BasicTestsPostgreSqlFixture : TestDbContainerSqlServer<TestDbContextPostgreSql>
{
    public BasicTestsPostgreSqlFixture() : base("basic-postgresql")
    {
    }
}

[Trait("Category", "PostgreSql")]
public class BasicTestsPostgreSql(BasicTestsPostgreSqlFixture dbContainer) : BasicTestsBase<BasicTestsPostgreSqlFixture, TestDbContextPostgreSql>(dbContainer)
{
}
