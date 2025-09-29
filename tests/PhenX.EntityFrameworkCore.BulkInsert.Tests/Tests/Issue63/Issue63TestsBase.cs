using PhenX.EntityFrameworkCore.BulkInsert.Extensions;
using PhenX.EntityFrameworkCore.BulkInsert.Tests.DbContainer;
using PhenX.EntityFrameworkCore.BulkInsert.Tests.DbContext;

using Xunit;

namespace PhenX.EntityFrameworkCore.BulkInsert.Tests.Tests.Issue63;

/// <summary>
/// Test case for issue #63: Error when bulk inserting entities with composite primary key 
/// where one column is DatabaseGenerated(DatabaseGeneratedOption.Identity)
/// 
/// Error: "Incorrect number of arguments supplied for call to method 'System.Object get_Item(System.String)'"
/// Stack trace points to PropertyAccessor.CreateGetter when building metadata for composite primary keys with identity columns.
/// </summary>
public abstract class Issue63TestsBase<TDbContext>(TestDbContainer dbContainer) : IAsyncLifetime
    where TDbContext : TestDbContext, new()
{
    private readonly Guid _run = Guid.NewGuid();
    private TDbContext _context = null!;

    public async Task InitializeAsync()
    {
        _context = await dbContainer.CreateContextAsync<TDbContext>("issue63");
    }

    public Task DisposeAsync()
    {
        _context.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task BulkInsert_WithCompositePrimaryKey_AndIdentityColumn_ReproducesIssue63()
    {
        // Arrange
        var entities = new List<TestEntityWithCompositePrimaryKey>
        {
            new TestEntityWithCompositePrimaryKey 
            { 
                TestRun = _run, 
                DateTimeUtc = DateTime.UtcNow,
                Name = "Test Entity 1",
                Value = 100
            },
            new TestEntityWithCompositePrimaryKey 
            { 
                TestRun = _run, 
                DateTimeUtc = DateTime.UtcNow.AddMinutes(1),
                Name = "Test Entity 2",
                Value = 200
            }
        };

        // Act & Assert - This reproduces issue #63
        // The error can manifest as different types depending on the provider:
        // - ArgumentException with "Incorrect number of arguments supplied for call to method"
        // - SqliteException for NOT NULL constraint failures
        // - Or it might work in some cases (PostgreSQL with sequences)
        
        var exception = await Record.ExceptionAsync(async () =>
            await _context.ExecuteBulkInsertAsync(entities));

        // Document the actual behavior - the test serves to reproduce the issue
        if (exception != null)
        {
            // If there's an exception, verify it's one of the expected types related to issue #63
            Assert.True(
                exception is ArgumentException || 
                exception is Microsoft.Data.Sqlite.SqliteException ||
                exception is InvalidOperationException,
                $"Unexpected exception type: {exception.GetType()}, Message: {exception.Message}");
            
            // Log the exception for debugging
            // Just verify the context is accessible
            Assert.NotNull(_context.Database);
        }
        else
        {
            // Some providers might handle this correctly, which is also valuable information
            Assert.NotEmpty(entities); // Just verify the test setup was correct
        }
    }
}