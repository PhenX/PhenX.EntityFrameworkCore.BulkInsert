using System.Runtime.CompilerServices;

namespace PhenX.EntityFrameworkCore.BulkInsert.Graph;

/// <summary>
/// Compares pairs of entity references for equality using reference equality.
/// Used for deduplicating many-to-many join records.
/// </summary>
internal sealed class EntityPairEqualityComparer : IEqualityComparer<(object Left, object Right)>
{
    public static readonly EntityPairEqualityComparer Instance = new();

    private EntityPairEqualityComparer() { }

    public bool Equals((object Left, object Right) x, (object Left, object Right) y)
    {
        return ReferenceEquals(x.Left, y.Left) && ReferenceEquals(x.Right, y.Right);
    }

    public int GetHashCode((object Left, object Right) obj)
    {
        return HashCode.Combine(
            RuntimeHelpers.GetHashCode(obj.Left),
            RuntimeHelpers.GetHashCode(obj.Right)
        );
    }
}

