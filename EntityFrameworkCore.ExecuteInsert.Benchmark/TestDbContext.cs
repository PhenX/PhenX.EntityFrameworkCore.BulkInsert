using EntityFrameworkCore.ExecuteInsert.PostgreSql;

using Microsoft.EntityFrameworkCore;

namespace EntityFrameworkCore.ExecuteInsert.Benchmark;

public class TestDbContext : DbContext
{
    public DbSet<TestEntity> TestEntities { get; set; } = null!;

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder
            .UseNpgsql()
            .UseExecuteInsertPostgres();
    }
}
