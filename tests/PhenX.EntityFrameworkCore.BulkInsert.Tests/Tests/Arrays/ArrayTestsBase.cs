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
/// Also contains targeted regression tests for GitHub issue #98 which reported that
/// <c>List&lt;E&gt;</c> properties (where <c>E</c> is a .NET enum) are silently
/// inserted as empty arrays.
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

    /// <summary>
    /// Verifies that a <c>List&lt;NumericEnum&gt;</c> property (which maps to an
    /// <c>integer[]</c> column in PostgreSQL via Npgsql's array converter) is
    /// inserted with its full contents and not silently written as an empty array.
    /// </summary>
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

    /// <summary>
    /// Verifies that empty and null array-typed properties are handled correctly.
    /// </summary>
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

    /// <summary>
    /// Verifies that a single-element <c>List&lt;NumericEnum&gt;</c> is inserted correctly.
    /// </summary>
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

    // --- Regression tests for GitHub issue #98 ---
    // Issue: List<E> (where E is a .NET enum) was silently inserted as an empty
    // array.  The three tests below triangulate the root cause:
    //   1. Minimal entity with non-nullable List<NumericEnum>  (exact issue shape)
    //   2. Same values stored as NumericEnum[]                 (array vs list)
    //   3. Same values stored as List<int>                     (enum vs plain int)
    // If (1) fails while (2) or (3) passes, the bug is specific to List<Enum>.

    /// <summary>
    /// Reproduces the exact shape reported in GitHub issue #98:
    /// a minimal entity whose only payload is a non-nullable
    /// <c>List&lt;NumericEnum&gt;</c> property mapped to a PostgreSQL
    /// <c>integer[]</c> column.
    /// </summary>
    [SkippableFact]
    public async Task Issue98_InsertEntity_WithNonNullableEnumList_StoresCorrectValues()
    {
        Skip.If(!_context.IsProvider(ProviderType.PostgreSql));

        // Arrange – mirrors "record Item(List<E> Values)" from the issue
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
        inserted[0].EnumList.Should().BeEquivalentTo(entities[0].EnumList, o => o.WithStrictOrdering(),
            "the list must not be silently replaced by an empty array (issue #98)");
        inserted[1].EnumList.Should().BeEquivalentTo(entities[1].EnumList, o => o.WithStrictOrdering(),
            "the list must not be silently replaced by an empty array (issue #98)");
    }

    /// <summary>
    /// Inserts a <c>NumericEnum[]</c> (native .NET array rather than
    /// <c>List&lt;NumericEnum&gt;</c>) to verify whether the issue is specific to
    /// the <c>List&lt;T&gt;</c> collection type.
    /// </summary>
    [SkippableFact]
    public async Task Issue98_InsertEntity_WithEnumArray_StoresCorrectValues()
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

    /// <summary>
    /// Inserts a <c>List&lt;int&gt;</c> (same collection type as the failing case
    /// but with a plain integer element type) to determine whether the issue is
    /// enum-specific or affects <c>List&lt;T&gt;</c> in general.
    /// </summary>
    [SkippableFact]
    public async Task Issue98_InsertEntity_WithIntList_StoresCorrectValues()
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
}
