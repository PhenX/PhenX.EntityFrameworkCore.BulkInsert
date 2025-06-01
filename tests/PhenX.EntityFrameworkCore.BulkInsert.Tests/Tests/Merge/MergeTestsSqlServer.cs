using PhenX.EntityFrameworkCore.BulkInsert.Tests.DbContainer;
using PhenX.EntityFrameworkCore.BulkInsert.Tests.DbContext;

using Xunit;

namespace PhenX.EntityFrameworkCore.BulkInsert.Tests.Tests.Merge;

[Trait("Category", "SqlServer")]
[Collection(TestDbContainerSqlServerCollection.Name)]
public class MergeTestsSqlServer(TestDbContainerSqlServer dbContainer) : MergeTestsBase<TestDbContextSqlServer>(dbContainer)
{
}
