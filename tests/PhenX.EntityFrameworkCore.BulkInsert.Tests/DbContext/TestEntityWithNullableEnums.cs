using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

using Microsoft.EntityFrameworkCore;

namespace PhenX.EntityFrameworkCore.BulkInsert.Tests.DbContext;

[PrimaryKey(nameof(Id))]
[Table("test_entity_with_nullable_enums")]
public class TestEntityWithNullableEnums : TestEntityBase
{
    public int Id { get; set; }


    [Column("string_enum_value")]
    public StringEnum? StringEnumValue { get; set; }

    [Column("num_enum_value")]
    public NumericEnum? NumericEnumValue { get; set; }
}
