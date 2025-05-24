using PhenX.EntityFrameworkCore.BulkInsert.Tests.DbContainer;
using PhenX.EntityFrameworkCore.BulkInsert.Tests.DbContext;

using Xunit;

namespace PhenX.EntityFrameworkCore.BulkInsert.Tests.Tests.Basic;

[Trait("Category", "Sqlite")]
[Collection(TestDbContainerSqliteCollection.Name)]
public class BasicTestsSqlite(TestDbContainerSqlite dbContainer) : BasicTestsBase<TestDbContextSqlite>(dbContainer)
{
}
