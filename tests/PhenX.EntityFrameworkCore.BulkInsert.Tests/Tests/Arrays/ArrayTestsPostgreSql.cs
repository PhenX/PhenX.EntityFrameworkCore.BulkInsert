using PhenX.EntityFrameworkCore.BulkInsert.Tests.DbContainer;
using PhenX.EntityFrameworkCore.BulkInsert.Tests.DbContext;

using Xunit;

namespace PhenX.EntityFrameworkCore.BulkInsert.Tests.Tests.Arrays;

[Trait("Category", "PostgreSql")]
[Collection(TestDbContainerPostgreSqlCollection.Name)]
public class ArrayTestsPostgreSql(TestDbContainerPostgreSql dbContainer) : ArrayTestsBase<TestDbContextPostgreSql>(dbContainer)
{
}
