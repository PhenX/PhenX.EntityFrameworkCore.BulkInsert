using System.Diagnostics;

namespace PhenX.EntityFrameworkCore.BulkInsert;

/// <summary>
/// Utility class for telemetry.
/// </summary>
public static class Telemetry
{
    /// <summary>
    /// The activity source.
    /// </summary>
    public static readonly ActivitySource ActivitySource = new("PhenX.EntityFrameworkCore.BulkInsert");
}
