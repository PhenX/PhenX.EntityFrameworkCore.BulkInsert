using PhenX.EntityFrameworkCore.BulkInsert.Tests.DbContainer;
using PhenX.EntityFrameworkCore.BulkInsert.Tests.DbContext;

using Xunit;

namespace PhenX.EntityFrameworkCore.BulkInsert.Tests.Tests.Geo;

[Trait("Category", "PostgreSql")]
public class GeoTestsPostgreSql(TestDbContainerPostgreSql<TestDbContextGeo> dbContainer) : GeoTestsBase<TestDbContainerPostgreSql<TestDbContextGeo>, TestDbContextGeo>(dbContainer)
{
}
