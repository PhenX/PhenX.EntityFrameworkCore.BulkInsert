using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

using NetTopologySuite.Geometries;

namespace PhenX.EntityFrameworkCore.BulkInsert.Tests.DbContext;

[Table("test_entity_geo")]
public class TestEntityWithGeo : TestEntityBase
{
    [Key]
    public int Id { get; set; }

    public Geometry GeoObject { get; set; } = null!;
}
