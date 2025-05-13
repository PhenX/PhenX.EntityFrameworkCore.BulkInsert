using System.Linq.Expressions;

namespace PhenX.EntityFrameworkCore.BulkInsert.Options;

/// <summary>
/// Conflict options for bulk insert.
/// </summary>
public abstract class OnConflictOptions
{
    /// <summary>
    /// Optional condition to apply on conflict.
    /// </summary>
    public string? Condition {get; set; }
}

/// <summary>
/// Conflict options for bulk insert, for a specific entity type.
/// </summary>
/// <typeparam name="T"></typeparam>
public class OnConflictOptions<T> : OnConflictOptions
{
    /// <summary>
    /// Columns to match on conflict.
    /// </summary>
    public Expression<Func<T, object>>? Match { get; set; }

    /// <summary>
    /// Updates to apply on conflict.
    /// </summary>
    public Expression<Func<T, object>>? Update { get; set; }
}
