using NetTopologySuite.Geometries;

using PhenX.EntityFrameworkCore.BulkInsert.Extensions;
using PhenX.EntityFrameworkCore.BulkInsert.Tests.DbContainer;
using PhenX.EntityFrameworkCore.BulkInsert.Tests.DbContext;

using Xunit;

namespace PhenX.EntityFrameworkCore.BulkInsert.Tests.Tests.Geo;

public abstract class GeoTestsBase<TDbContext>(TestDbContainer dbContainer) : IAsyncLifetime
    where TDbContext : TestDbContextGeo, new()
{
    private readonly Guid _run = Guid.NewGuid();
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

    [Fact]
    public async Task InsertEntities_WithGeo()
    {
        // Arrange
        var geo1 = new Point(1, 2) { SRID = 4326 };
        var geo2 = new Point(3, 4) { SRID = 4326 };

        var entities = new List<TestEntityWithGeo>
        {
            new TestEntityWithGeo { TestRun = _run, GeoObject = geo1 },
            new TestEntityWithGeo { TestRun = _run, GeoObject = geo2 }
        };

        // Act
        await _context.ExecuteBulkInsertAsync(entities);

        // Assert
        var insertedEntities = _context.TestEntitiesWithGeo.Where(x => x.TestRun == _run).ToList();
        Assert.Equal(2, insertedEntities.Count);
        Assert.Contains(insertedEntities, e => e.GeoObject == geo1);
        Assert.Contains(insertedEntities, e => e.GeoObject == geo2);
    }
}
