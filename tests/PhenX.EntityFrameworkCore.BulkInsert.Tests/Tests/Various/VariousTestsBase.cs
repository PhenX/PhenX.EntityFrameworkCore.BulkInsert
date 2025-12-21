using FluentAssertions;

using PhenX.EntityFrameworkCore.BulkInsert.Enums;
using PhenX.EntityFrameworkCore.BulkInsert.Extensions;
using PhenX.EntityFrameworkCore.BulkInsert.Options;
using PhenX.EntityFrameworkCore.BulkInsert.Tests.DbContainer;
using PhenX.EntityFrameworkCore.BulkInsert.Tests.DbContext;

using Xunit;

namespace PhenX.EntityFrameworkCore.BulkInsert.Tests.Tests.Various;

public abstract class VariousTestsBase<TDbContext>(TestDbContainer dbContainer) : IAsyncLifetime
    where TDbContext : TestDbContext, new()
{
    private readonly Guid _run = Guid.NewGuid();
    private TDbContext _context = null!;

    public async Task InitializeAsync()
    {
        _context = await dbContainer.CreateContextAsync<TDbContext>("various");
    }

    public Task DisposeAsync()
    {
        _context.Dispose();
        return Task.CompletedTask;
    }

    [SkippableTheory]
    [CombinatorialData]
    public async Task InsertSmartEnumEntities(InsertStrategy strategy)
    {
        // Arrange
        var entities = new List<TestEntityWithSmartEnum>
        {
            new TestEntityWithSmartEnum { TestRun = _run, Enum = TestSmartEnum.Value},
            new TestEntityWithSmartEnum { TestRun = _run, Enum = TestSmartEnum.Value}
        };

        // Act
        var insertedEntities = await _context.InsertWithStrategyAsync(strategy, entities);

        // Assert
        insertedEntities.Should().BeEquivalentTo(entities,
            o => o.RespectingRuntimeTypes().Excluding(e => e.Id));
    }

    /// <summary>
    /// Tests that column names with spaces and SQL reserved keywords are properly quoted.
    /// This addresses the issue where columns like "Business Function Text" were not being
    /// properly escaped, causing SQL syntax errors.
    /// </summary>
    [SkippableTheory]
    [CombinatorialData]
    public async Task InsertEntities_WithSpecialColumnNames(InsertStrategy strategy)
    {
        // Arrange
        var entities = new List<TestEntityWithSpecialColumnNames>
        {
            new TestEntityWithSpecialColumnNames
            {
                TestRun = _run,
                BusinessFunctionText = $"{_run}_BusinessFunction1",
                OrderNumber = 100,
                Description = "Test description 1"
            },
            new TestEntityWithSpecialColumnNames
            {
                TestRun = _run,
                BusinessFunctionText = $"{_run}_BusinessFunction2",
                OrderNumber = 200,
                Description = "Test description 2"
            }
        };

        // Act
        var insertedEntities = await _context.InsertWithStrategyAsync(strategy, entities);

        // Assert
        insertedEntities.Should().BeEquivalentTo(entities,
            o => o.RespectingRuntimeTypes().Excluding(e => e.Id));
    }

    /// <summary>
    /// Tests that merge/upsert operations work correctly with column names containing
    /// spaces and SQL reserved keywords.
    /// </summary>
    [SkippableTheory]
    [CombinatorialData]
    public async Task InsertEntities_WithSpecialColumnNames_Merge(InsertStrategy strategy)
    {
        Skip.If(_context.IsProvider(ProviderType.MySql));

        // Arrange
        var entities = new List<TestEntityWithSpecialColumnNames>
        {
            new TestEntityWithSpecialColumnNames
            {
                TestRun = _run,
                BusinessFunctionText = $"{_run}_BusinessFunction1",
                OrderNumber = 100,
                Description = "Initial description"
            }
        };

        // First insert
        await _context.InsertWithStrategyAsync(strategy, entities);

        // Update entity for upsert
        entities[0].OrderNumber = 200;
        entities[0].Description = "Updated description";

        // Act - Merge with update on conflict
        var insertedEntities = await _context.InsertWithStrategyAsync(strategy, entities, _ => { },
            onConflict: new OnConflictOptions<TestEntityWithSpecialColumnNames>
            {
                Match = e => new { e.BusinessFunctionText },
                Update = (inserted, excluded) => new TestEntityWithSpecialColumnNames
                {
                    OrderNumber = excluded.OrderNumber,
                    Description = excluded.Description
                }
            });

        // Assert
        insertedEntities.Should().HaveCount(1);
        insertedEntities[0].OrderNumber.Should().Be(200);
        insertedEntities[0].Description.Should().Be("Updated description");
    }
}
