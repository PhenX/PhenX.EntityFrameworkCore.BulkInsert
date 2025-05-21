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

    public TDbContext DbContext { get; private set; } = null!;

    protected TestDbContainer()
    {
        DbContainer = GetDbContainer();
    }

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

        DbContext = new TDbContext
        {
            ConfigureOptions = Configure
        };
        DbContext.Database.SetConnectionString(GetConnectionString());

        await EnsureConnectedAsync();

        await DbContext.Database.EnsureCreatedAsync();
    }

    protected virtual async Task EnsureConnectedAsync()
    {
        using var cts = new CancellationTokenSource(WaitTime);
        while (!await DbContext.Database.CanConnectAsync(cts.Token))
        {
            await Task.Delay(100, cts.Token);
        }
    }

    public async Task DisposeAsync()
    {
        // await DbContext.Database.EnsureDeletedAsync();
        await DbContext.DisposeAsync();

        if (DbContainer != null)
        {
            await DbContainer.DisposeAsync();
        }
    }
}
