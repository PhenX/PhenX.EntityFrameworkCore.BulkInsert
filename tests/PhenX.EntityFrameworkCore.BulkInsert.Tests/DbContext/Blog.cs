using System.ComponentModel.DataAnnotations.Schema;

namespace PhenX.EntityFrameworkCore.BulkInsert.Tests.DbContext;

/// <summary>
/// Blog entity for testing one-to-many relationships.
/// </summary>
[Table("blog")]
public class Blog : TestEntityBase
{
    public int Id { get; set; }
    public string Name { get; set; } = null!;
    public ICollection<Post> Posts { get; set; } = new List<Post>();
    public BlogSettings? Settings { get; set; }
}
