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

    /// <summary>
    /// The actual root entity instances passed to the collector, in input order. Captured directly so
    /// callers can return them without a type-keyed lookup (which misses TPH-derived and proxy roots).
    /// </summary>
    public required List<object> RootEntities { get; init; }
}
