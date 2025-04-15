using System.Threading.Tasks;

using Microsoft.EntityFrameworkCore;

using Testcontainers.PostgreSql;

using Xunit;

namespace EntityFrameworkCore.ExecuteInsert.Tests;

public abstract class BulkInsertProviderTestsBase<TDbContext> : IAsyncLifetime
    where TDbContext : DbContext, new()
{
    protected readonly PostgreSqlContainer PostgresContainer;
    protected TDbContext DbContext;

    protected BulkInsertProviderTestsBase()
    {
        PostgresContainer = GetPostgresContainer();
    }

    private static PostgreSqlContainer GetPostgresContainer()
    {
        return new PostgreSqlBuilder()
            .WithDatabase("testdb")
            .WithUsername("testuser")
            .WithPassword("testpassword")
            .Build();
    }

    public async Task InitializeAsync()
    {
        await PostgresContainer.StartAsync();

        var connectionString = PostgresContainer.GetConnectionString();

        DbContext = new TDbContext();
        DbContext.Database.SetConnectionString(connectionString);
        await DbContext.Database.EnsureCreatedAsync();
    }

    public async Task DisposeAsync()
    {
        await DbContext.Database.EnsureDeletedAsync();
        await DbContext.DisposeAsync();

        await PostgresContainer.DisposeAsync();
    }
}
