using PhenX.EntityFrameworkCore.BulkInsert.Tests.DbContainer;
using PhenX.EntityFrameworkCore.BulkInsert.Tests.DbContext;

using Xunit;

namespace PhenX.EntityFrameworkCore.BulkInsert.Tests.Tests.Geo;

public class GeoTestsMySqlFixture : TestDbContainerMySql<TestDbContextGeo>
{
    public GeoTestsMySqlFixture() : base("geo-mysql")
    {
    }
}

[Trait("Category", "MySql")]
public class GeoTestsMySql(GeoTestsMySqlFixture dbContainer) : GeoTestsBase<GeoTestsMySqlFixture, TestDbContextGeo>(dbContainer)
{
}
