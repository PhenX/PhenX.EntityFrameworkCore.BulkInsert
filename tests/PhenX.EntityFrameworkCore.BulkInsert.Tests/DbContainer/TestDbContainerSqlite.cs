using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

using PhenX.EntityFrameworkCore.BulkInsert.Sqlite;
using PhenX.EntityFrameworkCore.BulkInsert.Tests.DbContext;

using Xunit;

namespace PhenX.EntityFrameworkCore.BulkInsert.Tests.DbContainer;

[CollectionDefinition(Name)]
public class TestDbContainerSqliteCollection : ICollectionFixture<TestDbContainerSqlite>
{
    public const string Name = "Sqlite";
}

public sealed class TestDbContainerSqlite : IDbContextFactory, IDisposable
{
    private SqliteConnection? _connection;

    public async Task<TDbContext> CreateContextAsync<TDbContext>(string databaseName) where TDbContext : TestDbContextBase, new()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        await _connection.OpenAsync();

        var dbContext = new TDbContext
        {
            ConfigureOptions = builder => builder.UseSqlite(_connection).UseBulkInsertSqlite(),
        };
        await dbContext.Database.EnsureCreatedAsync();

        return dbContext;
    }

    public void Dispose()
    {
        _connection?.Dispose();
    }
}
