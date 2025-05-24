using PhenX.EntityFrameworkCore.BulkInsert.Tests.DbContainer;
using PhenX.EntityFrameworkCore.BulkInsert.Tests.DbContext;

using Xunit;

namespace PhenX.EntityFrameworkCore.BulkInsert.Tests.Tests.Geo;

public class GeoTestsSqlServerFixture : TestDbContainerSqlServer<TestDbContextGeo>
{
    public GeoTestsSqlServerFixture() : base("geo-sqlserver")
    {
    }
}

[Trait("Category", "SqlServer")]
public class GeoTestsSqlServer(GeoTestsSqlServerFixture dbContainer) : GeoTestsBase<GeoTestsSqlServerFixture, TestDbContextGeo>(dbContainer)
{
}
