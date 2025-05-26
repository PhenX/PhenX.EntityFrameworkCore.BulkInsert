using FluentAssertions;

using NetTopologySuite.Geometries;

using PhenX.EntityFrameworkCore.BulkInsert.Tests.DbContainer;
using PhenX.EntityFrameworkCore.BulkInsert.Tests.DbContext;

using Xunit;

namespace PhenX.EntityFrameworkCore.BulkInsert.Tests.Tests.Geo;

public abstract class GeoTestsBase<TDbContext>(TestDbContainer dbContainer) : IAsyncLifetime
    where TDbContext : TestDbContextGeo, new()
{
    private TDbContext _context = null!;

    public async Task InitializeAsync()
    {
        _context = await dbContainer.CreateContextAsync<TDbContext>("geo");
    }

    public Task DisposeAsync()
    {
        _context.Dispose();
        return Task.CompletedTask;
    }

    [SkippableTheory]
    [CombinatorialData]
    public async Task InsertEntities_WithGeo(InsertStrategy strategy)
    {
        // Arrange
        var geo1 = new Point(1, 2) { SRID = 4326 };
        var geo2 = new Point(3, 4) { SRID = 4326 };

        var entities = new List<TestEntityWithGeo>
        {
            new TestEntityWithGeo { GeoObject = geo1 },
            new TestEntityWithGeo { GeoObject = geo2 }
        };

        // Act
        var insertedEntities = await _context.InsertWithStrategyAsync(strategy, entities);

        // Assert
        insertedEntities.Should().BeEquivalentTo(entities,
            o => o.RespectingRuntimeTypes().Excluding((TestEntityWithGeo e) => e.Id));
    }

    [SkippableTheory]
    [CombinatorialData]
    public async Task InsertEntities_WithGeo_And_Default_SRID(InsertStrategy strategy)
    {
        // Arrange
        var geo1 = new Point(1, 2);
        var geo2 = new Point(3, 4);

        var entities = new List<TestEntityWithGeo>
        {
            new TestEntityWithGeo { GeoObject = geo1 },
            new TestEntityWithGeo { GeoObject = geo2 }
        };

        // Act
        var insertedEntities = await _context.InsertWithStrategyAsync(strategy, entities);

        geo1.SRID = 4326;
        geo2.SRID = 4326;

        // Assert
        insertedEntities.Should().BeEquivalentTo(entities,
            o => o.RespectingRuntimeTypes().Excluding((TestEntityWithGeo e) => e.Id));
    }
}
