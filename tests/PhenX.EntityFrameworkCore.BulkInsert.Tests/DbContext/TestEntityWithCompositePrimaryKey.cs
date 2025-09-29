using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

using Microsoft.EntityFrameworkCore;

namespace PhenX.EntityFrameworkCore.BulkInsert.Tests.DbContext;

[PrimaryKey(nameof(Id), nameof(DateTimeUtc))]
[Table("test_entity_composite_pk")]
public class TestEntityWithCompositePrimaryKey : TestEntityBase
{
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public long Id { get; set; }

    [Column("datetime_utc")]
    public required DateTime DateTimeUtc { get; set; }

    [Column("name")]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [Column("value")]
    public int Value { get; set; }
}