using Microsoft.Extensions.Logging;

namespace PhenX.EntityFrameworkCore.BulkInsert;

internal static partial class Log
{
    [LoggerMessage(
        EventId = 1000,
        Level = LogLevel.Trace,
        Message = "Using temporary table to return data")]
    public static partial void UsingTempTableToReturnData(ILogger logger);

    [LoggerMessage(
        EventId = 1001,
        Level = LogLevel.Trace,
        Message = "Using temporary table to resolve conflicts")]
    public static partial void UsingTempTableToResolveConflicts(ILogger logger);

    [LoggerMessage(
        EventId = 1002,
        Level = LogLevel.Trace,
        Message = "Insert to table directly")]
    public static partial void UsingDirectInsert(ILogger logger);

    [LoggerMessage(
        EventId = 1003,
        Level = LogLevel.Error,
        Message = "Failed to drop temporary table.")]
    public static partial void DropTemporaryTableFailed(ILogger logger, Exception exception);

    [LoggerMessage(
        EventId = 1004,
        Level = LogLevel.Information,
        Message = "Executed BulkInsert ({ElapsedMs}ms) [DestinationTable='{DestinationTable}']")]
    public static partial void BulkInsertExecuted(ILogger logger, long elapsedMs, string destinationTable);

    [LoggerMessage(
        EventId = 1005,
        Level = LogLevel.Debug,
        Message = "Executed DbCommand ({ElapsedMs}ms)\n{CommandText}")]
    public static partial void ExecutedDbCommand(ILogger logger, long elapsedMs, string commandText);
}
