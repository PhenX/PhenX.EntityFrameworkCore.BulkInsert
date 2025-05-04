using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Threading.Tasks;

using EntityFrameworkCore.ExecuteInsert.Abstractions;

using Microsoft.EntityFrameworkCore;
using Xunit;
using EntityFrameworkCore.ExecuteInsert.PostgreSql;

namespace EntityFrameworkCore.ExecuteInsert.Tests;

[PrimaryKey(nameof(Id))]
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

public class TestDbContext : DbContext
{
    public DbSet<TestEntity> TestEntities { get; set; } = null!;

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder
            .UseNpgsql()
            .UseExecuteInsertPostgres();
    }
}

public class BulkInsertProviderTests : BulkInsertProviderTestsBase<TestDbContext>
{
    [Fact]
    public async Task InsertsEntitiesSuccessfully()
    {
        // Arrange
        var entities = new List<TestEntity>
        {
            new TestEntity { Id = 1, Name = "Entity1" },
            new TestEntity { Id = 2, Name = "Entity2" }
        };

        // Act
        await DbContext.ExecuteInsertWithIdentityAsync(entities);

        // Assert
        var insertedEntities = DbContext.TestEntities.ToList();
        Assert.Equal(2, insertedEntities.Count);
        Assert.Contains(insertedEntities, e => e.Name == "Entity1");
        Assert.Contains(insertedEntities, e => e.Name == "Entity2");
    }

    [Fact]
    public async Task InsertsEntitiesMoveRowsSuccessfully()
    {
        // Arrange
        var entities = new List<TestEntity>
        {
            new TestEntity { Id = 1, Name = "Entity1" },
            new TestEntity { Id = 2, Name = "Entity2" }
        };

        // Act
        await DbContext.ExecuteInsertWithIdentityAsync(entities, o =>
        {
            o.MoveRows = true;
        });

        // Assert
        var insertedEntities = DbContext.TestEntities.ToList();
        Assert.Equal(2, insertedEntities.Count);
        Assert.Contains(insertedEntities, e => e.Name == "Entity1");
        Assert.Contains(insertedEntities, e => e.Name == "Entity2");
    }

    [Fact]
    public async Task DoesNothingWhenEntitiesAreEmpty()
    {
        // Arrange
        var entities = new List<TestEntity>();

        // Act
        await Assert.ThrowsAsync<InvalidOperationException>(async () => await DbContext.ExecuteInsertAsync(entities));

        // Assert
        var insertedEntities = DbContext.TestEntities.ToList();
        Assert.Empty(insertedEntities);
    }

    [Fact]
    public async Task InsertsThousandsOfEntitiesSuccessfully()
    {
        // Arrange
        const int count = 1_000_000;
        var entities = Enumerable.Range(1, count).Select(i => new TestEntity
        {
            Id = i,
            Name = $"Entity{i}",
            Price = (decimal)(i * 0.1),
            Identifier = Guid.NewGuid(),
            StringEnumValue = (StringEnum)(i % 2),
            NumericEnumValue = (NumericEnum)(i % 2),
        }).ToList();

        // Act
        await DbContext.ExecuteInsertAsync(entities, o =>
        {
            o.Recursive = false;
            o.MoveRows = false;
        });

        // Assert
        var insertedEntities = DbContext.TestEntities.ToList();
        Assert.Equal(count, insertedEntities.Count);
        Assert.Contains(insertedEntities, e => e.Name == "Entity1");
        Assert.Contains(insertedEntities, e => e.Name == "Entity" + count);
    }
}
