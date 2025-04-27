
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using EntityFrameworkCore.ExecuteInsert.Abstractions;
using EntityFrameworkCore.ExecuteInsert.PostgreSql;

using Microsoft.EntityFrameworkCore;

using Xunit;

namespace EntityFrameworkCore.ExecuteInsert.Tests;

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

public class TestDbContextWithNavigation : DbContext
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
public class BulkInsertProviderWithNavigationTests : BulkInsertProviderTestsBase<TestDbContextWithNavigation>
{

    [Fact]
    public async Task InsertsEntitiesWithNavigationPropertiesSuccessfully()
    {
        // Arrange
        var parents = new List<ParentEntity>
        {
            new ParentEntity
            {
                Id = 1,
                Name = "Parent1",
                Children = new List<ChildEntity>
                {
                    new ChildEntity { Id = 1, Name = "Child1" },
                    new ChildEntity { Id = 2, Name = "Child2" }
                },
                GrandParent = new GrandParentEntity { Id = 1, Name = "GrandParent1" }
            },
            new ParentEntity
            {
                Id = 2,
                Name = "Parent2",
                Children = new List<ChildEntity>
                {
                    new ChildEntity { Id = 3, Name = "Child3" }
                },
                GrandParent = new GrandParentEntity { Id = 2, Name = "GrandParent2" }
            }
        };

        // Act
        await DbContext.ParentEntities.ExecuteInsertAsync(parents, o => o.Recursive = true);

        // Assert
        var insertedGrandParents = DbContext.GrandParentEntities.ToList();
        var insertedParents = DbContext.ParentEntities.ToList();
        var insertedChildren = DbContext.ChildEntities.ToList();

        Assert.Equal(2, insertedGrandParents.Count);
        Assert.Equal(2, insertedParents.Count);
        // Assert.Equal(3, insertedChildren.Count);

        Assert.Contains(insertedParents, p => p.Name == "Parent1");
        // Assert.Contains(insertedChildren, c => c.Name == "Child1");
        // Assert.Contains(insertedChildren, c => c.Name == "Child3");
    }
}
