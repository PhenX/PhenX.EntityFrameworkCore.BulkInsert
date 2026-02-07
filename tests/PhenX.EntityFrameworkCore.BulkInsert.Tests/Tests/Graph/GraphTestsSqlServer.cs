using PhenX.EntityFrameworkCore.BulkInsert.Tests.DbContainer;
using PhenX.EntityFrameworkCore.BulkInsert.Tests.DbContext;

using Xunit;

namespace PhenX.EntityFrameworkCore.BulkInsert.Tests.Tests.Graph;

[Trait("Category", "SqlServer")]
[Collection(TestDbContainerSqlServerCollection.Name)]
public class GraphTestsSqlServer(TestDbContainerSqlServer dbContainer) : GraphTestsBase<TestDbContextSqlServer>(dbContainer)
{
}
