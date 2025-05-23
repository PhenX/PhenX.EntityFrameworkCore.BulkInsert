using Microsoft.Extensions.Logging;

namespace PhenX.EntityFrameworkCore.BulkInsert;

internal static partial class Log
{
    [LoggerMessage(
        EventId = 1000,
        Level = LogLevel.Trace,
        Message = "Using temporary table to return data")]
    public static partial void UsingTempTablToReturnData(ILogger logger);

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
}
