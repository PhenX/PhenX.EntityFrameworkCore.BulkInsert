using Microsoft.EntityFrameworkCore;

namespace EntityFrameworkCore.ExecuteInsert.Tests.DbContext;

public class TestDbContext : TestDbContextBase
{
    public DbSet<TestEntity> TestEntities { get; set; } = null!;
}
