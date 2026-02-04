using Microsoft.EntityFrameworkCore;

namespace PhenX.EntityFrameworkCore.BulkInsert.Benchmark;

public class TestDbContext(Action<DbContextOptionsBuilder> configure) : DbContext
{
    public Action<DbContextOptionsBuilder> Configure { get; } = configure;

    public DbSet<TestEntity> TestEntities { get; set; } = null!;
    public DbSet<TestEntityChild> TestEntityChildren { get; set; } = null!;

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        Configure(optionsBuilder);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<TestEntity>(builder =>
        {
            builder.HasMany(e => e.Children)
                .WithOne(c => c.TestEntity)
                .HasForeignKey(c => c.TestEntityId);
        });
    }
}
