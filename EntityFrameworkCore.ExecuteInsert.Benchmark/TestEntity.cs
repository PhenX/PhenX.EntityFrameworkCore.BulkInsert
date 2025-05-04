using System.ComponentModel.DataAnnotations.Schema;

using Microsoft.EntityFrameworkCore;

namespace EntityFrameworkCore.ExecuteInsert.Benchmark;

[PrimaryKey(nameof(Id))]
public class TestEntity
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public Guid Identifier { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

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
