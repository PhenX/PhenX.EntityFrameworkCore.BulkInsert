using FluentAssertions;

using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using PhenX.EntityFrameworkCore.BulkInsert.Extensions;
using PhenX.EntityFrameworkCore.BulkInsert.Sqlite;
using PhenX.EntityFrameworkCore.BulkInsert.Tests.DbContext;

using Xunit;

namespace PhenX.EntityFrameworkCore.BulkInsert.Tests.Tests.Logging;

[Trait("Category", "Sqlite")]
public class LoggingTests
{
    [Fact]
    public async Task BulkInsert_LogsBulkInsertExecuted_AtInformationLevel()
    {
        // Arrange
        var logEntries = new List<LogEntry>();
        using var loggerFactory = LoggerFactory.Create(builder =>
            builder
                .AddProvider(new CapturingLoggerProvider(logEntries))
                .SetMinimumLevel(LogLevel.Debug));

        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();

        var context = new TestDbContextSqlite
        {
            ConfigureOptions = builder => builder
                .UseSqlite(connection)
                .UseLoggerFactory(loggerFactory)
                .UseBulkInsertSqlite(),
        };
        await context.Database.EnsureCreatedAsync();

        var run = Guid.NewGuid();
        var entities = new List<TestEntity>
        {
            new TestEntity { TestRun = run, Name = "Entity1" },
            new TestEntity { TestRun = run, Name = "Entity2" },
        };

        // Act
        await context.ExecuteBulkInsertAsync(entities);

        // Assert
        logEntries.Should().Contain(e =>
            e.Level == LogLevel.Information &&
            e.EventId.Id == 1004 &&
            e.Message.Contains("Executed BulkInsert"));
    }

    [Fact]
    public async Task BulkInsert_LogsDbCommands_AtDebugLevel()
    {
        // Arrange
        var logEntries = new List<LogEntry>();
        using var loggerFactory = LoggerFactory.Create(builder =>
            builder
                .AddProvider(new CapturingLoggerProvider(logEntries))
                .SetMinimumLevel(LogLevel.Debug));

        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();

        var context = new TestDbContextSqlite
        {
            ConfigureOptions = builder => builder
                .UseSqlite(connection)
                .UseLoggerFactory(loggerFactory)
                .UseBulkInsertSqlite(),
        };
        await context.Database.EnsureCreatedAsync();

        var run = Guid.NewGuid();
        var entities = new List<TestEntity>
        {
            new TestEntity { TestRun = run, Name = "Entity1" },
        };

        // Act - ReturnEntities always uses a temp table, triggering auxiliary SQL commands
        await context.ExecuteBulkInsertReturnEntitiesAsync(entities);

        // Assert
        logEntries.Should().Contain(e =>
            e.Level == LogLevel.Debug &&
            e.EventId.Id == 1005 &&
            e.Message.Contains("Executed DbCommand"));
    }

    [Fact]
    public async Task BulkInsert_LogMessageContainsTableName()
    {
        // Arrange
        var logEntries = new List<LogEntry>();
        using var loggerFactory = LoggerFactory.Create(builder =>
            builder
                .AddProvider(new CapturingLoggerProvider(logEntries))
                .SetMinimumLevel(LogLevel.Information));

        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();

        var context = new TestDbContextSqlite
        {
            ConfigureOptions = builder => builder
                .UseSqlite(connection)
                .UseLoggerFactory(loggerFactory)
                .UseBulkInsertSqlite(),
        };
        await context.Database.EnsureCreatedAsync();

        var run = Guid.NewGuid();
        var entities = new List<TestEntity>
        {
            new TestEntity { TestRun = run, Name = "Entity1" },
        };

        // Act
        await context.ExecuteBulkInsertAsync(entities);

        // Assert
        logEntries.Should().Contain(e =>
            e.Level == LogLevel.Information &&
            e.EventId.Id == 1004 &&
            e.Message.Contains("test_entity"));
    }
}
