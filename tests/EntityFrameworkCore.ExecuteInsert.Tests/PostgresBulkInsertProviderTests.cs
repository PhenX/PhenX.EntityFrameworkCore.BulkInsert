using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using EntityFrameworkCore.ExecuteInsert.Abstractions;

using Microsoft.EntityFrameworkCore;
using Xunit;
using EntityFrameworkCore.ExecuteInsert.PostgreSql;
using Testcontainers.PostgreSql;

namespace EntityFrameworkCore.ExecuteInsert.Tests;

public class PostgresBulkInsertProviderTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgresContainer;

    public PostgresBulkInsertProviderTests()
    {
        _postgresContainer = GetPostgresContainer();
    }

    private static PostgreSqlContainer GetPostgresContainer()
    {
        return new PostgreSqlBuilder()
            .WithDatabase("testdb")
            .WithUsername("testuser")
            .WithPassword("testpassword")
            .Build();
    }

    public async Task InitializeAsync()
    {
        await _postgresContainer.StartAsync();
    }

    public async Task DisposeAsync()
    {
        await _postgresContainer.DisposeAsync();
    }

    private class TestEntity
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    private class TestDbContext : DbContext
    {
        private readonly string _connectionString;

        public TestDbContext(string connectionString)
        {
            _connectionString = connectionString;
        }

        public DbSet<TestEntity> TestEntities { get; set; } = null!;

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseNpgsql(_connectionString)
                          .UseExecuteInsertPostgres();
        }
    }

    private class GrandParentEntity
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    private class ParentEntity
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public ICollection<ChildEntity> Children { get; set; } = new List<ChildEntity>();
        public GrandParentEntity GrandParent { get; set; } = null!;
        public int? GrandParentId { get; set; }
    }

    private class ChildEntity
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public int ParentId { get; set; }
        public ParentEntity Parent { get; set; } = null!;
    }

    private class TestDbContextWithNavigation : DbContext
    {
        private readonly string _connectionString;

        public TestDbContextWithNavigation(string connectionString)
        {
            _connectionString = connectionString;
        }

        public DbSet<GrandParentEntity> GrandParentEntities { get; set; } = null!;
        public DbSet<ParentEntity> ParentEntities { get; set; } = null!;
        public DbSet<ChildEntity> ChildEntities { get; set; } = null!;

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseNpgsql(_connectionString)
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

    [Fact]
    public async Task BulkInsertAsync_InsertsEntitiesSuccessfully()
    {
        // Arrange
        var connectionString = _postgresContainer.GetConnectionString();
        await using var context = new TestDbContext(connectionString);
        await context.Database.EnsureDeletedAsync();
        await context.Database.EnsureCreatedAsync();

        var provider = new PostgresBulkInsertProvider();
        var entities = new List<TestEntity>
        {
            new TestEntity { Id = 1, Name = "Entity1" },
            new TestEntity { Id = 2, Name = "Entity2" }
        };

        // Act
        await provider.BulkInsertAsync(context, entities);

        // Assert
        var insertedEntities = context.TestEntities.ToList();
        Assert.Equal(2, insertedEntities.Count);
        Assert.Contains(insertedEntities, e => e.Name == "Entity1");
        Assert.Contains(insertedEntities, e => e.Name == "Entity2");
    }

    [Fact]
    public async Task BulkInsertAsync_DoesNothingWhenEntitiesAreEmpty()
    {
        // Arrange
        var connectionString = _postgresContainer.GetConnectionString();
        await using var context = new TestDbContext(connectionString);
        await context.Database.EnsureCreatedAsync();

        var provider = new PostgresBulkInsertProvider();
        var entities = new List<TestEntity>();

        // Act
        await provider.BulkInsertAsync(context, entities);

        // Assert
        var insertedEntities = context.TestEntities.ToList();
        Assert.Empty(insertedEntities);
    }

    [Fact]
    public async Task BulkInsertAsync_InsertsThousandsOfEntitiesSuccessfully()
    {
        // Arrange
        var connectionString = _postgresContainer.GetConnectionString();
        await using var context = new TestDbContext(connectionString);
        await context.Database.EnsureDeletedAsync();
        await context.Database.EnsureCreatedAsync();

        const int count = 10_000_000;
        var provider = new PostgresBulkInsertProvider();
        var entities = Enumerable.Range(1, count).Select(i => new TestEntity
        {
            Id = i,
            Name = $"Entity{i}"
        }).ToList();

        // Act
        await provider.BulkInsertAsync(context, entities);

        // Assert
        var insertedEntities = context.TestEntities.ToList();
        Assert.Equal(count, insertedEntities.Count);
        Assert.Contains(insertedEntities, e => e.Name == "Entity1");
        Assert.Contains(insertedEntities, e => e.Name == "Entity" + count);
    }

    [Fact]
    public async Task BulkInsertAsync_InsertsEntitiesWithNavigationPropertiesSuccessfully()
    {
        // Arrange
        var connectionString = _postgresContainer.GetConnectionString();
        await using var context = new TestDbContextWithNavigation(connectionString);
        await context.Database.EnsureDeletedAsync();
        await context.Database.EnsureCreatedAsync();

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
        await context.ParentEntities.ExecuteInsert(parents);

        // Assert
        var insertedGrandParents = context.GrandParentEntities.ToList();
        var insertedParents = context.ParentEntities.ToList();
        var insertedChildren = context.ChildEntities.ToList();

        Assert.Equal(2, insertedGrandParents.Count);
        Assert.Equal(2, insertedParents.Count);
        // Assert.Equal(3, insertedChildren.Count);

        Assert.Contains(insertedParents, p => p.Name == "Parent1");
        // Assert.Contains(insertedChildren, c => c.Name == "Child1");
        // Assert.Contains(insertedChildren, c => c.Name == "Child3");
    }
}
