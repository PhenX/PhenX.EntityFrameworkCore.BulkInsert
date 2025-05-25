using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace PhenX.EntityFrameworkCore.BulkInsert.Tests.DbContext;

public class TestDbContext : TestDbContextBase
{
    public DbSet<TestEntity> TestEntities { get; set; } = null!;
    public DbSet<TestEntityWithJson> TestEntitiesWithJson { get; set; } = null!;
    public DbSet<TestEntityWithGuidId> TestEntitiesWithGuidId { get; set; } = null!;
    public DbSet<TestEntityWithConverters> TestEntitiesWithConverter { get; set; } = null!;

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

public class TestDbContextPostgreSql : TestDbContext
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<TestEntityWithJson>(b =>
        {
            b.Property(x => x.Json).AsJsonString("jsonb");
        });
    }
}

public class TestDbContextMySql : TestDbContext
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<TestEntityWithJson>(b =>
        {
            b.Property(x => x.Json).AsJsonString("json");
        });
    }
}

public class TestDbContextSqlServer : TestDbContext
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<TestEntityWithJson>(b =>
        {
            b.Property(x => x.Json).AsJsonString(null);
        });
    }
}

public class TestDbContextSqlite : TestDbContext
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<TestEntityWithJson>(b =>
        {
            b.Property(x => x.Json).AsJsonString(null);
        });
    }
}

public class TestDbContextOracle : TestDbContext
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<TestEntityWithJson>(b =>
        {
            b.Property(x => x.Json).AsJsonString(null);
        });
    }
}



