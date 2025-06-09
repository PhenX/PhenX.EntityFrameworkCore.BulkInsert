using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PhenX.EntityFrameworkCore.BulkInsert.Tests.DbContext;

[Table("test_entity_complex_type")]
public class TestEntityWithComplexType : TestEntityBase
{
    [Key]
    public int Id { get; set; }

    public OwnedObject OwnedComplexType { get; set; } = null!;
}
