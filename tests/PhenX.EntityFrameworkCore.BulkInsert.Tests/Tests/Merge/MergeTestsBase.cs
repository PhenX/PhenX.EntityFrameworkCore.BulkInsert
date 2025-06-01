using FluentAssertions;

using PhenX.EntityFrameworkCore.BulkInsert.Enums;
using PhenX.EntityFrameworkCore.BulkInsert.Extensions;
using PhenX.EntityFrameworkCore.BulkInsert.Options;
using PhenX.EntityFrameworkCore.BulkInsert.Tests.DbContainer;
using PhenX.EntityFrameworkCore.BulkInsert.Tests.DbContext;

using Xunit;

namespace PhenX.EntityFrameworkCore.BulkInsert.Tests.Tests.Merge;

public abstract class MergeTestsBase<TDbContext>(TestDbContainer dbContainer) : IAsyncLifetime
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
            o => o.RespectingRuntimeTypes().Excluding(e => e.Id));
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
            o => o.RespectingRuntimeTypes().Excluding(e => e.Id));
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
            o => o.RespectingRuntimeTypes().Excluding(e => e.Id));
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
        var insertedEntities = await _context.InsertWithStrategyAsync(strategy, entities, _ =>
        {}, new OnConflictOptions<TestEntity>
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

        // Arrange
        _context.TestEntities.Add(new TestEntity { TestRun = _run, Name = $"{_run}_Entity1", Price = 10 });
        _context.SaveChanges();
        _context.ChangeTracker.Clear();

        var entities = new List<TestEntity>
        {
            new TestEntity { TestRun = _run, Name = $"{_run}_Entity1", Price = 20 },
            new TestEntity { TestRun = _run, Name = $"{_run}_Entity2", Price = 30 },
        };

        await _context.ExecuteBulkInsertAsync(entities, onConflict: new OnConflictOptions<TestEntity>
        {
            Match = e => new
            {
                e.Name,
                // ...other columns to match on
            }
        });

        // Act
        var insertedEntities = await _context.InsertWithStrategyAsync(strategy, entities, _ => {}, new OnConflictOptions<TestEntity>
        {

            Match = e => new { e.Name },
            Update = e => new TestEntity { Price = e.Price },
            RawWhere = "EXCLUDED.some_price > INSERTED.some_price"
        });

        // Assert
        Assert.Equal(2, insertedEntities.Count);
        Assert.Contains(insertedEntities, e => e.Name == $"{_run}_Entity1" && e.Price == 20);
        Assert.Contains(insertedEntities, e => e.Name == $"{_run}_Entity2" && e.Price == 30);
    }

    [SkippableTheory]
    [InlineData(InsertStrategy.InsertReturn)]
    [InlineData(InsertStrategy.InsertReturnAsync)]
    public async Task InsertEntities_WithConflict_ExpressionCondition(InsertStrategy strategy)
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
        var insertedEntities = await _context.InsertWithStrategyAsync(strategy, entities, _ => {}, new OnConflictOptions<TestEntity>
        {
            Match = e => new { e.Name },
            Update = e => new TestEntity { Price = e.Price },
            Where = (inserted, excluded) => excluded.Price > inserted.Price  && excluded.Price > 15 ? inserted.Name.Trim().Contains("Entity1") : inserted.Name.Trim().Contains("Entity2"),
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
        var insertedEntities = await _context.InsertWithStrategyAsync(strategy, entities, _ => {}, new OnConflictOptions<TestEntity>
        {
            Match = e => new { e.Name },
            Update = e => new TestEntity { Name = e.Name + " - Conflict", Price = 0 }
        });

        // Assert
        Assert.Equal(2, insertedEntities.Count);
        Assert.Contains(insertedEntities, e => e.Name == $"{_run}_Entity2");
        Assert.Contains(insertedEntities, e => e.Name == $"{_run}_Entity1 - Conflict" && e.Price == 0);
    }
}
