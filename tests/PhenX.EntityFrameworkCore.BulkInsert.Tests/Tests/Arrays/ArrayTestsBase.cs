using FluentAssertions;

using Microsoft.EntityFrameworkCore;

using PhenX.EntityFrameworkCore.BulkInsert.Enums;
using PhenX.EntityFrameworkCore.BulkInsert.Extensions;
using PhenX.EntityFrameworkCore.BulkInsert.Tests.DbContainer;
using PhenX.EntityFrameworkCore.BulkInsert.Tests.DbContext;

using Xunit;

namespace PhenX.EntityFrameworkCore.BulkInsert.Tests.Tests.Arrays;

/// <summary>
/// Tests for bulk-inserting entities that contain PostgreSQL array-typed properties,
/// including <c>List&lt;T&gt;</c> and <c>T[]</c> where <c>T</c> is mapped to a
/// PostgreSQL array column (e.g. <c>integer[]</c>, <c>text[]</c>).
/// </summary>
public abstract class ArrayTestsBase<TDbContext>(IDbContextFactory dbContextFactory) : IAsyncLifetime
    where TDbContext : TestDbContextPostgreSql, new()
{
    private readonly Guid _run = Guid.NewGuid();
    private TDbContext _context = null!;

    public async Task InitializeAsync()
    {
        _context = await dbContextFactory.CreateContextAsync<TDbContext>("arrays");
    }

    public Task DisposeAsync()
    {
        _context.Dispose();
        return Task.CompletedTask;
    }

    [SkippableFact]
    public async Task InsertEntities_WithEnumList_StoresCorrectValues()
    {
        Skip.If(!_context.IsProvider(ProviderType.PostgreSql));

        // Arrange
        var entities = new List<TestEntityWithArrays>
        {
            new TestEntityWithArrays
            {
                TestRun = _run,
                EnumList = [NumericEnum.First, NumericEnum.Second],
                IntArray = [1, 2, 3],
                StringArray = ["a", "b"]
            },
            new TestEntityWithArrays
            {
                TestRun = _run,
                EnumList = [NumericEnum.Second, NumericEnum.First],
                IntArray = [10, 20],
                StringArray = ["x"]
            }
        };

        // Act
        await _context.ExecuteBulkInsertAsync(entities);

        // Assert
        _context.ChangeTracker.Clear();
        var inserted = await _context.TestEntitiesWithArrays
            .Where(e => e.TestRun == _run)
            .OrderBy(e => e.Id)
            .ToListAsync();

        inserted.Should().HaveCount(2);
        inserted[0].EnumList.Should().BeEquivalentTo(entities[0].EnumList, o => o.WithStrictOrdering());
        inserted[0].IntArray.Should().BeEquivalentTo(entities[0].IntArray, o => o.WithStrictOrdering());
        inserted[0].StringArray.Should().BeEquivalentTo(entities[0].StringArray, o => o.WithStrictOrdering());
        inserted[1].EnumList.Should().BeEquivalentTo(entities[1].EnumList, o => o.WithStrictOrdering());
        inserted[1].IntArray.Should().BeEquivalentTo(entities[1].IntArray, o => o.WithStrictOrdering());
        inserted[1].StringArray.Should().BeEquivalentTo(entities[1].StringArray, o => o.WithStrictOrdering());
    }

    [SkippableFact]
    public async Task InsertEntities_WithNullAndEmptyArrays_StoresCorrectValues()
    {
        Skip.If(!_context.IsProvider(ProviderType.PostgreSql));

        // Arrange
        var entities = new List<TestEntityWithArrays>
        {
            new TestEntityWithArrays
            {
                TestRun = _run,
                EnumList = [],
                IntArray = null,
                StringArray = null
            },
            new TestEntityWithArrays
            {
                TestRun = _run,
                EnumList = null,
                IntArray = [],
                StringArray = []
            }
        };

        // Act
        await _context.ExecuteBulkInsertAsync(entities);

        // Assert
        _context.ChangeTracker.Clear();
        var inserted = await _context.TestEntitiesWithArrays
            .Where(e => e.TestRun == _run)
            .OrderBy(e => e.Id)
            .ToListAsync();

        inserted.Should().HaveCount(2);
        inserted[0].EnumList.Should().BeEmpty();
        inserted[0].IntArray.Should().BeNull();
        inserted[0].StringArray.Should().BeNull();
        inserted[1].EnumList.Should().BeNull();
        inserted[1].IntArray.Should().BeEmpty();
        inserted[1].StringArray.Should().BeEmpty();
    }

    [SkippableFact]
    public async Task InsertEntities_WithSingleElementEnumList_StoresCorrectValue()
    {
        Skip.If(!_context.IsProvider(ProviderType.PostgreSql));

        // Arrange
        var entities = new List<TestEntityWithArrays>
        {
            new TestEntityWithArrays
            {
                TestRun = _run,
                EnumList = [NumericEnum.First],
            }
        };

        // Act
        await _context.ExecuteBulkInsertAsync(entities);

        // Assert
        _context.ChangeTracker.Clear();
        var inserted = await _context.TestEntitiesWithArrays
            .Where(e => e.TestRun == _run)
            .ToListAsync();

        inserted.Should().HaveCount(1);
        inserted[0].EnumList.Should().BeEquivalentTo(entities[0].EnumList, o => o.WithStrictOrdering());
    }

    [SkippableFact]
    public async Task InsertEntities_WithNonNullableEnumList_StoresCorrectValues()
    {
        Skip.If(!_context.IsProvider(ProviderType.PostgreSql));

        // Arrange
        var entities = new List<TestEntityWithEnumList>
        {
            new() { TestRun = _run, EnumList = [NumericEnum.First, NumericEnum.Second] },
            new() { TestRun = _run, EnumList = [NumericEnum.Second, NumericEnum.First] },
        };

        // Act
        await _context.ExecuteBulkInsertAsync(entities);

        // Assert
        _context.ChangeTracker.Clear();
        var inserted = await _context.TestEntitiesWithEnumList
            .Where(e => e.TestRun == _run)
            .OrderBy(e => e.Id)
            .ToListAsync();

        inserted.Should().HaveCount(2);
        inserted[0].EnumList.Should().BeEquivalentTo(entities[0].EnumList, o => o.WithStrictOrdering());
        inserted[1].EnumList.Should().BeEquivalentTo(entities[1].EnumList, o => o.WithStrictOrdering());
    }

    [SkippableFact]
    public async Task InsertEntities_WithEnumArray_StoresCorrectValues()
    {
        Skip.If(!_context.IsProvider(ProviderType.PostgreSql));

        // Arrange
        var entities = new List<TestEntityWithEnumArray>
        {
            new() { TestRun = _run, EnumArray = [NumericEnum.First, NumericEnum.Second] },
            new() { TestRun = _run, EnumArray = [NumericEnum.Second, NumericEnum.First] },
        };

        // Act
        await _context.ExecuteBulkInsertAsync(entities);

        // Assert
        _context.ChangeTracker.Clear();
        var inserted = await _context.TestEntitiesWithEnumArray
            .Where(e => e.TestRun == _run)
            .OrderBy(e => e.Id)
            .ToListAsync();

        inserted.Should().HaveCount(2);
        inserted[0].EnumArray.Should().BeEquivalentTo(entities[0].EnumArray, o => o.WithStrictOrdering());
        inserted[1].EnumArray.Should().BeEquivalentTo(entities[1].EnumArray, o => o.WithStrictOrdering());
    }

    [SkippableFact]
    public async Task InsertEntities_WithIntList_StoresCorrectValues()
    {
        Skip.If(!_context.IsProvider(ProviderType.PostgreSql));

        // Arrange
        var entities = new List<TestEntityWithIntList>
        {
            new() { TestRun = _run, IntList = [1, 2, 3] },
            new() { TestRun = _run, IntList = [4, 5] },
        };

        // Act
        await _context.ExecuteBulkInsertAsync(entities);

        // Assert
        _context.ChangeTracker.Clear();
        var inserted = await _context.TestEntitiesWithIntList
            .Where(e => e.TestRun == _run)
            .OrderBy(e => e.Id)
            .ToListAsync();

        inserted.Should().HaveCount(2);
        inserted[0].IntList.Should().BeEquivalentTo(entities[0].IntList, o => o.WithStrictOrdering());
        inserted[1].IntList.Should().BeEquivalentTo(entities[1].IntList, o => o.WithStrictOrdering());
    }

    /// <summary>
    /// Verifies that a <c>List&lt;NumericEnum&gt;</c> property configured with an
    /// explicit <c>HasColumnType("integer[]")</c> in Fluent API is inserted correctly.
    /// </summary>
    [SkippableFact]
    public async Task InsertEntities_WithEnumListAndExplicitColumnType_StoresCorrectValues()
    {
        Skip.If(!_context.IsProvider(ProviderType.PostgreSql));

        // Arrange
        var entities = new List<TestEntityWithEnumListExplicitType>
        {
            new() { TestRun = _run, EnumList = [NumericEnum.First, NumericEnum.Second] },
            new() { TestRun = _run, EnumList = [NumericEnum.Second, NumericEnum.First] },
        };

        // Act
        await _context.ExecuteBulkInsertAsync(entities);

        // Assert
        _context.ChangeTracker.Clear();
        var inserted = await _context.TestEntitiesWithEnumListExplicitType
            .Where(e => e.TestRun == _run)
            .OrderBy(e => e.Id)
            .ToListAsync();

        inserted.Should().HaveCount(2);
        inserted[0].EnumList.Should().BeEquivalentTo(entities[0].EnumList, o => o.WithStrictOrdering());
        inserted[1].EnumList.Should().BeEquivalentTo(entities[1].EnumList, o => o.WithStrictOrdering());
    }
}

