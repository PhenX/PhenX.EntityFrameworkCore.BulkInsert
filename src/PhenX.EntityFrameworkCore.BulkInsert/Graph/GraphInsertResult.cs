namespace PhenX.EntityFrameworkCore.BulkInsert.Graph;

/// <summary>
/// Result of a graph insert operation.
/// </summary>
/// <typeparam name="T">The root entity type.</typeparam>
internal sealed class GraphInsertResult<T> where T : class
{
    /// <summary>
    /// The root entities that were inserted.
    /// </summary>
    public required IReadOnlyList<T> RootEntities { get; init; }

    /// <summary>
    /// Total count of all entities inserted across all types.
    /// </summary>
    public required int TotalInsertedCount { get; init; }
}
