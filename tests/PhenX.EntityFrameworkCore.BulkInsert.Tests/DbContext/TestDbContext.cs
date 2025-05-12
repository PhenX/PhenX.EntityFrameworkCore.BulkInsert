using Microsoft.EntityFrameworkCore;

namespace PhenX.EntityFrameworkCore.BulkInsert.Tests.DbContext;

public class TestDbContext : TestDbContextBase
{
    public DbSet<TestEntity> TestEntities { get; set; } = null!;
}
