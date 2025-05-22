using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace PhenX.EntityFrameworkCore.BulkInsert.Tests.DbContext;

public class TestDbContext : TestDbContextBase
{
    public DbSet<TestEntity> TestEntities { get; set; } = null!;
    public DbSet<TestEntityWithGuidId> TestEntitiesWithGuidIds { get; set; } = null!;
    public DbSet<TestEntityWithConverters> TestEntitiesWithConverters { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.Entity<TestEntityWithConverters>(builder =>
        {
            builder.Property(e => e.CreatedAt)
                .HasConversion(new DateTimeToBinaryConverter());
        });

        modelBuilder.Entity<TestEntityWithGuidId>(builder =>
        {
            builder.Property(e => e.Id)
                .ValueGeneratedNever();
        });
    }
}
