using FluentAssertions;

using PhenX.EntityFrameworkCore.BulkInsert.Enums;
using PhenX.EntityFrameworkCore.BulkInsert.Extensions;
using PhenX.EntityFrameworkCore.BulkInsert.MySql;
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
            o => o.RespectingRuntimeTypes().Excluding(e => e.Id));
    }

    [SkippableTheory]
    [CombinatorialData]
    public async Task InsertEntities_WithJson(InsertStrategy strategy)
    {
        // Arrange
        var entities = new List<TestEntityWithJson>
        {
            new TestEntityWithJson
            {
                JsonArray = [1],
                JsonObject = new JsonDbObject { Code = 1, Name = "Test1" },
            },
            new TestEntityWithJson
            {
                JsonArray = [2],
                JsonObject = new JsonDbObject { Code = 2, Name = "Test2" },
            },
        };

        // Act
        var insertedEntities = await _context.InsertWithStrategyAsync(strategy, entities);

        // Assert
        insertedEntities.Should().BeEquivalentTo(entities,
            o => o.RespectingRuntimeTypes().Excluding(e => e.Id));
    }

    [SkippableFact]
    public async Task InsertEntities_AndReturn_AsyncEnumerable()
    {
        Skip.If(_context.IsProvider(ProviderType.MySql, ProviderType.Oracle));

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
            o => o.RespectingRuntimeTypes().Excluding(e => e.Id));
    }

    [SkippableTheory]
    [CombinatorialData]
    public async Task InsertEntities_MoveRows(InsertStrategy strategy)
    {
        Skip.If(_context.IsProvider(ProviderType.Oracle), "Unstable with Oracle");

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
            o => o.RespectingRuntimeTypes().Excluding(e => e.Id));
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
        const int count = 56055;

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
            new TestEntityWithConverters() { Name = $"{_run}_Entity1", CreatedAt = now, Uri = null },
            new TestEntityWithConverters() { Name = $"{_run}_Entity2", CreatedAt = now.AddDays(-1), Uri = new Uri("http://example.com/test") }
        };

        // Act
        var insertedEntities = await _context.InsertWithStrategyAsync(strategy, entities);

        // Assert
        insertedEntities.Should().BeEquivalentTo(entities,
            o => o.RespectingRuntimeTypes().Excluding(e => e.Id));
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
            o => o.RespectingRuntimeTypes().Excluding(e => e.Id));
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

    [SkippableFact]
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
                await _context.ExecuteBulkInsertAsync(entities, (MySqlBulkInsertOptions _) =>
                {
                }));
        }

        if (_context.IsProvider(ProviderType.MySql))
        {
            await Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await _context.ExecuteBulkInsertAsync(entities, (SqlServerBulkInsertOptions _) =>
                {
                }));
        }

        if (_context.IsProvider(ProviderType.PostgreSql))
        {
            await Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await _context.ExecuteBulkInsertAsync(entities, (SqlServerBulkInsertOptions _) =>
                {
                }));
        }
    }

    [SkippableTheory]
    [CombinatorialData]
    public async Task InsertEntities_WithGeneratedGuidId(InsertStrategy strategy)
    {
        // Arrange
        var entities = new List<TestEntityWithGuidId>
        {
            new TestEntityWithGuidId { Id = Guid.NewGuid(), Name = $"{_run}_Entity1" },
            new TestEntityWithGuidId { Id = Guid.NewGuid(), Name = $"{_run}_Entity2" }
        };

        // Act
        var insertedEntities = await _context.InsertWithStrategyAsync(strategy, entities, configure => configure.CopyGeneratedColumns = true);

        // Assert
        insertedEntities.Should().BeEquivalentTo(entities,
            o=> o
                .RespectingRuntimeTypes()
                .Excluding(e => e.Id)
            );
    }
}
