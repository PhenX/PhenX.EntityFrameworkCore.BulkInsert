using PhenX.EntityFrameworkCore.BulkInsert.Tests.DbContainer;
using PhenX.EntityFrameworkCore.BulkInsert.Tests.DbContext;

using Xunit;

namespace PhenX.EntityFrameworkCore.BulkInsert.Tests.Tests.Graph;

[Trait("Category", "Sqlite")]
[Collection(TestDbContainerSqliteCollection.Name)]
public class GraphTestsSqlite(TestDbContainerSqlite dbContainer) : GraphTestsBase<TestDbContextSqlite>(dbContainer)
{
}
