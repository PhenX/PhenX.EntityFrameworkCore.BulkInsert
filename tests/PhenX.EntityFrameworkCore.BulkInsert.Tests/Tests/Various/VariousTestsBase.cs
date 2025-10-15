using FluentAssertions;

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
}
