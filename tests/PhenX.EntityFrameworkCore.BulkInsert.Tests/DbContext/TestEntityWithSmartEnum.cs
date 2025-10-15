using Microsoft.EntityFrameworkCore;

namespace PhenX.EntityFrameworkCore.BulkInsert.Tests.DbContext;

[PrimaryKey(nameof(Id))]
public class TestEntityWithSmartEnum : TestEntityBase
{
    public int Id { get; set; }

    public TestSmartEnum Enum { get; set; } = null!;
}
