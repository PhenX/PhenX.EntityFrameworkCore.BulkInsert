using System;

using Microsoft.EntityFrameworkCore;

namespace EntityFrameworkCore.ExecuteInsert.Tests;

public abstract class BulkDbContext : DbContext
{
    public Action<DbContextOptionsBuilder> ConfigureOptions { get; init; } = null!;

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder) => ConfigureOptions(optionsBuilder);
}
