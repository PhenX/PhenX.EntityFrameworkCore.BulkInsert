using PhenX.EntityFrameworkCore.BulkInsert.Tests.DbContainer;
using PhenX.EntityFrameworkCore.BulkInsert.Tests.DbContext;

using Xunit;

namespace PhenX.EntityFrameworkCore.BulkInsert.Tests.Tests.Geo;

[Trait("Category", "PostgreSql")]
[Collection(TestDbContainerPostgreSqlCollection.Name)]
public class GeoTestsPostgreSql(TestDbContainerPostgreSql dbContainer) : GeoTestsBase<TestDbContextGeo>(dbContainer)
{
}