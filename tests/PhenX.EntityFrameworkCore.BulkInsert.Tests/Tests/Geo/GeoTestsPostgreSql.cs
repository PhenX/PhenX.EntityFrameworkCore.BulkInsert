using PhenX.EntityFrameworkCore.BulkInsert.Tests.DbContainer;
using PhenX.EntityFrameworkCore.BulkInsert.Tests.DbContext;

using Xunit;

namespace PhenX.EntityFrameworkCore.BulkInsert.Tests.Tests.Geo;

public class GeoTestsPostgreSqlFixture : TestDbContainerPostgreSql<TestDbContextGeo>
{
    public GeoTestsPostgreSqlFixture() : base("geo-postgresql")
    {
    }
}

[Trait("Category", "PostgreSql")]
public class GeoTestsPostgreSql(GeoTestsPostgreSqlFixture dbContainer) : GeoTestsBase<GeoTestsPostgreSqlFixture, TestDbContextGeo>(dbContainer)
{
}
