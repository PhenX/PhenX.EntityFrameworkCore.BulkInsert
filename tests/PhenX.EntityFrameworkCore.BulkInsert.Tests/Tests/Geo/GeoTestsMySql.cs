using PhenX.EntityFrameworkCore.BulkInsert.Tests.DbContainer;
using PhenX.EntityFrameworkCore.BulkInsert.Tests.DbContext;

using Xunit;

namespace PhenX.EntityFrameworkCore.BulkInsert.Tests.Tests.Geo;

[Trait("Category", "MySql")]
[Collection(TestDbContainerMySqlCollection.Name)]
public class GeoTestsMySql(TestDbContainerMySql dbContainer) : GeoTestsBase<TestDbContextGeo>(dbContainer)
{
}
