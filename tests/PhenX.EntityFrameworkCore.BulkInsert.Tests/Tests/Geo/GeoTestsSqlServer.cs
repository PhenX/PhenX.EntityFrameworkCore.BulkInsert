using PhenX.EntityFrameworkCore.BulkInsert.Tests.DbContainer;
using PhenX.EntityFrameworkCore.BulkInsert.Tests.DbContext;

using Xunit;

namespace PhenX.EntityFrameworkCore.BulkInsert.Tests.Tests.Geo;

[Trait("Category", "SqlServer")]
[Collection(TestDbContainerSqlServerCollection.Name)]
public class GeoTestsSqlServer(TestDbContainerSqlServer dbContainer) : GeoTestsBase<TestDbContextGeo>(dbContainer)
{
}