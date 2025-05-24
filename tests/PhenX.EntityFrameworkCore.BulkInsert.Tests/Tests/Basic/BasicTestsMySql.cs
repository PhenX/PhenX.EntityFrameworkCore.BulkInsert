using PhenX.EntityFrameworkCore.BulkInsert.Tests.DbContainer;
using PhenX.EntityFrameworkCore.BulkInsert.Tests.DbContext;

using Xunit;

namespace PhenX.EntityFrameworkCore.BulkInsert.Tests.Tests.Basic;

public class BasicTestsMySqlFixture : TestDbContainerSqlServer<TestDbContextMySql>
{
    public BasicTestsMySqlFixture() : base("basic-mysql")
    {
    }
}

[Trait("Category", "MySql")]
public class BasicTestsMySql(BasicTestsMySqlFixture dbContainer) : BasicTestsBase<BasicTestsMySqlFixture, TestDbContextMySql>(dbContainer)
{
}
