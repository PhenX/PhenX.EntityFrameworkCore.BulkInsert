using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using EntityFrameworkCore.ExecuteInsert.Extensions;
using EntityFrameworkCore.ExecuteInsert.Options;
using EntityFrameworkCore.ExecuteInsert.Tests.DbContainer;
using EntityFrameworkCore.ExecuteInsert.Tests.DbContext;

using Xunit;

namespace EntityFrameworkCore.ExecuteInsert.Tests.Tests.Basic;

public abstract class BasicTestsBase : IAsyncLifetime
{
    protected BasicTestsBase(TestDbContainer<TestDbContext> dbContainer)
    {
        DbContainer = dbContainer;
    }

    protected TestDbContainer<TestDbContext> DbContainer { get; }

    [Fact]
    public async Task InsertsEntitiesSuccessfully()
    {
        // Arrange
        var entities = new List<TestEntity>
        {
            new TestEntity { Id = 1, Name = "Entity1" },
            new TestEntity { Id = 2, Name = "Entity2" }
        };

        // Act
        await DbContainer.DbContext.ExecuteInsertWithIdentityAsync(entities);

        // Assert
        var insertedEntities = DbContainer.DbContext.TestEntities.ToList();
        Assert.Equal(2, insertedEntities.Count);
        Assert.Contains(insertedEntities, e => e.Name == "Entity1");
        Assert.Contains(insertedEntities, e => e.Name == "Entity2");
    }

    [Fact]
    public async Task InsertsEntitiesMoveRowsSuccessfully()
    {
        // Arrange
        var entities = new List<TestEntity>
        {
            new TestEntity { Id = 1, Name = "Entity1" },
            new TestEntity { Id = 2, Name = "Entity2" }
        };

        // Act
        await DbContainer.DbContext.ExecuteInsertWithIdentityAsync(entities, o =>
        {
            o.MoveRows = true;
        });

        // Assert
        var insertedEntities = DbContainer.DbContext.TestEntities.ToList();
        Assert.Equal(2, insertedEntities.Count);
        Assert.Contains(insertedEntities, e => e.Name == "Entity1");
        Assert.Contains(insertedEntities, e => e.Name == "Entity2");
    }

    [Fact]
    public async Task InsertsEntitiesWithConflict_SingleColumn()
    {
        DbContainer.DbContext.TestEntities.Add(new TestEntity { Name = "Entity1" });
        await DbContainer.DbContext.SaveChangesAsync();
        DbContainer.DbContext.ChangeTracker.Clear();

        // Arrange
        var entities = new List<TestEntity>
        {
            new TestEntity { Name = "Entity1" },
            new TestEntity { Name = "Entity2" },
        };

        // Act
        await DbContainer.DbContext.ExecuteInsertAsync(entities, o =>
        {
            o.MoveRows = true;
        }, new OnConflictOptions<TestEntity>
        {
            Match = e => new
            {
                e.Name,
            },
            Update = e => new TestEntity
            {
                Name = e.Name + " - Conflict",
            },
        });

        // Assert
        var insertedEntities = DbContainer.DbContext.TestEntities.ToList();
        Assert.Equal(2, insertedEntities.Count);
        Assert.Contains(insertedEntities, e => e.Name == "Entity1 - Conflict");
        Assert.Contains(insertedEntities, e => e.Name == "Entity2");
    }

    [Fact]
    public async Task InsertsEntitiesWithConflict_DoNothing()
    {
        DbContainer.DbContext.TestEntities.Add(new TestEntity { Name = "Entity1" });
        await DbContainer.DbContext.SaveChangesAsync();
        DbContainer.DbContext.ChangeTracker.Clear();

        var entities = new List<TestEntity>
        {
            new TestEntity { Name = "Entity1" },
            new TestEntity { Name = "Entity2" },
        };

        await DbContainer.DbContext.ExecuteInsertAsync(entities, o =>
        {
            o.MoveRows = true;
        }, new OnConflictOptions<TestEntity>
        {
            Match = e => new { e.Name }
            // Pas de Update => DO NOTHING
        });

        var insertedEntities = DbContainer.DbContext.TestEntities.ToList();
        Assert.Equal(2, insertedEntities.Count);
        Assert.Contains(insertedEntities, e => e.Name == "Entity1");
        Assert.Contains(insertedEntities, e => e.Name == "Entity2");
    }

