using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PhenX.EntityFrameworkCore.BulkInsert.Tests.DbContext;

[Table("test_entity_with_converters")]
public class TestEntityWithConverters
{
    public int Id { get; set; }

    [Column("name")]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }
}

