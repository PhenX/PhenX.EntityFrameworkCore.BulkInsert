using System.Collections.Generic;

using EntityFrameworkCore.ExecuteInsert.PostgreSql;

using Microsoft.EntityFrameworkCore;

namespace EntityFrameworkCore.ExecuteInsert.Tests;

public class TestDbContextWithNavigation : BulkDbContext
{
    public DbSet<GrandParentEntity> GrandParentEntities { get; set; } = null!;
    public DbSet<ParentEntity> ParentEntities { get; set; } = null!;
    public DbSet<ChildEntity> ChildEntities { get; set; } = null!;

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder
            .UseNpgsql()
            .UseExecuteInsertPostgres();
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ParentEntity>()
            .HasMany(p => p.Children)
            .WithOne(c => c.Parent)
            .HasForeignKey(c => c.ParentId);

        modelBuilder.Entity<ParentEntity>()
            .HasOne(p => p.GrandParent)
            .WithMany()
            .HasForeignKey(c => c.GrandParentId);
    }
}


public class GrandParentEntity
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
}

public class ParentEntity
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public ICollection<ChildEntity> Children { get; set; } = new List<ChildEntity>();
    public GrandParentEntity GrandParent { get; set; } = null!;
    public int GrandParentId { get; set; }
}

public class ChildEntity
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int ParentId { get; set; }
    public ParentEntity Parent { get; set; } = null!;
}
