using PhenX.EntityFrameworkCore.BulkInsert.Tests.DbContainer;
using PhenX.EntityFrameworkCore.BulkInsert.Tests.DbContext;

using Xunit;

namespace PhenX.EntityFrameworkCore.BulkInsert.Tests.Tests.Geo;

[Trait("Category", "SqlServer")]
public class GeoTestsSqlServer(TestDbContainerSqlServer<TestDbContextGeo> dbContainer) : GeoTestsBase<TestDbContainerSqlServer<TestDbContextGeo>, TestDbContextGeo>(dbContainer)
{
}
