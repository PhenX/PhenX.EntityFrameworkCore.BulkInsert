using System.ComponentModel.DataAnnotations.Schema;

namespace PhenX.EntityFrameworkCore.BulkInsert.Tests.DbContext;

[Table("test_entity_with_arrays")]
public class TestEntityWithArrays : TestEntityBase
{
    public int Id { get; set; }

    [Column("enum_list")]
    public List<NumericEnum>? EnumList { get; set; }

    [Column("int_array")]
    public int[]? IntArray { get; set; }

    [Column("string_array")]
    public string[]? StringArray { get; set; }
}
