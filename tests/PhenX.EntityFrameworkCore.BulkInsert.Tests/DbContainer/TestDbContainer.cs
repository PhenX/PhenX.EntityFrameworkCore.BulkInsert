using System.Data.Common;

using DotNet.Testcontainers.Containers;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

using PhenX.EntityFrameworkCore.BulkInsert.Tests.DbContext;

using Xunit;

namespace PhenX.EntityFrameworkCore.BulkInsert.Tests.DbContainer;

public abstract class TestDbContainer : IAsyncLifetime
{
    private readonly TimeSpan _waitTime = TimeSpan.FromSeconds(30);
    private readonly HashSet<string> _connected = [];
    protected readonly IDatabaseContainer? DbContainer;

    protected TestDbContainer()
    {
        DbContainer = GetDbContainer();
    }

    protected abstract IDatabaseContainer? GetDbContainer();

    protected virtual string GetConnectionString(string databaseName)
    {
        if (DbContainer == null)
        {
            return string.Empty;
        }

        var builder = new DbConnectionStringBuilder()
        {
            ConnectionString = DbContainer.GetConnectionString()
        };

        builder["database"] = databaseName;
        return builder.ToString();
    }

    protected abstract void Configure(DbContextOptionsBuilder optionsBuilder, string databaseName);

    public async Task InitializeAsync()
    {
        if (DbContainer != null)
        {
            await DbContainer.StartAsync();
        }
    }

    public async Task<TDbContext> CreateContextAsync<TDbContext>(string databaseName)
        where TDbContext : TestDbContextBase, new()
    {
        var dbContext = new TDbContext
        {
            ConfigureOptions = (builder) =>
            {
                builder.UseLoggerFactory(NullLoggerFactory.Instance);
                Configure(builder, databaseName);
            }
        };

        if (_connected.Add(databaseName))
        {
            await EnsureConnectedAsync(dbContext, databaseName);
        }

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

    protected virtual async Task EnsureConnectedAsync<TDbContext>(TDbContext context, string databaseName)
        where TDbContext : TestDbContextBase
    {
        using var cts = new CancellationTokenSource(_waitTime);

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
