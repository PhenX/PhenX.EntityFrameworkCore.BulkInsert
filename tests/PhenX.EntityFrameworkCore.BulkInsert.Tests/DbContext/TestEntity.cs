using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

using Microsoft.EntityFrameworkCore;

namespace PhenX.EntityFrameworkCore.BulkInsert.Tests.DbContext;

[PrimaryKey(nameof(Id))]
[Index(nameof(Name), IsUnique = true)]
[Table("test_entity")]
public class TestEntity : TestEntityBase
{
    public int Id { get; set; }

    [Column("name")]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [Column("some_price")]
    public decimal Price { get; set; }

    [Column("some_float")]
    public float Float { get; set; } = 10.1f;

    [Column("some_double")]
    public double Double { get; set; } = 10.1d;

    [Column("the_identifier")]
    public Guid Identifier { get; set; }

    [Column("nullable_identifier")]
    public Guid? NullableIdentifier { get; set; }

    public DateTime Created { get; set; }

    public DateTime? Modified { get; set; }

    [Column("string_enum_value")]
    public StringEnum StringEnumValue { get; set; }

    [Column("num_enum_value")]
    public NumericEnum NumericEnumValue { get; set; }
}
