using PhenX.EntityFrameworkCore.BulkInsert.Tests.DbContainer;
using PhenX.EntityFrameworkCore.BulkInsert.Tests.DbContext;

using Xunit;

namespace PhenX.EntityFrameworkCore.BulkInsert.Tests.Tests.Basic;

[Trait("Category", "PostgreSql")]
[Collection(TestDbContainerPostgreSqlCollection.Name)]
public class BasicTestsPostgreSql(TestDbContainerPostgreSql dbContainer) : BasicTestsBase<TestDbContextPostgreSql>(dbContainer)
{
}
