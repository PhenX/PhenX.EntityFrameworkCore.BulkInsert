using PhenX.EntityFrameworkCore.BulkInsert.Tests.DbContainer;
using PhenX.EntityFrameworkCore.BulkInsert.Tests.DbContext;

using Xunit;

namespace PhenX.EntityFrameworkCore.BulkInsert.Tests.Tests.Merge;

[Trait("Category", "Sqlite")]
[Collection(TestDbContainerSqliteCollection.Name)]
public class MergeTestsSqlite(TestDbContainerSqlite dbContainer) : MergeTestsBase<TestDbContextSqlite>(dbContainer)
{
}
