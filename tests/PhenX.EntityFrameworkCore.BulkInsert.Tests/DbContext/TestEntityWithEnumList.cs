using System.ComponentModel.DataAnnotations.Schema;

using Microsoft.EntityFrameworkCore;

namespace PhenX.EntityFrameworkCore.BulkInsert.Tests.DbContext;

/// <summary>
/// Minimal entity that exactly mirrors the shape reported in GitHub issue #98:
/// a non-nullable <c>List&lt;E&gt;</c> property (where <c>E</c> is a .NET enum)
/// mapped to a PostgreSQL <c>integer[]</c> column.
/// </summary>
[PrimaryKey(nameof(Id))]
[Table("test_entity_with_enum_list")]
public class TestEntityWithEnumList : TestEntityBase
{
    public int Id { get; set; }

    /// <summary>Non-nullable list of enum values, matching the issue sample.</summary>
    [Column("enum_list")]
    public List<NumericEnum> EnumList { get; set; } = [];
}

/// <summary>
/// Similar to <see cref="TestEntityWithEnumList"/> but with a <c>NumericEnum[]</c>
/// (native array) instead of <c>List&lt;NumericEnum&gt;</c>.  Used to compare
/// whether the issue is specific to <c>List&lt;T&gt;</c> or affects arrays as well.
/// </summary>
[PrimaryKey(nameof(Id))]
[Table("test_entity_with_enum_array")]
public class TestEntityWithEnumArray : TestEntityBase
{
    public int Id { get; set; }

    [Column("enum_array")]
    public NumericEnum[] EnumArray { get; set; } = [];
}

/// <summary>
/// Has a <c>List&lt;int&gt;</c> property to determine whether the potential issue
/// is enum-specific or affects all <c>List&lt;T&gt;</c> types.
/// </summary>
[PrimaryKey(nameof(Id))]
[Table("test_entity_with_int_list")]
public class TestEntityWithIntList : TestEntityBase
{
    public int Id { get; set; }

    [Column("int_list")]
    public List<int> IntList { get; set; } = [];
}
