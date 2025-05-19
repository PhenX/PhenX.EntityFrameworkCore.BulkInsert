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

    protected abstract IDatabaseContainer? GetDbContainer();

    protected virtual string GetConnectionString()
    {
        return DbContainer?.GetConnectionString() ?? string.Empty;
    }

    protected abstract void Configure(DbContextOptionsBuilder optionsBuilder);

    protected virtual void ConfigureModel(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TestEntityWithMultipleTypes>(builder =>
        {
            builder.OwnsOne(p => p.SubEntity, owned => owned.ToJson("child_entity"));
        });
    }

    public async Task InitializeAsync()
    {
        if (DbContainer != null)
        {
            await DbContainer.StartAsync();
        }

        DbContext = new TDbContext
        {
            ConfigureOptions = Configure,
            ConfigureModel = ConfigureModel,
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
