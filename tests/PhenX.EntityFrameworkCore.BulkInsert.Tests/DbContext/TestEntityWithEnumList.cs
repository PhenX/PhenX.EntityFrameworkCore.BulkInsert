using System.ComponentModel.DataAnnotations.Schema;

using Microsoft.EntityFrameworkCore;

namespace PhenX.EntityFrameworkCore.BulkInsert.Tests.DbContext;

[PrimaryKey(nameof(Id))]
[Table("test_entity_with_enum_list")]
public class TestEntityWithEnumList : TestEntityBase
{
    public int Id { get; set; }

    [Column("enum_list")]
    public List<NumericEnum> EnumList { get; set; } = [];
}

[PrimaryKey(nameof(Id))]
[Table("test_entity_with_enum_array")]
public class TestEntityWithEnumArray : TestEntityBase
{
    public int Id { get; set; }

    [Column("enum_array")]
    public NumericEnum[] EnumArray { get; set; } = [];
}

[PrimaryKey(nameof(Id))]
[Table("test_entity_with_int_list")]
public class TestEntityWithIntList : TestEntityBase
{
    public int Id { get; set; }

    [Column("int_list")]
    public List<int> IntList { get; set; } = [];
}

/// <summary>
/// Same as <see cref="TestEntityWithEnumList"/> but its <c>List&lt;NumericEnum&gt;</c>
/// property is additionally configured with <c>HasColumnType("integer[]")</c> via
/// Fluent API, which is a common pattern in user code.
/// </summary>
[PrimaryKey(nameof(Id))]
[Table("test_entity_with_enum_list_explicit_type")]
public class TestEntityWithEnumListExplicitType : TestEntityBase
{
    public int Id { get; set; }

    [Column("enum_list")]
    public List<NumericEnum> EnumList { get; set; } = [];
}

/// <summary>
/// Positional record that mirrors the reporter's shape:
/// <c>record Item(List&lt;E&gt; Values)</c>.
/// Properties are <c>init</c>-only, populated via the primary constructor.
/// </summary>
[PrimaryKey(nameof(Id))]
[Table("test_record_with_enum_list")]
public record TestRecordWithEnumList(int Id, Guid TestRun, List<NumericEnum> Values);
