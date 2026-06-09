using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PhenX.EntityFrameworkCore.BulkInsert.Tests.DbContext;

[Table("test_entity_json_complex_collection")]
public class TestEntityWithJsonComplexCollection : TestEntityBase
{
    [Key]
    public int Id { get; set; }

    public List<JsonComplexItem>? Items { get; set; }
}

public class JsonComplexItem
{
    public required string Name { get; set; }
    public int Value { get; set; }
}
