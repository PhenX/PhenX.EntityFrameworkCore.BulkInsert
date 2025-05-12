using Microsoft.EntityFrameworkCore;

namespace PhenX.EntityFrameworkCore.BulkInsert.Benchmark;

public class TestDbContext : DbContext
{
    public Action<DbContextOptionsBuilder> Configure { get; }

    public DbSet<TestEntity> TestEntities { get; set; } = null!;

    public TestDbContext(Action<DbContextOptionsBuilder> configure)
    {
        Configure = configure;
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        Configure(optionsBuilder);
    }
}
