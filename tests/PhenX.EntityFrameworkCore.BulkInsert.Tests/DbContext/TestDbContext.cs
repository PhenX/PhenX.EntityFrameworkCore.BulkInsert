using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace PhenX.EntityFrameworkCore.BulkInsert.Tests.DbContext;

public class TestDbContext : TestDbContextBase
{
    public DbSet<TestEntity> TestEntities { get; set; } = null!;
    public DbSet<TestEntityWithSimpleTypes> TestEntitiesWithSimpleTypes { get; set; } = null!;
    public DbSet<TestEntityWithJson> TestEntitiesWithJson { get; set; } = null!;
    public DbSet<TestEntityWithGuidId> TestEntitiesWithGuidId { get; set; } = null!;
    public DbSet<TestEntityWithConverters> TestEntitiesWithConverter { get; set; } = null!;
    public DbSet<TestEntityWithComplexType> TestEntitiesWithComplexType { get; set; } = null!;
    public DbSet<Student> Students { get; set; } = null!;
    public DbSet<Course> Courses { get; set; } = null!;

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

        modelBuilder.Entity<TestEntityWithComplexType>(builder =>
        {
            builder
                .ComplexProperty(e => e.OwnedComplexType)
                .IsRequired();
        });

        // Many-to-many with shadow property
        modelBuilder.Entity<Student>()
            .HasMany(s => s.Courses)
            .WithMany(c => c.Students)
            .UsingEntity<Dictionary<string, object>>(
                "StudentCourse",
                j => j.HasOne<Course>().WithMany().HasForeignKey("CourseId"),
                j => j.HasOne<Student>().WithMany().HasForeignKey("StudentId"),
                j =>
                {
                    j.Property<DateTime>("EnrolledAt");
                    j.HasKey("StudentId", "CourseId");
                }
            );
    }
}

public class TestDbContextPostgreSql : TestDbContext
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<TestEntityWithJson>(b =>
        {
            b.Property(x => x.JsonArray).AsJsonString("jsonb");
            b.Property(x => x.JsonObject).AsJsonString("jsonb");
        });

        modelBuilder.Entity<TestEntity>(b =>
        {
            b.Property(x => x.StringEnumValue).HasColumnType("text");
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
            b.Property(x => x.JsonArray).AsJsonString("json");
            b.Property(x => x.JsonObject).AsJsonString("json");
        });

        modelBuilder.Entity<TestEntity>(b =>
        {
            b.Property(x => x.StringEnumValue).HasColumnType("text");
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
            b.Property(x => x.JsonArray).AsJsonString(null);
            b.Property(x => x.JsonObject).AsJsonString(null);
        });

        modelBuilder.Entity<TestEntity>(b =>
        {
            b.Property(x => x.StringEnumValue).HasColumnType("text");
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
            b.Property(x => x.JsonArray).AsJsonString(null);
            b.Property(x => x.JsonObject).AsJsonString(null);
        });

        modelBuilder.Entity<TestEntity>(b =>
        {
            b.Property(x => x.StringEnumValue).HasColumnType("text");
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
            b.Property(x => x.JsonArray).AsJsonString(null);
            b.Property(x => x.JsonObject).AsJsonString(null);
        });

        modelBuilder.Entity<TestEntity>(b =>
        {
            b.Property(x => x.StringEnumValue).HasColumnType("nvarchar2(255)");
        });
    }
}
