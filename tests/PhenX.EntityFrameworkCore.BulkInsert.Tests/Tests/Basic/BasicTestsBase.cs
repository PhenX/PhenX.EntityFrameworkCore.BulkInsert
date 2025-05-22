using PhenX.EntityFrameworkCore.BulkInsert.Enums;
using PhenX.EntityFrameworkCore.BulkInsert.Extensions;
using PhenX.EntityFrameworkCore.BulkInsert.MySql;
using PhenX.EntityFrameworkCore.BulkInsert.Options;
using PhenX.EntityFrameworkCore.BulkInsert.SqlServer;
using PhenX.EntityFrameworkCore.BulkInsert.Tests.DbContainer;
using PhenX.EntityFrameworkCore.BulkInsert.Tests.DbContext;

using Xunit;

namespace PhenX.EntityFrameworkCore.BulkInsert.Tests.Tests.Basic;

public abstract class BasicTestsBase<TFixture> : IClassFixture<TFixture>, IAsyncLifetime
    where TFixture : TestDbContainer<TestDbContext>
{
    private readonly Guid _run = Guid.NewGuid();
    private TestDbContext _context = null!;

    protected BasicTestsBase(TestDbContainer<TestDbContext> dbContainer)
    {
        DbContainer = dbContainer;
    }

    public async Task InitializeAsync()
    {
        _context = await DbContainer.CreateContextAsync();
    }

    public Task DisposeAsync()
    {
        _context.Dispose();
        return Task.CompletedTask;
    }

    protected TestDbContainer<TestDbContext> DbContainer { get; }

    [Fact]
    public async Task InsertsEntitiesSuccessfully()
    {
        // Arrange
        var entities = new List<TestEntity>
        {
            new TestEntity { TestRun = _run, Name = $"{_run}_Entity1" },
            new TestEntity { TestRun = _run, Name = $"{_run}_Entity2" }
        };

        // Act
        await _context.ExecuteBulkInsertAsync(entities);

        // Assert
        var insertedEntities = _context.TestEntities.Where(x => x.TestRun == _run).ToList();
        Assert.Equal(2, insertedEntities.Count);
        Assert.Contains(insertedEntities, e => e.Name == $"{_run}_Entity1");
        Assert.Contains(insertedEntities, e => e.Name == $"{_run}_Entity2");
    }

    [Fact]
    public void InsertsEntitiesSuccessfully_Sync()
    {
        // Arrange
        var entities = new List<TestEntity>
        {
            new TestEntity { TestRun = _run, Name = $"{_run}_Entity1" },
            new TestEntity { TestRun = _run, Name = $"{_run}_Entity2" }
        };

        // Act
        _context.ExecuteBulkInsert(entities);

        // Assert
        var insertedEntities = _context.TestEntities.Where(x => x.TestRun == _run).ToList();
        Assert.Equal(2, insertedEntities.Count);
        Assert.Contains(insertedEntities, e => e.Name == $"{_run}_Entity1");
        Assert.Contains(insertedEntities, e => e.Name == $"{_run}_Entity2");
    }

    [SkippableFact]
    public async Task InsertsEntitiesAndReturn()
    {
        Skip.If(_context.IsProvider(ProviderType.MySql));

        // Arrange
        var entities = new List<TestEntity>
        {
            new TestEntity { TestRun = _run, Name = $"{_run}_Entity1" },
            new TestEntity { TestRun = _run, Name = $"{_run}_Entity2" }
        };

        // Act
        var insertedEntities = await _context.ExecuteBulkInsertReturnEntitiesAsync(entities);

        // Assert
        Assert.Equal(2, insertedEntities.Count);
        Assert.Contains(insertedEntities, e => e.Name == $"{_run}_Entity1");
        Assert.Contains(insertedEntities, e => e.Name == $"{_run}_Entity2");
    }

    [SkippableFact]
    public void InsertsEntitiesAndReturn_Sync()
    {
        Skip.If(_context.IsProvider(ProviderType.MySql));

        // Arrange
        var entities = new List<TestEntity>
        {
            new TestEntity { TestRun = _run, Name = $"{_run}_Entity1" },
            new TestEntity { TestRun = _run, Name = $"{_run}_Entity2" }
        };

        // Act
        var insertedEntities = _context.ExecuteBulkInsertReturnEntities(entities);

        // Assert
        Assert.Equal(2, insertedEntities.Count);
        Assert.Contains(insertedEntities, e => e.Name == $"{_run}_Entity1");
        Assert.Contains(insertedEntities, e => e.Name == $"{_run}_Entity2");
    }

    [SkippableFact]
    public async Task InsertsEntities_MultipleTimes()
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
        await _context.ExecuteBulkInsertAsync(entities);

        foreach (var entity in entities)
        {
            entity.NumericEnumValue = NumericEnum.Second;
        }

        await _context.ExecuteBulkInsertAsync(entities,
            onConflict: new OnConflictOptions<TestEntity>
            {
                Update = e => e,
            });

        // Assert
        var insertedEntities = _context.TestEntities.Where(x => x.TestRun == _run).ToList();
        Assert.Equal(2, insertedEntities.Count);
        Assert.Contains(insertedEntities, e => e.NumericEnumValue == NumericEnum.Second);
        Assert.Contains(insertedEntities, e => e.NumericEnumValue == NumericEnum.Second);
    }

    [SkippableFact]
    public async Task InsertsEntities_MultipleTimes_With_Conflict_On_Id()
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
        await _context.ExecuteBulkInsertAsync(entities);

        var insertedEntities0 = _context.TestEntities.Where(x => x.TestRun == _run).ToList();
        foreach (var entity in insertedEntities0)
        {
            entity.Name = $"Updated_{entity.Name}";
        }

        await _context.ExecuteBulkInsertAsync(insertedEntities0,
            o => o.CopyGeneratedColumns = true,
            onConflict: new OnConflictOptions<TestEntity>
            {
                Update = e => e,
            });

        // Assert
        var insertedEntities1 = _context.TestEntities.Where(x => x.TestRun == _run).ToList();
        Assert.Equal(2, insertedEntities1.Count);
        Assert.Contains(insertedEntities1, e => e.Name == $"Updated_{_run}_Entity1");
        Assert.Contains(insertedEntities1, e => e.Name == $"Updated_{_run}_Entity2");
    }

    [Fact]
    public async Task InsertsEntitiesMoveRowsSuccessfully()
    {
        // Arrange
        var entities = new List<TestEntity>
        {
            new TestEntity { TestRun = _run, Name = $"{_run}_Entity1" },
            new TestEntity { TestRun = _run, Name = $"{_run}_Entity2" }
        };

        // Act
        await _context.ExecuteBulkInsertAsync(entities, o =>
        {
            o.MoveRows = true;
        });

        // Assert
        var insertedEntities = _context.TestEntities.Where(x => x.TestRun == _run).ToList();
        Assert.Equal(2, insertedEntities.Count);
        Assert.Contains(insertedEntities, e => e.Name == $"{_run}_Entity1");
        Assert.Contains(insertedEntities, e => e.Name == $"{_run}_Entity2");
    }

    [SkippableFact]
    public async Task InsertsEntitiesWithConflict_SingleColumn()
    {
        Skip.If(_context.IsProvider(ProviderType.MySql));

        _context.TestEntities.Add(new TestEntity { TestRun = _run, Name = $"{_run}_Entity1" });
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        // Arrange
        var entities = new List<TestEntity>
        {
            new TestEntity { TestRun = _run, Name = $"{_run}_Entity1" },
            new TestEntity { TestRun = _run, Name = $"{_run}_Entity2" },
        };

        // Act
        await _context.ExecuteBulkInsertAsync(entities, o =>
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
        var insertedEntities = _context.TestEntities.Where(x => x.TestRun == _run).ToList();
        Assert.Equal(2, insertedEntities.Count);
        Assert.Contains(insertedEntities, e => e.Name == $"{_run}_Entity1 - Conflict");
        Assert.Contains(insertedEntities, e => e.Name == $"{_run}_Entity2");
    }

    [SkippableFact]
    public async Task InsertsEntitiesWithConflict_DoNothing()
    {
        Skip.If(_context.IsProvider(ProviderType.MySql));

        _context.TestEntities.Add(new TestEntity { TestRun = _run, Name = $"{_run}_Entity1" });
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        var entities = new List<TestEntity>
        {
            new TestEntity { TestRun = _run, Name = $"{_run}_Entity1" },
            new TestEntity { TestRun = _run, Name = $"{_run}_Entity2" },
        };

        await _context.ExecuteBulkInsertAsync(entities, o =>
        {
            o.MoveRows = true;
        }, new OnConflictOptions<TestEntity>
        {
            Match = e => new { e.Name }
            // Pas de Update => DO NOTHING
        });

        var insertedEntities = _context.TestEntities.Where(x => x.TestRun == _run).ToList();
        Assert.Equal(2, insertedEntities.Count);
        Assert.Contains(insertedEntities, e => e.Name == $"{_run}_Entity1");
        Assert.Contains(insertedEntities, e => e.Name == $"{_run}_Entity2");
    }

    [SkippableFact]
    public async Task InsertsEntitiesWithConflict_Condition()
    {
        Skip.If(_context.IsProvider(ProviderType.MySql));

        _context.TestEntities.Add(new TestEntity { TestRun = _run, Name = $"{_run}_Entity1", Price = 10 });
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        var entities = new List<TestEntity>
        {
            new TestEntity { TestRun = _run, Name = $"{_run}_Entity1", Price = 20 },
            new TestEntity { TestRun = _run, Name = $"{_run}_Entity2", Price = 30 },
        };

        await _context.ExecuteBulkInsertAsync(entities, o =>
        {
            o.MoveRows = true;
        }, new OnConflictOptions<TestEntity>
        {
            Match = e => new { e.Name },
            Update = e => new TestEntity { Price = e.Price },
            Condition = "EXCLUDED.some_price > test_entity.some_price"
        });

        var insertedEntities = _context.TestEntities.Where(x => x.TestRun == _run).ToList();
        Assert.Equal(2, insertedEntities.Count);
        Assert.Contains(insertedEntities, e => e.Name == $"{_run}_Entity1" && e.Price == 20);
        Assert.Contains(insertedEntities, e => e.Name == $"{_run}_Entity2" && e.Price == 30);
    }

    [SkippableFact]
    public async Task InsertsEntitiesWithConflict_MultipleColumns()
    {
        Skip.If(_context.IsProvider(ProviderType.MySql));

        _context.TestEntities.Add(new TestEntity { TestRun = _run, Name = $"{_run}_Entity1", Price = 10 });
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        var entities = new List<TestEntity>
        {
            new TestEntity { TestRun = _run, Name = $"{_run}_Entity1", Price = 20, Identifier = Guid.NewGuid() },
            new TestEntity { TestRun = _run, Name = $"{_run}_Entity2", Price = 30, Identifier = Guid.NewGuid() },
        };

        await _context.ExecuteBulkInsertAsync(entities, o =>
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

        var insertedEntities = _context.TestEntities.Where(x => x.TestRun == _run).ToList();
        Assert.Equal(2, insertedEntities.Count);
        Assert.Equal(1, insertedEntities.Count(e => e.Name == $"{_run}_Entity1 - Conflict"));
        Assert.Contains(insertedEntities, e => e.Name == $"{_run}_Entity2");

        var entity1 = insertedEntities.First(e => e.Name == $"{_run}_Entity1 - Conflict");
        Assert.Equal(0, entity1.Price);
    }

    [Fact]
    public async Task DoesNothingWhenEntitiesAreEmpty()
    {
        // Arrange
        var entities = new List<TestEntity>();

        // Act
        await Assert.ThrowsAsync<InvalidOperationException>(async () => await _context.ExecuteBulkInsertAsync(entities));

        // Assert
        var insertedEntities = _context.TestEntities.Where(x => x.TestRun == _run).ToList();
        Assert.Empty(insertedEntities);
    }

    [Fact]
    public async Task InsertsEntities_Many()
    {
        // Arrange
        const int count = 156055;
        var entities = Enumerable.Range(1, count).Select(i => new TestEntity
        {
            Name = $"{_run}_Entity{i}",
            Price = (decimal)(i * 0.1),
            Identifier = Guid.NewGuid(),
            StringEnumValue = (StringEnum)(i % 2),
            NumericEnumValue = (NumericEnum)(i % 2),
            TestRun = _run,
        }).ToList();

        // Act
        await _context.ExecuteBulkInsertAsync(entities, o =>
        {
            o.MoveRows = false;
        });

        // Assert
        var insertedEntities = _context.TestEntities.Where(x => x.TestRun == _run).ToList();
        Assert.Equal(count, insertedEntities.Count);
        Assert.Contains(insertedEntities, e => e.Name == $"{_run}_Entity1");
        Assert.Contains(insertedEntities, e => e.Name == $"{_run}_Entity" + count);
    }

    [Fact]
    public async Task InsertAndRead_EntityWithValueConverters()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var entities = new List<TestEntityWithConverters>
        {
            new() { TestRun = _run, Name = $"{_run}_Entity1", CreatedAt = now },
            new() { TestRun = _run, Name = $"{_run}_Entity2", CreatedAt = now.AddDays(-1) }
        };

        // Act
        await _context.ExecuteBulkInsertAsync(entities);
        var inserted = _context.TestEntitiesWithConverters.Where(x => x.TestRun == _run).ToList();

        // Assert
        Assert.Equal(2, inserted.Count);
        Assert.Contains(inserted, e => e.Name == $"{_run}_Entity1" && e.CreatedAt == now);
        Assert.Contains(inserted, e => e.Name == $"{_run}_Entity2" && e.CreatedAt == now.AddDays(-1));
    }

    [Fact]
    public async Task BulkInsert_WithOpenTransaction_CommitsSuccessfully()
    {
        // Arrange
        var entities = new List<TestEntity>
        {
            new TestEntity { TestRun = _run, Name = $"{_run}_EntityWithTx1" },
            new TestEntity { TestRun = _run, Name = $"{_run}_EntityWithTx2" }
        };

        await using var transaction = await _context.Database.BeginTransactionAsync();

        await _context.ExecuteBulkInsertAsync(entities);

        await transaction.CommitAsync();

        // Assert
        var insertedEntities = _context.TestEntities.Where(x => x.TestRun == _run).ToList();
        Assert.Contains(insertedEntities, e => e.Name == $"{_run}_EntityWithTx1");
        Assert.Contains(insertedEntities, e => e.Name == $"{_run}_EntityWithTx2");
    }

    [Fact]
    public void BulkInsert_WithOpenTransaction_CommitsSuccessfully_Sync()
    {
        // Arrange
        var entities = new List<TestEntity>
        {
            new TestEntity { TestRun = _run, Name = $"{_run}_EntityWithTx1" },
            new TestEntity { TestRun = _run, Name = $"{_run}_EntityWithTx2" }
        };

        var transaction = _context.Database.BeginTransaction();

        _context.ExecuteBulkInsert(entities);

        transaction.Commit();

        // Assert
        var insertedEntities = _context.TestEntities.Where(x => x.TestRun == _run).ToList();
        Assert.Contains(insertedEntities, e => e.Name == $"{_run}_EntityWithTx1");
        Assert.Contains(insertedEntities, e => e.Name == $"{_run}_EntityWithTx2");
    }

    [Fact]
    public async Task BulkInsert_WithOpenTransaction_RollsBackOnFailure()
    {
        // Arrange
        var entities = new List<TestEntity>
        {
            new TestEntity { TestRun = _run, Name = $"{_run}_EntityWithTxFail1" },
            new TestEntity { TestRun = _run, Name = $"{_run}_EntityWithTxFail2" }
        };

        await using var transaction = await _context.Database.BeginTransactionAsync();

        await _context.ExecuteBulkInsertAsync(entities);

        await transaction.RollbackAsync();

        // Assert
        _context.ChangeTracker.Clear();
        var insertedEntities = _context.TestEntities.Where(x => x.TestRun == _run).ToList();
        Assert.DoesNotContain(insertedEntities, e => e.Name == $"{_run}_EntityWithTxFail1");
        Assert.DoesNotContain(insertedEntities, e => e.Name == $"{_run}_EntityWithTxFail2");
    }

    [Fact]
    public void BulkInsert_WithOpenTransaction_RollsBackOnFailure_Sync()
    {
        // Arrange
        var entities = new List<TestEntity>
        {
            new TestEntity { TestRun = _run, Name = $"{_run}_EntityWithTxFail1" },
            new TestEntity { TestRun = _run, Name = $"{_run}_EntityWithTxFail2" }
        };

        using var transaction = _context.Database.BeginTransaction();

        _context.ExecuteBulkInsert(entities);

        transaction.Rollback();

        // Assert
        _context.ChangeTracker.Clear();
        var insertedEntities = _context.TestEntities.Where(x => x.TestRun == _run).ToList();
        Assert.DoesNotContain(insertedEntities, e => e.Name == $"{_run}_EntityWithTxFail1");
        Assert.DoesNotContain(insertedEntities, e => e.Name == $"{_run}_EntityWithTxFail2");
    }

    [SkippableFact]
    public async Task ThrowsWhenUsingWrongConfigurationType()
    {
        // Skip for providers that don't support this feature
        Skip.If(_context.IsProvider(ProviderType.PostgreSql));
        Skip.If(_context.IsProvider(ProviderType.Sqlite));

        // Arrange
        var entities = new List<TestEntity>
        {
            new TestEntity { TestRun = _run, Name = $"{_run}_Entity1" },
            new TestEntity { TestRun = _run, Name = $"{_run}_Entity2" }
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
    }
}
