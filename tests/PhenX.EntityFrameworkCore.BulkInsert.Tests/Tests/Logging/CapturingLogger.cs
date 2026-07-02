using Microsoft.Extensions.Logging;

namespace PhenX.EntityFrameworkCore.BulkInsert.Tests.Tests.Logging;

internal sealed record LogEntry(LogLevel Level, EventId EventId, string Message);

internal sealed class CapturingLogger : ILogger
{
    private readonly List<LogEntry> _entries;

    public CapturingLogger(List<LogEntry> entries)
    {
        _entries = entries;
    }

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        _entries.Add(new LogEntry(logLevel, eventId, formatter(state, exception)));
    }

    private sealed class NullScope : IDisposable
    {
        public static readonly NullScope Instance = new();
        public void Dispose() { }
    }
}

internal sealed class CapturingLoggerProvider(List<LogEntry> entries) : ILoggerProvider
{
    public ILogger CreateLogger(string categoryName) => new CapturingLogger(entries);
    public void Dispose() { }
}
