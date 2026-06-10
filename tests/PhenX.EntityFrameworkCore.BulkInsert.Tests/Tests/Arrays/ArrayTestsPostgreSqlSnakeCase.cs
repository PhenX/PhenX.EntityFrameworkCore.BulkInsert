using PhenX.EntityFrameworkCore.BulkInsert.Tests.DbContainer;
using PhenX.EntityFrameworkCore.BulkInsert.Tests.DbContext;

using Xunit;

namespace PhenX.EntityFrameworkCore.BulkInsert.Tests.Tests.Arrays;

[Trait("Category", "PostgreSqlSnakeCase")]
[Collection(TestDbContainerPostgreSqlSnakeCaseCollection.Name)]
public class ArrayTestsPostgreSqlSnakeCase(TestDbContainerPostgreSqlSnakeCase dbContainer) : ArrayTestsBase<TestDbContextPostgreSql>(dbContainer)
{
}
