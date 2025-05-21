using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

using Microsoft.EntityFrameworkCore;

namespace PhenX.EntityFrameworkCore.BulkInsert.Tests.DbContext;

[PrimaryKey(nameof(Id))]
[Index(nameof(Name), IsUnique = true)]
[Table("test_entity")]
public class TestEntity
{

    public int Id { get; set; }

    [Column("name")]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [Column("some_price")]
    public decimal Price { get; set; }

    [Column("test_run")]
    public Guid TestRun { get; set; }

    [Column("the_identifier")]
    public Guid Identifier { get; set; }

    [Column("string_enum_value", TypeName = "text")]
    public StringEnum StringEnumValue { get; set; }

    [Column("num_enum_value", TypeName = "text")]
    public NumericEnum NumericEnumValue { get; set; }
}
