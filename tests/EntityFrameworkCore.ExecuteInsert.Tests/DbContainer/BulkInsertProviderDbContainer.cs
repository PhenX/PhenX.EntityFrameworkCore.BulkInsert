using System;
using System.ComponentModel.DataAnnotations.Schema;
using System.Threading.Tasks;

using DotNet.Testcontainers.Containers;

using Microsoft.EntityFrameworkCore;

using Xunit;

namespace EntityFrameworkCore.ExecuteInsert.Tests.DbContainer;


[PrimaryKey(nameof(Id))]
[Index(nameof(Name), IsUnique = true)]
public class TestEntity
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public Guid Identifier { get; set; }

    [Column(nameof(StringEnumValue), TypeName = "text")]
    public StringEnum StringEnumValue { get; set; }

    public NumericEnum NumericEnumValue { get; set; }
}

public enum NumericEnum
{
    First = 1,
    Second = 2,
}

public enum StringEnum
{
    First,
    Second,
}

public class TestDbContext : BulkDbContext
{
    public DbSet<TestEntity> TestEntities { get; set; } = null!;
}

public abstract class BulkInsertProviderDbContainer<TDbContext> : IAsyncLifetime
    where TDbContext : BulkDbContext, new()
{
    protected readonly IDatabaseContainer? DbContainer;

    public TDbContext DbContext { get; private set; } = null!;

    protected BulkInsertProviderDbContainer()
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
