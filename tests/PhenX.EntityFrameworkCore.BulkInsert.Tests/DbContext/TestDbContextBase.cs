using Microsoft.EntityFrameworkCore;

namespace PhenX.EntityFrameworkCore.BulkInsert.Tests.DbContext;

public abstract class TestDbContextBase : Microsoft.EntityFrameworkCore.DbContext
{
    public Action<DbContextOptionsBuilder> ConfigureOptions { get; init; } = null!;

    public Action<ModelBuilder> ConfigureModel { get; init; } = null!;

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder) => ConfigureOptions(optionsBuilder);
}
