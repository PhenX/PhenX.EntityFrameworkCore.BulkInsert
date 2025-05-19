using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace PhenX.EntityFrameworkCore.BulkInsert.Tests.DbContext;

public class TestDbContext : TestDbContextBase
{
    public DbSet<TestEntity> TestEntities { get; set; } = null!;
    public DbSet<TestEntityWithConverters> TestEntitiesWithConverters { get; set; } = null!;
    public DbSet<TestEntityWithMultipleTypes> TestEntityWithMultipleTypes { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        ConfigureModel(modelBuilder);

        modelBuilder.Entity<TestEntityWithConverters>(builder =>
        {
            builder.Property(e => e.CreatedAt)
                .HasConversion(new DateTimeToBinaryConverter());
        });
    }
}
