using PhenX.EntityFrameworkCore.BulkInsert.Abstractions;

namespace PhenX.EntityFrameworkCore.BulkInsert.Options;

/// <summary>
/// Bulk insert general options.
/// </summary>
public class BulkInsertOptions
{
    /// <summary>
    /// Progress callback delegate to notify about the number of rows copied.
    /// </summary>
    public delegate void ProgressCallback(long rowsCopied);

    /// <summary>
    /// Move rows between tables instead of inserting them.
    /// Only supported for PostgreSQL.
    /// </summary>
    public bool MoveRows { get; set; }

    /// <summary>
    /// Batch size for bulk insert.
    /// <list type="table">
    /// <listheader>
    /// <term>Default values</term>
    /// </listheader>
    /// <item>
    /// <term>PostgreSQL</term>
    /// <description>N/A</description>
    /// </item>
    /// <item>
    /// <term>SQL Server</term>
    /// <description>50 000</description>
    /// </item>
    /// <item>
    /// <term>SQLite</term>
    /// <description>5</description>
    /// </item>
    /// <item>
    /// <term>Oracle</term>
    /// <description>50 000</description>
    /// </item>
    /// </list>
    /// </summary>
    public int BatchSize { get; set; }

    /// <summary>
    /// Indicates if also generated columns should be copied. This is useful for upsert operations.
    /// </summary>
    public bool CopyGeneratedColumns { get; set; }

    /// <summary>
    /// The timeout to copy records.
    /// </summary>
    public TimeSpan CopyTimeout { get; set; } = TimeSpan.FromMinutes(10);

    /// <summary>
    /// The value converters.
    /// </summary>
    public List<IBulkValueConverter>? Converters { get; set; }

    /// <summary>
    /// Sets the ID of the Spatial Reference System used by the Geometries to be inserted.
    /// </summary>
    public int SRID { get; set; } = 4326;

    /// <summary>
    /// Number of rows after which the progress callback is invoked.
    /// </summary>
    public int? NotifyProgressAfter { get; set; }

    /// <summary>
    /// Callback to notify about the progress of the bulk insert operation.
    /// </summary>
    public ProgressCallback? OnProgress { get; set; }

    internal int GetCopyTimeoutInSeconds()
    {
        return Math.Max(0, (int)CopyTimeout.TotalSeconds);
    }

    internal void HandleOnProgress(ref long rowsCopied)
    {
        rowsCopied++;

        if (OnProgress == null || NotifyProgressAfter == null || NotifyProgressAfter <= 0 || rowsCopied % NotifyProgressAfter != 0)
        {
            return;
        }

        OnProgress(rowsCopied);
    }
}
