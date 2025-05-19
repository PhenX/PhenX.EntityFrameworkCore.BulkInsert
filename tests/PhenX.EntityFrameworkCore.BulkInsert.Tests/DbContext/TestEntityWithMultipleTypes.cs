using System.ComponentModel.DataAnnotations.Schema;

using Microsoft.EntityFrameworkCore;

namespace PhenX.EntityFrameworkCore.BulkInsert.Tests.DbContext;

[PrimaryKey(nameof(Id))]
[Table("test_entity_multiple_types")]
public class TestEntityWithMultipleTypes
{
    public int Id { get; set; }

    [Column("some_price")]
    public decimal Price { get; set; }

    public decimal? NewPrice { get; set; }

    [Column("nullable_identifier")]
    public Guid? NullableIdentifier { get; set; }

    [Column("child_entity")]
    public JsonEntity? SubEntity { get; set; }
}

public class JsonEntity
{
    public string Name { get; set; } = null!;

    public decimal Value { get; set; }
}
