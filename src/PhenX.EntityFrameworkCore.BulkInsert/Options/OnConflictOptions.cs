using System.Linq.Expressions;

namespace PhenX.EntityFrameworkCore.BulkInsert.Options;

public abstract class OnConflictOptions
{
    /// <summary>
    /// Optional condition to apply on conflict.
    /// </summary>
    public string? Condition {get; set; }
}

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
