using System.Threading.Tasks;

using DotNet.Testcontainers.Containers;

using EntityFrameworkCore.ExecuteInsert.Tests.DbContext;

using Microsoft.EntityFrameworkCore;

using Xunit;

namespace EntityFrameworkCore.ExecuteInsert.Tests.DbContainer;

public abstract class TestDbContainer<TDbContext> : IAsyncLifetime
    where TDbContext : TestDbContextBase, new()
{
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

        await DbContext.Database.EnsureCreatedAsync();
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
