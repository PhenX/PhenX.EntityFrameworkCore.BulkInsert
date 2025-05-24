using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PhenX.EntityFrameworkCore.BulkInsert.Tests.DbContext;

[Table("test_entity_json")]
public class TestEntityWithJson
{
    [Key]
    public int Id { get; set; }

    public List<int> Json { get; set; } = [];

    [Column("test_run")]
    public Guid TestRun { get; set; }
}
