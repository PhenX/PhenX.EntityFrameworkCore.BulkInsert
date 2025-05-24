using PhenX.EntityFrameworkCore.BulkInsert.Tests.DbContainer;
using PhenX.EntityFrameworkCore.BulkInsert.Tests.DbContext;

using Xunit;

namespace PhenX.EntityFrameworkCore.BulkInsert.Tests.Tests.Basic;

[Trait("Category", "SqlServer")]
public class BasicTestsSqlServer(TestDbContainerSqlServer<TestDbContextSqlServer> dbContainer) : BasicTestsBase<TestDbContainerSqlServer<TestDbContextSqlServer>, TestDbContextSqlServer>(dbContainer)
{
}
