using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PhenX.EntityFrameworkCore.BulkInsert.Tests.DbContext;

[Table("test_entity_generated_guids")]
public class TestEntityWithGeneratedGuidId
{
    [Key]
    public Guid Id { get; set; }

    [Column("name")]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [Column("test_run")]
    public Guid TestRun { get; set; }
}
