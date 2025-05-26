using System.ComponentModel.DataAnnotations.Schema;

namespace PhenX.EntityFrameworkCore.BulkInsert.Tests.DbContext;

public abstract class TestEntityBase
{
    [Column("test_run")]
    public Guid TestRun { get; set; }
}
