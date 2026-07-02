using PhenX.EntityFrameworkCore.BulkInsert.Metadata;

namespace PhenX.EntityFrameworkCore.BulkInsert.Graph;

/// <summary>
/// Represents a join table record for many-to-many relationships.
/// </summary>
internal sealed class JoinRecord
{
    public required Type JoinEntityType { get; init; }
    public required object LeftEntity { get; init; }
    public required object RightEntity { get; init; }
    public required NavigationMetadata Navigation { get; init; }
}
