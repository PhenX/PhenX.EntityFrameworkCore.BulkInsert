using PhenX.EntityFrameworkCore.BulkInsert.Tests.DbContainer;
using PhenX.EntityFrameworkCore.BulkInsert.Tests.DbContext;

using Xunit;

namespace PhenX.EntityFrameworkCore.BulkInsert.Tests.Tests.Various;

[Trait("Category", "SqlServer")]
[Collection(TestDbContainerSqlServerCollection.Name)]
public class VariousTestsSqlServer(TestDbContainerSqlServer dbContainer) : VariousTestsBase<TestDbContextSqlServer>(dbContainer)
{
}
