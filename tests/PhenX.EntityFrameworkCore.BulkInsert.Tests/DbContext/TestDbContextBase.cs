using Microsoft.EntityFrameworkCore;

namespace PhenX.EntityFrameworkCore.BulkInsert.Tests.DbContext;

public abstract class TestDbContextBase : Microsoft.EntityFrameworkCore.DbContext
{
    public Action<DbContextOptionsBuilder>? ConfigureOptions { get; init; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder) => (ConfigureOptions ?? throw new InvalidOperationException("ConfigureOptions must be set")).Invoke(optionsBuilder);
}
