using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

using Microsoft.EntityFrameworkCore;

namespace PhenX.EntityFrameworkCore.BulkInsert.Tests.DbContext;

[Keyless]
public class TestEntityKeyless : TestEntityBase
{
    public int Id { get; set; }

    [Column("name")]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [Column("some_price")]
    public decimal Price { get; set; }
}
