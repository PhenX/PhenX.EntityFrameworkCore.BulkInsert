using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

using Microsoft.EntityFrameworkCore;

namespace PhenX.EntityFrameworkCore.BulkInsert.Tests.DbContext;

[PrimaryKey(nameof(Id))]
[Table("test_entity_guids")]
public class TestEntityWithGuidId
{
    public Guid Id { get; set; }

    [Column("name")]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [Column("test_run")]
    public Guid TestRun { get; set; }
}
