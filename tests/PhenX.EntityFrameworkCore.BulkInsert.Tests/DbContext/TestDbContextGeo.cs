using Microsoft.EntityFrameworkCore;

namespace PhenX.EntityFrameworkCore.BulkInsert.Tests.DbContext;

public class TestDbContextGeo : TestDbContextBase
{
    public DbSet<TestEntityWithGeo> TestEntitiesWithGeo { get; set; } = null!;
}
