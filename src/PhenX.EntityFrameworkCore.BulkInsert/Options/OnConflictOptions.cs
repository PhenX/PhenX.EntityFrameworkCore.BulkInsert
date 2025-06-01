using System.Linq.Expressions;

namespace PhenX.EntityFrameworkCore.BulkInsert.Options;

/// <summary>
/// Conflict options for bulk insert.
/// </summary>
public abstract class OnConflictOptions
{
    /// <summary>
    /// Optional condition to apply on conflict, in raw SQL.
    /// The pseudo tables `INSERTED` and `EXCLUDED` can be used to reference data :
    /// * `INSERTED` refers to the data already in the target table.
    /// * `EXCLUDED` refers to the new data, being in conflict.
    /// </summary>
    public string? RawWhere { get; set; }
}

/// <summary>
/// Conflict options for bulk insert, for a specific entity type.
/// </summary>
/// <typeparam name="T"></typeparam>
public class OnConflictOptions<T> : OnConflictOptions
{
    /// <summary>
    /// Columns to match on conflict.
    /// <example><code>
    /// Match = (inserted) => new { inserted.Id } // Match on the Id column
    /// </code></example>
    /// </summary>
    public Expression<Func<T, object>>? Match { get; set; }

    /// <summary>
    /// Updates to apply on conflict.
    /// <example><code>
    /// Update = (inserted, excluded) => new { inserted.Quantity = excluded.Quantity } // Update the Quantity column
    /// </code></example>
    /// </summary>
    public Expression<Func<T, T, object>>? Update { get; set; }

    /// <summary>
    /// Condition to apply on conflict, with an expression.
    /// <example><code>
    /// Where = (inserted, excluded) => inserted.Price > excluded.Price
    /// </code></example>
    /// </summary>
    public Expression<Func<T, T, bool>>? Where { get; set; }
}
