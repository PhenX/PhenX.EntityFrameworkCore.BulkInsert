using PhenX.EntityFrameworkCore.BulkInsert.Tests.DbContainer;
using PhenX.EntityFrameworkCore.BulkInsert.Tests.DbContext;

using Xunit;

namespace PhenX.EntityFrameworkCore.BulkInsert.Tests.Tests.Basic;

[Trait("Category", "SqlServer")]
public class BasicTestsSqlServer : BasicTestsBase
{
    public BasicTestsSqlServer() : base(new TestDbContainerSqlServer<TestDbContext>())
    {
    }
}
