using System.ComponentModel.DataAnnotations.Schema;

using Microsoft.EntityFrameworkCore;

namespace PhenX.EntityFrameworkCore.BulkInsert.Benchmark;

[PrimaryKey(nameof(Id))]
[Table(nameof(TestEntity))]
public class TestEntity
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public Guid Identifier { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public NumericEnum NumericEnumValue { get; set; }

    /// <summary>
    /// Child entities for IncludeGraph benchmarking.
    /// </summary>
    public ICollection<TestEntityChild> Children { get; set; } = new List<TestEntityChild>();
}

/// <summary>
/// Child entity for benchmarking IncludeGraph with navigation properties.
/// </summary>
[PrimaryKey(nameof(Id))]
[Table(nameof(TestEntityChild))]
public class TestEntityChild
{
    public int Id { get; set; }
    public string Description { get; set; } = string.Empty;
    public int Quantity { get; set; }

    public int TestEntityId { get; set; }
    public TestEntity TestEntity { get; set; } = null!;
}

public enum NumericEnum
{
    First = 1,
    Second = 2,
}
