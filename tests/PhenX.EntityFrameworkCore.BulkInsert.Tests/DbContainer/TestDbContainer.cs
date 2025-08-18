using System.Data.Common;
using System.Reflection;

using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Configurations;
using DotNet.Testcontainers.Containers;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

using PhenX.EntityFrameworkCore.BulkInsert.Tests.DbContext;

using Testcontainers.Xunit;

using Xunit.Abstractions;

namespace PhenX.EntityFrameworkCore.BulkInsert.Tests.DbContainer;

public abstract class TestDbContainer<TBuilderEntity, TContainerEntity>(IMessageSink messageSink) : DbContainerFixture<TBuilderEntity, TContainerEntity>(messageSink), IDbContextFactory
    where TBuilderEntity : IContainerBuilder<TBuilderEntity, TContainerEntity, IContainerConfiguration>, new()
    where TContainerEntity : IContainer, IDatabaseContainer
{
    protected abstract void Configure(DbContextOptionsBuilder optionsBuilder, string databaseName);

    protected abstract TBuilderEntity CreateBuilder();

    protected virtual string DbmsName => typeof(TContainerEntity).Name.Replace("Container", "");

    protected override TBuilderEntity Configure()
    {
        var targetFramework = GetType().Assembly.GetCustomAttributes<AssemblyMetadataAttribute>().FirstOrDefault(e => e.Key == "TargetFramework")?.Value ?? "NA";
        return CreateBuilder()
            .WithReuse(true)
            .WithName($"PhenX.EntityFrameworkCore.BulkInsert.Tests.{DbmsName}-{targetFramework}")
            .WithWaitStrategy(Wait.ForUnixContainer().UntilDatabaseIsAvailable(DbProviderFactory));
    }

    protected virtual string GetConnectionString(string databaseName)
    {
        var builder = DbProviderFactory.CreateConnectionStringBuilder() ?? new DbConnectionStringBuilder();
        builder.ConnectionString = ConnectionString;
        builder["database"] = databaseName;
        return builder.ToString();
    }

    protected virtual async Task EnsureDatabaseCreatedAsync(Microsoft.EntityFrameworkCore.DbContext dbContext)
    {
        await dbContext.Database.EnsureCreatedAsync();
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

        await EnsureDatabaseCreatedAsync(dbContext);

        return dbContext;
    }
}
