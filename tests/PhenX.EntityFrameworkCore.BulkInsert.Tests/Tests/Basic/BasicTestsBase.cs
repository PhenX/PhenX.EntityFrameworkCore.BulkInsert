using PhenX.EntityFrameworkCore.BulkInsert.Extensions;
using PhenX.EntityFrameworkCore.BulkInsert.Options;
using PhenX.EntityFrameworkCore.BulkInsert.Tests.DbContainer;
using PhenX.EntityFrameworkCore.BulkInsert.Tests.DbContext;

using Xunit;

namespace PhenX.EntityFrameworkCore.BulkInsert.Tests.Tests.Basic;

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
        await DbContainer.DbContext.ExecuteBulkInsertReturnEntitiesAsync(entities);

        // Assert
        var insertedEntities = DbContainer.DbContext.TestEntities.ToList();
        Assert.Equal(2, insertedEntities.Count);
        Assert.Contains(insertedEntities, e => e.Name == "Entity1");
        Assert.Contains(insertedEntities, e => e.Name == "Entity2");
    }

    [Fact]
    public void InsertsEntitiesSuccessfully_Sync()
    {
        // Arrange
        var entities = new List<TestEntity>
        {
            new TestEntity { Id = 1, Name = "Entity1" },
            new TestEntity { Id = 2, Name = "Entity2" }
        };

        // Act
        DbContainer.DbContext.ExecuteBulkInsertReturnEntities(entities);

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
        await DbContainer.DbContext.ExecuteBulkInsertReturnEntitiesAsync(entities, o =>
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
        await DbContainer.DbContext.ExecuteBulkInsertAsync(entities, o =>
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

        await DbContainer.DbContext.ExecuteBulkInsertAsync(entities, o =>
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

        await DbContainer.DbContext.ExecuteBulkInsertAsync(entities, o =>
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

        await DbContainer.DbContext.ExecuteBulkInsertAsync(entities, o =>
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
        await Assert.ThrowsAsync<InvalidOperationException>(async () => await DbContainer.DbContext.ExecuteBulkInsertAsync(entities));

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
        await DbContainer.DbContext.ExecuteBulkInsertAsync(entities, o =>
        {
            o.MoveRows = false;
        });

        // Assert
        var insertedEntities = DbContainer.DbContext.TestEntities.ToList();
        Assert.Equal(count, insertedEntities.Count);
        Assert.Contains(insertedEntities, e => e.Name == "Entity1");
        Assert.Contains(insertedEntities, e => e.Name == "Entity" + count);
    }

    [Fact]
    public async Task InsertAndRead_EntityWithValueConverters()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var entities = new List<TestEntityWithConverters>
        {
            new() { Name = "Entity1", CreatedAt = now },
            new() { Name = "Entity2", CreatedAt = now.AddDays(-1) }
        };

        // Act
        await DbContainer.DbContext.ExecuteBulkInsertAsync(entities);
        var inserted = DbContainer.DbContext.TestEntitiesWithConverters.ToList();

        // Assert
        Assert.Equal(2, inserted.Count);
        Assert.Contains(inserted, e => e.Name == "Entity1" && e.CreatedAt == now);
        Assert.Contains(inserted, e => e.Name == "Entity2" && e.CreatedAt == now.AddDays(-1));
    }

    [Fact]
    public async Task BulkInsert_WithOpenTransaction_CommitsSuccessfully()
    {
        // Arrange
        var entities = new List<TestEntity>
        {
            new TestEntity { Name = "EntityWithTx1" },
            new TestEntity { Name = "EntityWithTx2" }
        };

        await using var transaction = await DbContainer.DbContext.Database.BeginTransactionAsync();

        await DbContainer.DbContext.ExecuteBulkInsertAsync(entities);

        await transaction.CommitAsync();

        // Assert
        var insertedEntities = DbContainer.DbContext.TestEntities.ToList();
        Assert.Contains(insertedEntities, e => e.Name == "EntityWithTx1");
        Assert.Contains(insertedEntities, e => e.Name == "EntityWithTx2");
    }

    [Fact]
    public void BulkInsert_WithOpenTransaction_CommitsSuccessfully_Sync()
    {
        // Arrange
        var entities = new List<TestEntity>
        {
            new TestEntity { Name = "EntityWithTx1" },
            new TestEntity { Name = "EntityWithTx2" }
        };

        var transaction = DbContainer.DbContext.Database.BeginTransaction();

        DbContainer.DbContext.ExecuteBulkInsert(entities);

        transaction.Commit();

        // Assert
        var insertedEntities = DbContainer.DbContext.TestEntities.ToList();
        Assert.Contains(insertedEntities, e => e.Name == "EntityWithTx1");
        Assert.Contains(insertedEntities, e => e.Name == "EntityWithTx2");
    }

    [Fact]
    public async Task BulkInsert_WithOpenTransaction_RollsBackOnFailure()
    {
        // Arrange
        var entities = new List<TestEntity>
        {
            new TestEntity { Name = "EntityWithTxFail1" },
            new TestEntity { Name = "EntityWithTxFail2" }
        };

        await using var transaction = await DbContainer.DbContext.Database.BeginTransactionAsync();

        await DbContainer.DbContext.ExecuteBulkInsertAsync(entities);

        await transaction.RollbackAsync();

        // Assert
        DbContainer.DbContext.ChangeTracker.Clear();
        var insertedEntities = DbContainer.DbContext.TestEntities.ToList();
        Assert.DoesNotContain(insertedEntities, e => e.Name == "EntityWithTxFail1");
        Assert.DoesNotContain(insertedEntities, e => e.Name == "EntityWithTxFail2");
    }

    [Fact]
    public void BulkInsert_WithOpenTransaction_RollsBackOnFailure_Sync()
    {
        // Arrange
        var entities = new List<TestEntity>
        {
            new TestEntity { Name = "EntityWithTxFail1" },
            new TestEntity { Name = "EntityWithTxFail2" }
        };

        using var transaction = DbContainer.DbContext.Database.BeginTransaction();

        DbContainer.DbContext.ExecuteBulkInsert(entities);

        transaction.Rollback();

        // Assert
        DbContainer.DbContext.ChangeTracker.Clear();
        var insertedEntities = DbContainer.DbContext.TestEntities.ToList();
        Assert.DoesNotContain(insertedEntities, e => e.Name == "EntityWithTxFail1");
        Assert.DoesNotContain(insertedEntities, e => e.Name == "EntityWithTxFail2");
    }

    public Task InitializeAsync() => DbContainer.InitializeAsync();

    public Task DisposeAsync() => DbContainer.DisposeAsync();
}
