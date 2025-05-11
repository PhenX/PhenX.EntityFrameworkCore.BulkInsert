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
    public async Task InsertsEntitiesWithConflictSuccessfully()
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
    public async Task InsertsThousandsOfEntitiesSuccessfully()
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
            o.Recursive = false;
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
