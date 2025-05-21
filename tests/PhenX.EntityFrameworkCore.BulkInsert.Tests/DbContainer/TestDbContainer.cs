using DotNet.Testcontainers.Containers;

using Microsoft.EntityFrameworkCore;

using PhenX.EntityFrameworkCore.BulkInsert.Tests.DbContext;

using Xunit;

namespace PhenX.EntityFrameworkCore.BulkInsert.Tests.DbContainer;

public abstract class TestDbContainer<TDbContext> : IAsyncLifetime
    where TDbContext : TestDbContextBase, new()
{
    private static readonly TimeSpan WaitTime = TimeSpan.FromSeconds(30);
    protected readonly IDatabaseContainer? DbContainer;

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

    public async Task<TDbContext> CreateContextAsync()
    {
        var dbContext = new TDbContext
        {
            ConfigureOptions = Configure
        };

        dbContext.Database.SetConnectionString(GetConnectionString());

        await EnsureConnectedAsync(dbContext);
        try
        {
            await dbContext.Database.EnsureCreatedAsync();
        }
        catch
        {
            // Often fails with SQL server.
        }

        return dbContext;
    }

    protected virtual async Task EnsureConnectedAsync(TDbContext context)
    {
        using var cts = new CancellationTokenSource(WaitTime);
        while (!await context.Database.CanConnectAsync(cts.Token))
        {
            await Task.Delay(100, cts.Token);
        }
    }

    public async Task DisposeAsync()
    {
        if (DbContainer != null)
        {
            await DbContainer.DisposeAsync();
        }
    }
}
