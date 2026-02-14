using System.ComponentModel.DataAnnotations.Schema;

namespace PhenX.EntityFrameworkCore.BulkInsert.Tests.DbContext;

/// <summary>
/// Tag entity for testing many-to-many relationships.
/// </summary>
[Table("tag")]
public class Tag : TestEntityBase
{
    public int Id { get; set; }
    public string Name { get; set; } = null!;
    public ICollection<Post> Posts { get; set; } = new List<Post>();
}
