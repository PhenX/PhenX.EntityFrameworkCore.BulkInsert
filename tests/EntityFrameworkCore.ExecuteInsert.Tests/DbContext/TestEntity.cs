using System;
using System.ComponentModel.DataAnnotations.Schema;

using Microsoft.EntityFrameworkCore;

namespace EntityFrameworkCore.ExecuteInsert.Tests.DbContext;

[PrimaryKey(nameof(Id))]
[Index(nameof(Name), IsUnique = true)]
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
