using FluentAssertions;

using PhenX.EntityFrameworkCore.BulkInsert.Enums;
using PhenX.EntityFrameworkCore.BulkInsert.Extensions;
using PhenX.EntityFrameworkCore.BulkInsert.Options;
using PhenX.EntityFrameworkCore.BulkInsert.Tests.DbContainer;
using PhenX.EntityFrameworkCore.BulkInsert.Tests.DbContext;

using Xunit;

namespace PhenX.EntityFrameworkCore.BulkInsert.Tests.Tests.Merge;

public abstract class MergeTestsBase<TDbContext>(IDbContextFactory dbContextFactory) : IAsyncLifetime
    where TDbContext : TestDbContext, new()
{
    private readonly Guid _run = Guid.NewGuid();
    private TDbContext _context = null!;

    public async Task InitializeAsync()
    {
        _context = await dbContextFactory.CreateContextAsync<TDbContext>("basic");
    }

    public Task DisposeAsync()
    {
        _context.Dispose();
        return Task.CompletedTask;
    }

    [SkippableTheory]
    [CombinatorialData]
    public async Task InsertEntities_MultipleTimes(InsertStrategy strategy)
    {
        Skip.If(_context.IsProvider(ProviderType.PostgreSql));
        Skip.If(_context.IsProvider(ProviderType.SqlServer));
        // Oracle MERGE requires match columns to be in the source data; auto-generated Id is not available
        Skip.If(_context.IsProvider(ProviderType.Oracle));

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
                Update = (inserted, excluded) => inserted,
            });

        // Assert
        insertedEntities.Should().BeEquivalentTo(entities,
            o => o.RespectingRuntimeTypes().Excluding(e => e.Id));
    }

    [SkippableTheory]
    [CombinatorialData]
    public async Task InsertEntities_MultipleTimes_WithGuidId(InsertStrategy strategy)
    {
        // Arrange
        var entities = new List<TestEntityWithGuidId>
        {
            new TestEntityWithGuidId { TestRun = _run,Id = Guid.NewGuid(), Name = $"{_run}_Entity1" },
            new TestEntityWithGuidId { TestRun = _run,Id = Guid.NewGuid(), Name = $"{_run}_Entity2" }
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
                Update = (inserted, excluded) => inserted,
            });

        // Assert
        insertedEntities.Should().BeEquivalentTo(entities,
            o => o.RespectingRuntimeTypes().Excluding(e => e.Id));
    }

    [SkippableTheory]
    [CombinatorialData]
    public async Task InsertEntities_MultipleTimes_With_Conflict_On_Id(InsertStrategy strategy)
    {
        // Arrange
        var entities = new List<TestEntity>
        {
            new TestEntity { TestRun = _run, Name = $"{_run}_Entity1" },
            new TestEntity { TestRun = _run, Name = $"{_run}_Entity2" }
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
                Update = (inserted, excluded) => inserted,
            });

        // Assert
        insertedEntities1.Should().BeEquivalentTo(insertedEntities0,
            o => o.RespectingRuntimeTypes().Excluding(e => e.Id));
    }

    [SkippableTheory]
    [InlineData(InsertStrategy.InsertReturn)]
    [InlineData(InsertStrategy.InsertReturnAsync)]
    public async Task InsertEntities_WithConflict_SingleColumn(InsertStrategy strategy)
    {
        Skip.If(_context.IsProvider(ProviderType.MySql));
        // Oracle MERGE does not support returning entities
        Skip.If(_context.IsProvider(ProviderType.Oracle));

        // Arrange
        _context.TestEntities.Add(new TestEntity { TestRun = _run,Name = $"{_run}_Entity1" });
        _context.SaveChanges();
        _context.ChangeTracker.Clear();

        var entities = new List<TestEntity>
        {
            new TestEntity { TestRun = _run, Name = $"{_run}_Entity1" },
            new TestEntity { TestRun = _run, Name = $"{_run}_Entity2" },
        };

        // Act
        var insertedEntities = await _context.InsertWithStrategyAsync(strategy, entities, _ =>
        {}, new OnConflictOptions<TestEntity>
        {
            Match = e => new
            {
                e.Name,
            },
            Update = (inserted, excluded) => new TestEntity
            {
                Name = inserted.Name + " - Conflict",
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
        var insertedEntities = await _context.InsertWithStrategyAsync(strategy, entities, _ => {}, new OnConflictOptions<TestEntity>
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
    public async Task InsertEntities_WithConflict_RawCondition(InsertStrategy strategy)
    {
        Skip.If(_context.IsProvider(ProviderType.MySql));
        // Oracle MERGE does not support returning entities
        Skip.If(_context.IsProvider(ProviderType.Oracle));

        // Arrange
        _context.TestEntities.Add(new TestEntity { TestRun = _run, Name = $"{_run}_Entity1", Price = 10 });
        _context.SaveChanges();
        _context.ChangeTracker.Clear();

        var entities = new List<TestEntity>
        {
            new TestEntity { TestRun = _run, Name = $"{_run}_Entity1", Price = 20 },
            new TestEntity { TestRun = _run, Name = $"{_run}_Entity2", Price = 600 },
        };

        await _context.ExecuteBulkInsertAsync(entities, onConflict: new OnConflictOptions<TestEntity>
        {
            Match = e => new
            {
                e.Name,
            }
        });

        // Act
        var insertedEntities = await _context.InsertWithStrategyAsync(strategy, entities, _ => {}, new OnConflictOptions<TestEntity>
        {

            Match = e => new { e.Name },
            Update = (inserted, excluded) => new TestEntity
            {
                Price = excluded.Price + inserted.Price,
            },
            RawWhere = (insertedTable, excludedTable) => $"{excludedTable}.some_price != {insertedTable}.some_price",
        });

        // Assert
        Assert.Single(insertedEntities);
        Assert.Contains(insertedEntities, e => e.Name == $"{_run}_Entity1" && e.Price == 30);
    }

    [SkippableTheory]
    [InlineData(InsertStrategy.InsertReturn)]
    [InlineData(InsertStrategy.InsertReturnAsync)]
    public async Task InsertEntities_WithConflict_ExpressionCondition(InsertStrategy strategy)
    {
        Skip.If(_context.IsProvider(ProviderType.MySql));
        // Oracle MERGE does not support returning entities
        Skip.If(_context.IsProvider(ProviderType.Oracle));

        // Arrange
        _context.TestEntities.Add(new TestEntity { TestRun = _run, Name = $"{_run}_Entity1", Price = 10 });
        _context.SaveChanges();
        _context.ChangeTracker.Clear();

        var entities = new List<TestEntity>
        {
            new TestEntity { TestRun = _run, Name = $"{_run}_Entity1", Price = 20 },
            new TestEntity { TestRun = _run, Name = $"{_run}_Entity2", Price = 600 },
        };

        await _context.ExecuteBulkInsertAsync(entities, onConflict: new OnConflictOptions<TestEntity>
        {
            Match = e => new
            {
                e.Name,
            }
        });

        // Act
        var insertedEntities = await _context.InsertWithStrategyAsync(strategy, entities, _ => {}, new OnConflictOptions<TestEntity>
        {

            Match = e => new { e.Name },
            Update = (inserted, excluded) => new TestEntity
            {
                Price = excluded.Price + inserted.Price,
            },
            Where = (inserted, excluded) => excluded.Price != inserted.Price,
        });

        // Assert
        Assert.Single(insertedEntities);
        Assert.Contains(insertedEntities, e => e.Name == $"{_run}_Entity1" && e.Price == 30);
    }

    [SkippableTheory]
    [InlineData(InsertStrategy.InsertReturn)]
    [InlineData(InsertStrategy.InsertReturnAsync)]
    public async Task InsertEntities_WithConflict_ComplexExpressionCondition(InsertStrategy strategy)
    {
        Skip.If(_context.IsProvider(ProviderType.MySql));
        // Oracle MERGE does not support returning entities
        Skip.If(_context.IsProvider(ProviderType.Oracle));

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
        var insertedEntities = await _context.InsertWithStrategyAsync(strategy, entities, _ => {}, new OnConflictOptions<TestEntity>
        {
            Match = e => new { e.Name },
            Update = (inserted, excluded) => new TestEntity { Price = (excluded.Price > 15 ? 15 : 10) },
            Where = (inserted, excluded) => excluded.Price > inserted.Price && inserted.Name.Trim().Contains("Entity1"),
        });

        // Assert
        Assert.Equal(2, insertedEntities.Count);
        Assert.Contains(insertedEntities, e => e.Name == $"{_run}_Entity1" && e.Price == 15);
        Assert.Contains(insertedEntities, e => e.Name == $"{_run}_Entity2" && e.Price == 30);
    }

    [SkippableTheory]
    [InlineData(InsertStrategy.InsertReturn)]
    [InlineData(InsertStrategy.InsertReturnAsync)]
    public async Task InsertEntities_WithConflict_MultipleColumns(InsertStrategy strategy)
    {
        Skip.If(_context.IsProvider(ProviderType.MySql));
        // Oracle MERGE does not support returning entities
        Skip.If(_context.IsProvider(ProviderType.Oracle));

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
        var insertedEntities = await _context.InsertWithStrategyAsync(strategy, entities, _ => {}, new OnConflictOptions<TestEntity>
        {
            Match = e => new { e.Name },
            Update = (inserted, excluded) => new TestEntity { Name = inserted.Name + " - Conflict", Price = 0 }
        });

        // Assert
        Assert.Equal(2, insertedEntities.Count);
        Assert.Contains(insertedEntities, e => e.Name == $"{_run}_Entity2");
        Assert.Contains(insertedEntities, e => e.Name == $"{_run}_Entity1 - Conflict" && e.Price == 0);
    }

    [SkippableTheory]
    [InlineData(InsertStrategy.InsertReturn)]
    [InlineData(InsertStrategy.InsertReturnAsync)]
    public async Task InsertEntities_WithComplexType_UpdateAll(InsertStrategy strategy)
    {
        Skip.If(_context.IsProvider(ProviderType.MySql));
        // Oracle MERGE does not support returning entities
        Skip.If(_context.IsProvider(ProviderType.Oracle));

        // Arrange
        var entities = new List<TestEntityWithComplexType>
        {
            new TestEntityWithComplexType
            {
                TestRun = _run,
                OwnedComplexType = new OwnedObject { Code = 1, Name = "Name1" }
            },
            new TestEntityWithComplexType
            {
                TestRun = _run,
                OwnedComplexType = new OwnedObject { Code = 2, Name = "Name2" }
            }
        };

        // Act - First insert (without CopyGeneratedColumns - returns generated IDs via RETURNING)
        var insertedEntities = await _context.InsertWithStrategyAsync(strategy, entities);

        // Update the complex properties
        foreach (var entity in insertedEntities)
        {
            entity.OwnedComplexType = new OwnedObject
            {
                Code = entity.OwnedComplexType.Code + 100,
                Name = $"Updated_{entity.OwnedComplexType.Name}"
            };
        }

        // Act - Second insert with update on conflict
        // The ParameterExpression case in GetUpdates generates UPDATE statements for all columns
        var updatedEntities = await _context.InsertWithStrategyAsync(strategy, insertedEntities, o => o.CopyGeneratedColumns = true,
            onConflict: new OnConflictOptions<TestEntityWithComplexType>
            {
                Update = (inserted, excluded) => inserted,
            });

        // Assert - complex properties should be updated
        Assert.Equal(2, updatedEntities.Count);
        Assert.All(updatedEntities, e =>
        {
            Assert.StartsWith("Updated_", e.OwnedComplexType.Name);
            Assert.True(e.OwnedComplexType.Code > 100);
        });
    }

    [SkippableTheory]
    [InlineData(InsertStrategy.InsertReturn)]
    [InlineData(InsertStrategy.InsertReturnAsync)]
    public async Task InsertEntities_WithComplexType_UpdateWithWhere(InsertStrategy strategy)
    {
        Skip.If(_context.IsProvider(ProviderType.MySql));
        // Oracle MERGE does not support returning entities
        Skip.If(_context.IsProvider(ProviderType.Oracle));

        // Arrange - initial Code values are 10 and 20
        var entities = new List<TestEntityWithComplexType>
        {
            new TestEntityWithComplexType
            {
                TestRun = _run,
                OwnedComplexType = new OwnedObject { Code = 10, Name = "Original1" }
            },
            new TestEntityWithComplexType
            {
                TestRun = _run,
                OwnedComplexType = new OwnedObject { Code = 20, Name = "Original2" }
            }
        };

        // Act - First insert (without CopyGeneratedColumns - returns generated IDs via RETURNING)
        var insertedEntities = await _context.InsertWithStrategyAsync(strategy, entities);

        // Update the complex property - new Code values will be original + 100 (110 and 120)
        foreach (var entity in insertedEntities)
        {
            entity.OwnedComplexType.Name = $"Changed_{entity.OwnedComplexType.Name}";
            entity.OwnedComplexType.Code = entity.OwnedComplexType.Code + 100;
        }

        // Act - Second insert updating complex properties with a WHERE condition
        // This tests that complex property access works correctly in the Where clause
        var updatedEntities = await _context.InsertWithStrategyAsync(strategy, insertedEntities, o => o.CopyGeneratedColumns = true,
            onConflict: new OnConflictOptions<TestEntityWithComplexType>
            {
                Update = (inserted, excluded) => inserted,
                Where = (inserted, excluded) => excluded.OwnedComplexType.Code > inserted.OwnedComplexType.Code
            });

        // Assert - entities should be updated because the new Code values (110, 120)
        // are greater than the existing values in the database (10, 20)
        Assert.Equal(2, updatedEntities.Count);
        Assert.All(updatedEntities, e =>
        {
            Assert.StartsWith("Changed_", e.OwnedComplexType.Name);
            Assert.True(e.OwnedComplexType.Code > 100);
        });
    }

    [SkippableTheory]
    [InlineData(InsertStrategy.InsertReturn)]
    [InlineData(InsertStrategy.InsertReturnAsync)]
    public async Task InsertEntities_WithComplexType_UpdateComplexPropertyConditionally(InsertStrategy strategy)
    {
        Skip.If(_context.IsProvider(ProviderType.MySql));
        // Oracle MERGE does not support returning entities
        Skip.If(_context.IsProvider(ProviderType.Oracle));

        // Arrange - Create entities with different Code values
        var entities = new List<TestEntityWithComplexType>
        {
            new TestEntityWithComplexType
            {
                TestRun = _run,
                OwnedComplexType = new OwnedObject { Code = 50, Name = "LowCode" }
            },
            new TestEntityWithComplexType
            {
                TestRun = _run,
                OwnedComplexType = new OwnedObject { Code = 150, Name = "HighCode" }
            }
        };

        // Act - First insert (returns entities with generated IDs)
        var insertedEntities = await _context.InsertWithStrategyAsync(strategy, entities);

        // Update both entities with new values
        foreach (var entity in insertedEntities)
        {
            entity.OwnedComplexType.Code = entity.OwnedComplexType.Code + 10;
            entity.OwnedComplexType.Name = $"Modified_{entity.OwnedComplexType.Name}";
        }

        // Act - Update using nested MemberInitExpression for complex property assignment
        // Note: entities with Code >= 100 (original value) will not be updated due to WHERE clause
        var updatedEntities = await _context.InsertWithStrategyAsync(strategy, insertedEntities,
            o => o.CopyGeneratedColumns = true,
            onConflict: new OnConflictOptions<TestEntityWithComplexType>
            {
                Update = (inserted, excluded) => new TestEntityWithComplexType
                {
                    OwnedComplexType = new OwnedObject
                    {
                        Code = excluded.OwnedComplexType.Code,
                        Name = excluded.OwnedComplexType.Name
                    }
                },
                Where = (inserted, excluded) => inserted.OwnedComplexType.Code < 100
            });

        // Assert - Only the entity with original Code < 100 should be updated (Code was 50, now 60)
        // The one with original Code >= 100 is not updated but is also not returned by RETURNING clause
        Assert.Single(updatedEntities);
        var updatedEntity = updatedEntities.Single();
        Assert.Equal(60, updatedEntity.OwnedComplexType.Code);
        Assert.Equal("Modified_LowCode", updatedEntity.OwnedComplexType.Name);
    }
}
