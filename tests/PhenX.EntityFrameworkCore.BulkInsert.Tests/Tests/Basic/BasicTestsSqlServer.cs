using PhenX.EntityFrameworkCore.BulkInsert.Tests.DbContainer;
using PhenX.EntityFrameworkCore.BulkInsert.Tests.DbContext;

using Xunit;

namespace PhenX.EntityFrameworkCore.BulkInsert.Tests.Tests.Basic;

[Trait("Category", "SqlServer")]
[Collection(TestDbContainerSqlServerCollection.Name)]
public class BasicTestsSqlServer(TestDbContainerSqlServer dbContainer) : BasicTestsBase<TestDbContextSqlServer>(dbContainer)
{
}
