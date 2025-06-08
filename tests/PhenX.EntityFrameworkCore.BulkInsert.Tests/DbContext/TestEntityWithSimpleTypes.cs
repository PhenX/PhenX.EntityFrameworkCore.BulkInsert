using System.ComponentModel.DataAnnotations.Schema;

using Microsoft.EntityFrameworkCore;

namespace PhenX.EntityFrameworkCore.BulkInsert.Tests.DbContext;

[PrimaryKey(nameof(Id))]
[Table("test_entity_with_simple_types")]
public class TestEntityWithSimpleTypes : TestEntityBase
{
    public int Id { get; set; }

    public required string StringValue { get; set; } = string.Empty;

    public required bool BoolValue { get; set; }

    public required byte ByteValue { get; set; }
    public required byte[]? ByteArrayValue { get; set; }
    public required sbyte SByteValue { get; set; }
    public required char CharValue { get; set; }

    public required short ShortValue { get; set; }
    public required ushort UShortValue { get; set; }

    public required int IntValue { get; set; }
    public required uint UIntValue { get; set; }

    public required long LongValue { get; set; }
    public required ulong ULongValue { get; set; }

    public required float FloatValue { get; set; }
    public required double DoubleValue { get; set; }
    public required decimal DecimalValue { get; set; }

    public required DateTime DateTimeValue { get; set; }
    public required DateOnly DateOnlyValue { get; set; }
    public required TimeOnly TimeOnlyValue { get; set; }
    public required TimeSpan TimeSpanValue { get; set; }
    public required DateTimeOffset DateTimeOffsetValue { get; set; }

    public required Guid GuidValue { get; set; }
}
