using FluentAssertions;

using PhenX.EntityFrameworkCore.BulkInsert.Enums;
using PhenX.EntityFrameworkCore.BulkInsert.Extensions;
using PhenX.EntityFrameworkCore.BulkInsert.MySql;
using PhenX.EntityFrameworkCore.BulkInsert.Options;
using PhenX.EntityFrameworkCore.BulkInsert.SqlServer;
using PhenX.EntityFrameworkCore.BulkInsert.Tests.DbContainer;
using PhenX.EntityFrameworkCore.BulkInsert.Tests.DbContext;

using Xunit;

namespace PhenX.EntityFrameworkCore.BulkInsert.Tests.Tests.Basic;

public abstract class BasicTestsBase<TDbContext>(TestDbContainer dbContainer) : IAsyncLifetime
    where TDbContext : TestDbContext, new()
{
    private readonly Guid _run = Guid.NewGuid();
    private TDbContext _context = null!;

    public async Task InitializeAsync()
    {
        _context = await dbContainer.CreateContextAsync<TDbContext>("basic");
    }

    public Task DisposeAsync()
    {
        _context.Dispose();
        return Task.CompletedTask;
    }

    [SkippableTheory]
    [CombinatorialData]
    public async Task InsertsEntities(InsertStrategy strategy)
    {
        // Arrange
        var entities = new List<TestEntity>
        {
            new TestEntity { Name = $"{_run}_Entity1" },
            new TestEntity { Name = $"{_run}_Entity2" }
        };

        // Act
        var insertedEntities = await _context.InsertWithStrategyAsync(strategy, entities);

        // Assert
        insertedEntities.Should().BeEquivalentTo(entities,
            o => o.RespectingRuntimeTypes().Excluding((TestEntity e) => e.Id));
    }

    [SkippableTheory]
    [CombinatorialData]
    public async Task InsertEntities_WithJson(InsertStrategy strategy)
    {
        // Arrange
        var entities = new List<TestEntityWithJson>
        {
            new TestEntityWithJson { Json = [1] },
            new TestEntityWithJson { Json = [2] }
        };

        // Act
        var insertedEntities = await _context.InsertWithStrategyAsync(strategy, entities);

        // Assert
        insertedEntities.Should().BeEquivalentTo(entities,
            o => o.RespectingRuntimeTypes().Excluding((TestEntityWithJson e) => e.Id));
    }

    [SkippableFact]
    public async Task InsertEntities_AndReturn_AsyncEnumerable()
    {
        Skip.If(_context.IsProvider(ProviderType.MySql));

        // Arrange
        var entities = new List<TestEntity>
        {
            new TestEntity { TestRun = _run, Name = $"{_run}_Entity1" },
            new TestEntity { TestRun = _run, Name = $"{_run}_Entity2" }
        };

        // Act
        var enumerable = _context.ExecuteBulkInsertReturnEnumerableAsync(entities);

        var insertedEntities = new List<TestEntity>();
        await foreach (var item in enumerable)
        {
            insertedEntities.Add(item);
        }

        // Assert
        insertedEntities.Should().BeEquivalentTo(entities,
            o => o.RespectingRuntimeTypes().Excluding((TestEntity e) => e.Id));
    }

    [SkippableTheory]
    [CombinatorialData]
    public async Task InsertEntities_MultipleTimes(InsertStrategy strategy)
    {
        Skip.If(_context.IsProvider(ProviderType.PostgreSql));
        Skip.If(_context.IsProvider(ProviderType.SqlServer));

        // Arrange
        var entities = new List<TestEntity>
        {
            new TestEntity { TestRun = _run, Name = $"{_run}_Entity1" },
            new TestEntity { TestRun = _run, Name = $"{_run}_Entity2" }
        };

        // Act
        await _context.InsertWithStrategyAsync(strategy, entities);

        foreach (var entity in entities)
        {
            entity.NumericEnumValue = NumericEnum.Second;
        }

        var insertedEntities = await _context.InsertWithStrategyAsync(strategy, entities,
            onConflict: new OnConflictOptions<TestEntity>
            {
                Update = e => e,
            });

        // Assert
        insertedEntities.Should().BeEquivalentTo(entities,
            o => o.RespectingRuntimeTypes().Excluding((TestEntity e) => e.Id));
    }

    [SkippableTheory]
    [CombinatorialData]
    public async Task InsertEntities_MultipleTimes_WithGuidId(InsertStrategy strategy)
    {
        // Arrange
        var entities = new List<TestEntityWithGuidId>
        {
            new TestEntityWithGuidId { Id = Guid.NewGuid(), Name = $"{_run}_Entity1" },
            new TestEntityWithGuidId { Id = Guid.NewGuid(), Name = $"{_run}_Entity2" }
        };

        // Act
        await _context.ExecuteBulkInsertAsync(entities);

        foreach (var entity in entities)
        {
            entity.Name = $"Updated_{entity.Name}";
        }

        var insertedEntities = await _context.InsertWithStrategyAsync(strategy, entities,
            onConflict: new OnConflictOptions<TestEntityWithGuidId>
            {
                Update = e => e,
            });

        // Assert
        insertedEntities.Should().BeEquivalentTo(entities,
            o => o.RespectingRuntimeTypes().Excluding((TestEntityWithGuidId e) => e.Id));
    }

    [SkippableTheory]
    [CombinatorialData]
    public async Task InsertEntities_MultipleTimes_With_Conflict_On_Id(InsertStrategy strategy)
    {
        // Arrange
        var entities = new List<TestEntity>
        {
            new TestEntity { Name = $"{_run}_Entity1" },
            new TestEntity { Name = $"{_run}_Entity2" }
        };

        // Act
        var insertedEntities0 = await _context.InsertWithStrategyAsync(strategy, entities);
        foreach (var entity in insertedEntities0)
        {
            entity.Name = $"Updated_{entity.Name}";
        }

        var insertedEntities1 = await _context.InsertWithStrategyAsync(strategy, insertedEntities0,
            o => o.CopyGeneratedColumns = true,
            onConflict: new OnConflictOptions<TestEntity>
            {
                Update = e => e,
            });

        // Assert
        insertedEntities1.Should().BeEquivalentTo(insertedEntities0,
            o => o.RespectingRuntimeTypes().Excluding((TestEntity e) => e.Id));
    }

    [SkippableTheory]
    [CombinatorialData]
    public async Task InsertEntities_MoveRows(InsertStrategy strategy)
    {
        // Arrange
        var entities = new List<TestEntity>
        {
            new TestEntity { Name = $"{_run}_Entity1" },
            new TestEntity { Name = $"{_run}_Entity2" }
        };

        // Act
        var insertedEntities = await _context.InsertWithStrategyAsync(strategy, entities, o =>
        {
            o.MoveRows = true;
        });

        // Assert
        insertedEntities.Should().BeEquivalentTo(entities,
            o => o.RespectingRuntimeTypes().Excluding((TestEntity e) => e.Id));
    }

    [SkippableTheory]
    [InlineData(InsertStrategy.InsertReturn)]
    [InlineData(InsertStrategy.InsertReturnAsync)]
    public async Task InsertEntities_WithConflict_SingleColumn(InsertStrategy strategy)
    {
        Skip.If(_context.IsProvider(ProviderType.MySql));

        // Arrange
        _context.TestEntities.Add(new TestEntity { Name = $"{_run}_Entity1" });
        _context.SaveChanges();
        _context.ChangeTracker.Clear();

        var entities = new List<TestEntity>
        {
            new TestEntity { Name = $"{_run}_Entity1" },
            new TestEntity { TestRun = _run, Name = $"{_run}_Entity2" },
        };

        // Act
        var insertedEntities = await _context.InsertWithStrategyAsync(strategy, entities, o =>
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
        Assert.Equal(2, insertedEntities.Count);
        Assert.Contains(insertedEntities, e => e.Name == $"{_run}_Entity1 - Conflict");
        Assert.Contains(insertedEntities, e => e.Name == $"{_run}_Entity2");
    }

    [SkippableTheory]
    [InlineData(InsertStrategy.Insert)]
    [InlineData(InsertStrategy.InsertAsync)]
    public async Task InsertEntities_WithConflict_DoNothing(InsertStrategy strategy)
    {
        Skip.If(_context.IsProvider(ProviderType.MySql));

        // Arrange
        _context.TestEntities.Add(new TestEntity { TestRun = _run, Name = $"{_run}_Entity1" });
        _context.SaveChanges();
        _context.ChangeTracker.Clear();

        var entities = new List<TestEntity>
        {
            new TestEntity { TestRun = _run, Name = $"{_run}_Entity1" },
            new TestEntity { TestRun = _run, Name = $"{_run}_Entity2" },
        };

        // Act
        var insertedEntities = await _context.InsertWithStrategyAsync(strategy, entities, o =>
        {
            o.MoveRows = true;
        }, new OnConflictOptions<TestEntity>
        {
            Match = e => new { e.Name }
        });

        // Assert
        Assert.Equal(2, insertedEntities.Count);
        Assert.Contains(insertedEntities, e => e.Name == $"{_run}_Entity1");
        Assert.Contains(insertedEntities, e => e.Name == $"{_run}_Entity2");
    }

    [SkippableTheory]
    [InlineData(InsertStrategy.InsertReturn)]
    [InlineData(InsertStrategy.InsertReturnAsync)]
    public async Task InsertEntities_WithConflict_Condition(InsertStrategy strategy)
    {
        Skip.If(_context.IsProvider(ProviderType.MySql));

        // Arrange
        _context.TestEntities.Add(new TestEntity { TestRun = _run, Name = $"{_run}_Entity1", Price = 10 });
        _context.SaveChanges();
        _context.ChangeTracker.Clear();

        var entities = new List<TestEntity>
        {
            new TestEntity { TestRun = _run, Name = $"{_run}_Entity1", Price = 20 },
            new TestEntity { TestRun = _run, Name = $"{_run}_Entity2", Price = 30 },
        };

        // Act
        var insertedEntities = await _context.InsertWithStrategyAsync(strategy, entities, o =>
        {
            o.MoveRows = true;
        }, new OnConflictOptions<TestEntity>
        {
            Match = e => new { e.Name },
            Update = e => new TestEntity { Price = e.Price },
            Condition = "EXCLUDED.some_price > test_entity.some_price"
        });

        // Assert
        Assert.Equal(2, insertedEntities.Count);
        Assert.Contains(insertedEntities, e => e.Name == $"{_run}_Entity1" && e.Price == 20);
        Assert.Contains(insertedEntities, e => e.Name == $"{_run}_Entity2" && e.Price == 30);
    }

    [SkippableTheory]
    [InlineData(InsertStrategy.InsertReturn)]
    [InlineData(InsertStrategy.InsertReturnAsync)]
    public async Task InsertEntities_WithConflict_MultipleColumns(InsertStrategy strategy)
    {
        Skip.If(_context.IsProvider(ProviderType.MySql));

        // Arrange
        _context.TestEntities.Add(new TestEntity { TestRun = _run, Name = $"{_run}_Entity1", Price = 10 });
        _context.SaveChanges();
        _context.ChangeTracker.Clear();

        var entities = new List<TestEntity>
        {
            new TestEntity { TestRun = _run, Name = $"{_run}_Entity1", Price = 20, Identifier = Guid.NewGuid() },
            new TestEntity { TestRun = _run, Name = $"{_run}_Entity2", Price = 30, Identifier = Guid.NewGuid() },
        };

        // Act
        var insertedEntities = await _context.InsertWithStrategyAsync(strategy, entities, o =>
        {
            o.MoveRows = true;
        }, new OnConflictOptions<TestEntity>
        {
            Match = e => new { e.Name },
            Update = e => new TestEntity { Name = e.Name + " - Conflict", Price = 0 }
        });

        // Assert
        Assert.Equal(2, insertedEntities.Count);
        Assert.Contains(insertedEntities, e => e.Name == $"{_run}_Entity2");
        Assert.Contains(insertedEntities, e => e.Name == $"{_run}_Entity1 - Conflict" && e.Price == 0);
    }

    [SkippableTheory]
    [CombinatorialData]
    public async Task InsertEntities_DoesNothing_WhenEntitiesAreEmpty(InsertStrategy strategy)
    {
        // Arrange
        var entities = new List<TestEntity>();

        // Act
        await Assert.ThrowsAsync<InvalidOperationException>(async () => await _context.InsertWithStrategyAsync(strategy, entities));

        // Assert
        var insertedEntities = _context.TestEntities.Where(x => x.TestRun == _run).ToList();
        Assert.Empty(insertedEntities);
    }

    [SkippableTheory]
    [CombinatorialData]
    public async Task InsertEntities_Many(InsertStrategy strategy)
    {
        // Arrange
        const int count = 156055;

        var entities = Enumerable.Range(1, count).Select(i => new TestEntity
        {
            Identifier = Guid.NewGuid(),
            Name = $"{_run}_Entity{i}",
            NumericEnumValue = (NumericEnum)(i % 2),
            Price = (decimal)(i * 0.1),
            StringEnumValue = (StringEnum)(i % 2),
        }).ToList();

        // Act
        var insertedEntities = await _context.InsertWithStrategyAsync(strategy, entities, o =>
        {
            o.MoveRows = false;
        });

        // Assert
        Assert.Equal(count, insertedEntities.Count);
        Assert.Contains(insertedEntities, e => e.Name == $"{_run}_Entity1");
        Assert.Contains(insertedEntities, e => e.Name == $"{_run}_Entity" + count);
    }

    [SkippableTheory]
    [CombinatorialData]
    public async Task InsertEntities_AndReturn_WithEntityWithValueConverters(InsertStrategy strategy)
    {
        // Arrange
        var now = DateTime.UtcNow;

        var entities = new List<TestEntityWithConverters>
        {
            new TestEntityWithConverters() { Name = $"{_run}_Entity1", CreatedAt = now },
            new TestEntityWithConverters() { Name = $"{_run}_Entity2", CreatedAt = now.AddDays(-1) }
        };

        // Act
        var insertedEntities = await _context.InsertWithStrategyAsync(strategy, entities);

        // Assert
        insertedEntities.Should().BeEquivalentTo(entities,
            o => o.RespectingRuntimeTypes().Excluding((TestEntityWithConverters e) => e.Id));
    }

    [SkippableTheory]
    [CombinatorialData]
    public async Task InsertEntities_WithOpenTransaction_CommitsSuccessfully(InsertStrategy strategy)
    {
        // Arrange
        var entities = new List<TestEntity>
        {
            new TestEntity { TestRun = _run, Name = $"{_run}_EntityWithTx1" },
            new TestEntity { TestRun = _run, Name = $"{_run}_EntityWithTx2" }
        };

        // Act
        await using var transaction = await _context.Database.BeginTransactionAsync();
        var insertedEntities = await _context.InsertWithStrategyAsync(strategy, entities);
        await transaction.CommitAsync();

        // Assert
        insertedEntities.Should().BeEquivalentTo(entities,
            o => o.RespectingRuntimeTypes().Excluding((TestEntity e) => e.Id));
    }

    [SkippableTheory]
    [CombinatorialData]
    public async Task InsertEntities_WithOpenTransaction_RollsBackOnFailure(InsertStrategy strategy)
    {
        // Arrange
        var entities = new List<TestEntity>
        {
            new TestEntity { TestRun = _run, Name = $"{_run}_EntityWithTxFail1" },
            new TestEntity { TestRun = _run, Name = $"{_run}_EntityWithTxFail2" }
        };

        await using var transaction = await _context.Database.BeginTransactionAsync();
        await _context.InsertWithStrategyAsync(strategy, entities);
        await transaction.RollbackAsync();

        // Assert
        _context.ChangeTracker.Clear();

        var insertedEntities = _context.TestEntities.Where(x => x.TestRun == _run).ToList();
        Assert.Empty(insertedEntities);
    }
    
    [Fact]
    public async Task ThrowsWhenUsingWrongConfigurationType()
    {
        // Skip for providers that don't support this feature
        Skip.If(_context.IsProvider(ProviderType.Sqlite));

        // Arrange
        var entities = new List<TestEntity>
        {
            new TestEntity { Name = $"{_run}_Entity1" },
            new TestEntity { Name = $"{_run}_Entity2" }
        };

        // Act & Assert
        if (_context.IsProvider(ProviderType.SqlServer))
        {
            await Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await _context.ExecuteBulkInsertAsync(entities, (MySqlBulkInsertOptions o) =>
                {
                }));
        }

        if (_context.IsProvider(ProviderType.MySql))
        {
            await Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await _context.ExecuteBulkInsertAsync(entities, (SqlServerBulkInsertOptions o) =>
                {
                }));
        }

        if (_context.IsProvider(ProviderType.PostgreSql))
        {
            await Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await _context.ExecuteBulkInsertAsync(entities, (SqlServerBulkInsertOptions o) =>
                {
                }));
        }
    }
}
