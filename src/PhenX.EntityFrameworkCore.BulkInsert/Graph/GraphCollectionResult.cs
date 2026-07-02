namespace PhenX.EntityFrameworkCore.BulkInsert.Graph;

/// <summary>
/// Result of collecting entities from an object graph.
/// </summary>
internal sealed class GraphCollectionResult
{
    /// <summary>
    /// Entities grouped by type.
    /// </summary>
    public required Dictionary<Type, List<object>> EntitiesByType { get; init; }

    /// <summary>
    /// Types in topological insertion order (parents before children).
    /// </summary>
    public required IReadOnlyList<Type> InsertionOrder { get; init; }

    /// <summary>
    /// Many-to-many join records to insert after both sides are inserted.
    /// </summary>
    public required List<JoinRecord> JoinRecords { get; init; }
}
