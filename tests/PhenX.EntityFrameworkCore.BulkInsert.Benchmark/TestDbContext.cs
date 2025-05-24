using Microsoft.EntityFrameworkCore;

namespace PhenX.EntityFrameworkCore.BulkInsert.Benchmark;

public class TestDbContext(Action<DbContextOptionsBuilder> configure) : DbContext
{
    public Action<DbContextOptionsBuilder> Configure { get; } = configure;

    public DbSet<TestEntity> TestEntities { get; set; } = null!;

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        Configure(optionsBuilder);
    }
}