    [SkippableFact]
    public async Task InsertsEntitiesWithConflict_Condition()
    {
        // Skip.If(DbContainer.DbContext.Database.ProviderName!.Contains("Npgsql", StringComparison.InvariantCultureIgnoreCase));

        DbContainer.DbContext.TestEntities.Add(new TestEntity { Name = "Entity1", Price = 10 });
        await DbContainer.DbContext.SaveChangesAsync();
        DbContainer.DbContext.ChangeTracker.Clear();

        var entities = new List<TestEntity>
        {
            new TestEntity { Name = "Entity1", Price = 20 },
            new TestEntity { Name = "Entity2", Price = 30 },
        };

        await DbContainer.DbContext.ExecuteInsertAsync(entities, o =>
        {
            o.MoveRows = true;
        }, new OnConflictOptions<TestEntity>
        {
            Match = e => new { e.Name },
            Update = e => new TestEntity { Price = e.Price },
            Condition = "EXCLUDED.some_price > test_entity.some_price"
        });

        var insertedEntities = DbContainer.DbContext.TestEntities.ToList();
        Assert.Equal(2, insertedEntities.Count);
        Assert.Contains(insertedEntities, e => e.Name == "Entity1" && e.Price == 20);
        Assert.Contains(insertedEntities, e => e.Name == "Entity2" && e.Price == 30);
    }

    [Fact]
    public async Task InsertsEntitiesWithConflict_MultipleColumns()
    {
        DbContainer.DbContext.TestEntities.Add(new TestEntity { Name = "Entity1", Price = 10 });
        await DbContainer.DbContext.SaveChangesAsync();
        DbContainer.DbContext.ChangeTracker.Clear();

        var entities = new List<TestEntity>
        {
            new TestEntity { Name = "Entity1", Price = 20, Identifier = Guid.NewGuid() },
            new TestEntity { Name = "Entity2", Price = 30, Identifier = Guid.NewGuid() },
        };

        await DbContainer.DbContext.ExecuteInsertAsync(entities, o =>
        {
            o.MoveRows = true;
        }, new OnConflictOptions<TestEntity>
        {
            Match = e => new { e.Name },
            Update = e => new TestEntity {
                Name = e.Name + " - Conflict",
                Price = 0,
            }
        });

        var insertedEntities = DbContainer.DbContext.TestEntities.ToList();
        Assert.Equal(2, insertedEntities.Count);
        Assert.Equal(1, insertedEntities.Count(e => e.Name == "Entity1 - Conflict"));
        Assert.Contains(insertedEntities, e => e.Name == "Entity2");

        var entity1 = insertedEntities.First(e => e.Name == "Entity1 - Conflict");
        Assert.Equal(0, entity1.Price);
    }

    [Fact]
    public async Task DoesNothingWhenEntitiesAreEmpty()
    {
        // Arrange
        var entities = new List<TestEntity>();

        // Act
        await Assert.ThrowsAsync<InvalidOperationException>(async () => await DbContainer.DbContext.ExecuteInsertAsync(entities));

        // Assert
        var insertedEntities = DbContainer.DbContext.TestEntities.ToList();
        Assert.Empty(insertedEntities);
    }

    [Fact]
    public async Task InsertsEntities_Many()
    {
        // Arrange
        const int count = 156055;
        var entities = Enumerable.Range(1, count).Select(i => new TestEntity
        {
            Id = i,
            Name = $"Entity{i}",
            Price = (decimal)(i * 0.1),
            Identifier = Guid.NewGuid(),
            StringEnumValue = (StringEnum)(i % 2),
            NumericEnumValue = (NumericEnum)(i % 2),
        }).ToList();

        // Act
        await DbContainer.DbContext.ExecuteInsertAsync(entities, o =>
        {
            o.MoveRows = false;
        });

        // Assert
        var insertedEntities = DbContainer.DbContext.TestEntities.ToList();
        Assert.Equal(count, insertedEntities.Count);
        Assert.Contains(insertedEntities, e => e.Name == "Entity1");
        Assert.Contains(insertedEntities, e => e.Name == "Entity" + count);
    }

    public Task InitializeAsync() => DbContainer.InitializeAsync();

    public Task DisposeAsync() => DbContainer.DisposeAsync();
}
