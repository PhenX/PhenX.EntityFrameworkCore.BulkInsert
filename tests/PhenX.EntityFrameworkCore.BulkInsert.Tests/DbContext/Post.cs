using System.ComponentModel.DataAnnotations.Schema;

namespace PhenX.EntityFrameworkCore.BulkInsert.Tests.DbContext;

/// <summary>
/// Post entity for testing one-to-many and many-to-many relationships.
/// </summary>
[Table("post")]
public class Post : TestEntityBase
{
    public int Id { get; set; }
    public string Title { get; set; } = null!;
    public int BlogId { get; set; }
    public Blog Blog { get; set; } = null!;
    public ICollection<Tag> Tags { get; set; } = new List<Tag>();
}
