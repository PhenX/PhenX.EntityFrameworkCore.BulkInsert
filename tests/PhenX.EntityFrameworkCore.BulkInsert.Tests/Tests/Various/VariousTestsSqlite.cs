using PhenX.EntityFrameworkCore.BulkInsert.Tests.DbContainer;
using PhenX.EntityFrameworkCore.BulkInsert.Tests.DbContext;

using Xunit;

namespace PhenX.EntityFrameworkCore.BulkInsert.Tests.Tests.Various;

[Trait("Category", "Sqlite")]
[Collection(TestDbContainerSqliteCollection.Name)]
public class VariousTestsSqlite(TestDbContainerSqlite dbContainer) : VariousTestsBase<TestDbContextSqlite>(dbContainer)
{
}
