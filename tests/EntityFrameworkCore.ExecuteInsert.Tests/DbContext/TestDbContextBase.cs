using System;

using Microsoft.EntityFrameworkCore;

namespace EntityFrameworkCore.ExecuteInsert.Tests.DbContext;

public abstract class TestDbContextBase : Microsoft.EntityFrameworkCore.DbContext
{
    public Action<DbContextOptionsBuilder> ConfigureOptions { get; init; } = null!;

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder) => ConfigureOptions(optionsBuilder);
}
