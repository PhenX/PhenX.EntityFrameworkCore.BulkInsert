using DotNet.Testcontainers.Containers;

using Microsoft.EntityFrameworkCore;

using PhenX.EntityFrameworkCore.BulkInsert.Tests.DbContext;

using Xunit;

namespace PhenX.EntityFrameworkCore.BulkInsert.Tests.DbContainer;

public abstract class TestDbContainer<TDbContext> : IAsyncLifetime
    where TDbContext : TestDbContextBase, new()
{
    protected readonly IDatabaseContainer? DbContainer;

    public TDbContext DbContext { get; private set; } = null!;

    protected TestDbContainer()
    {
        DbContainer = GetDbContainer();
    }

    protected string GetRandomContainerName() => "phenx-bulk-insert-test-" + Guid.NewGuid();

    protected abstract IDatabaseContainer? GetDbContainer();

    protected virtual string GetConnectionString()
    {
        return DbContainer?.GetConnectionString() ?? string.Empty;
    }

    protected abstract void Configure(DbContextOptionsBuilder optionsBuilder);

    public async Task InitializeAsync()
    {
        if (DbContainer != null)
        {
            await DbContainer.StartAsync();
        }
    }

    public async Task DisposeAsync()
    {
        if (DbContainer != null)
        {
            await DbContainer.DisposeAsync();
        }
    }

    public async Task InitializeDbContextAsync()
    {
        DbContext = new TDbContext
        {
            ConfigureOptions = Configure
        };

        DbContext.Database.SetConnectionString(GetConnectionString());
        await DbContext.Database.EnsureCreatedAsync();
    }

    public async Task DisposeDbContextAsync()
    {
        await DbContext.Database.EnsureDeletedAsync();
        await DbContext.DisposeAsync();
        DbContext = null!;
    }
}
