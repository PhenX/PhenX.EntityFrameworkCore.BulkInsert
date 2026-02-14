using System.ComponentModel.DataAnnotations.Schema;

namespace PhenX.EntityFrameworkCore.BulkInsert.Tests.DbContext;

/// <summary>
/// BlogSettings entity for testing one-to-one relationships.
/// </summary>
[Table("blog_settings")]
public class BlogSettings : TestEntityBase
{
    public int Id { get; set; }
    public int BlogId { get; set; }
    public Blog Blog { get; set; } = null!;
    public bool EnableComments { get; set; }
}
