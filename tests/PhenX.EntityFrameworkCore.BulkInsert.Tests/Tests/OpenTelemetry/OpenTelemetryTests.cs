using System.Collections.Concurrent;
using System.Diagnostics;

using FluentAssertions;

using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

using OpenTelemetry.Trace;

using PhenX.EntityFrameworkCore.BulkInsert.Extensions;
using PhenX.EntityFrameworkCore.BulkInsert.OpenTelemetry;
using PhenX.EntityFrameworkCore.BulkInsert.Sqlite;
using PhenX.EntityFrameworkCore.BulkInsert.Tests.DbContext;

using Xunit;

namespace PhenX.EntityFrameworkCore.BulkInsert.Tests.Tests.OpenTelemetry;

[Trait("Category", "Sqlite")]
public class OpenTelemetryTests
{
    [Fact]
    public void AddInstrumentation_RegistersBulkInsertActivitySource()
    {
        // Arrange
        var builder = new RecordingTracerProviderBuilder();

        // Act
        var result = builder.AddPhenXEntityFrameworkCoreBulkInsertInstrumentation();

        // Assert
        result.Should().BeSameAs(builder);
        builder.Sources.Should().ContainSingle().Which.Should().Be(Telemetry.ActivitySource.Name);
    }

    [Fact]
    public async Task BulkInsert_EmitsActivities_FromRegisteredSource()
    {
        // Arrange
        var builder = new RecordingTracerProviderBuilder();
        builder.AddPhenXEntityFrameworkCoreBulkInsertInstrumentation();

        // A dedicated parent source isolates this test's trace from activities emitted by tests running in parallel.
        using var testSource = new ActivitySource($"{nameof(OpenTelemetryTests)}.{Guid.NewGuid():N}");
        var stoppedActivities = new ConcurrentQueue<Activity>();
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source == testSource || builder.Sources.Contains(source.Name),
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = stoppedActivities.Enqueue,
        };
        ActivitySource.AddActivityListener(listener);

        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();

        await using var context = new TestDbContextSqlite
        {
            ConfigureOptions = options => options.UseSqlite(connection).UseBulkInsertSqlite(),
        };
        await context.Database.EnsureCreatedAsync();

        var run = Guid.NewGuid();
        var entities = new List<TestEntity>
        {
            new TestEntity { TestRun = run, Name = "Entity1" },
            new TestEntity { TestRun = run, Name = "Entity2" },
        };

        // Act
        ActivityTraceId traceId;
        using (var parent = testSource.StartActivity("Test"))
        {
            Assert.NotNull(parent);
            traceId = parent.TraceId;

            await context.ExecuteBulkInsertAsync(entities);
        }

        // Assert
        stoppedActivities
            .Where(a => a.TraceId == traceId && a.Source.Name == Telemetry.ActivitySource.Name)
            .Select(a => a.OperationName)
            .Should().Contain("BulkInsert");
    }

    private sealed class RecordingTracerProviderBuilder : TracerProviderBuilder
    {
        public List<string> Sources { get; } = [];

        public override TracerProviderBuilder AddInstrumentation<TInstrumentation>(Func<TInstrumentation> instrumentationFactory)
            => this;

        public override TracerProviderBuilder AddSource(params string[] names)
        {
            Sources.AddRange(names);
            return this;
        }

        public override TracerProviderBuilder AddLegacySource(string operationName)
            => this;
    }
}
