using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PhenX.EntityFrameworkCore.BulkInsert.Tests.DbContext;

[Table("test_entity_json")]
public class TestEntityWithJson : TestEntityBase
{
    [Key]
    public int Id { get; set; }

    public List<int> JsonArray { get; set; } = [];

    public JsonDbObject? JsonObject { get; set; }
}
