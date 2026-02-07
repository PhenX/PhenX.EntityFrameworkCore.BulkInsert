using PhenX.EntityFrameworkCore.BulkInsert.Tests.DbContainer;
using PhenX.EntityFrameworkCore.BulkInsert.Tests.DbContext;

using Xunit;

namespace PhenX.EntityFrameworkCore.BulkInsert.Tests.Tests.Graph;

[Trait("Category", "PostgreSql")]
[Collection(TestDbContainerPostgreSqlCollection.Name)]
public class GraphTestsPostgreSql(TestDbContainerPostgreSql dbContainer) : GraphTestsBase<TestDbContextPostgreSql>(dbContainer)
{
}
