using PhenX.EntityFrameworkCore.BulkInsert.Tests.DbContainer;
using PhenX.EntityFrameworkCore.BulkInsert.Tests.DbContext;

using Xunit;

namespace PhenX.EntityFrameworkCore.BulkInsert.Tests.Tests.Geo;

[Trait("Category", "MySql")]
public class GeoTestsMySql(TestDbContainerMySql<TestDbContextGeo> dbContainer) : GeoTestsBase<TestDbContainerMySql<TestDbContextGeo>, TestDbContextGeo>(dbContainer)
{
}
