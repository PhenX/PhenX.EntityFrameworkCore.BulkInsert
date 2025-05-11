namespace EntityFrameworkCore.ExecuteInsert.Options;

public class BulkInsertOptions
{
    /// <summary>
    /// Insert entities recursively.
    /// </summary>
    [Obsolete("Not supported yet.")]
    public bool Recursive { get; set; }

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
    /// </list>
    /// </summary>
    public int? BatchSize { get; set; }
}
