using Microsoft.EntityFrameworkCore;

using PhenX.EntityFrameworkCore.BulkInsert.Dialect;
using PhenX.EntityFrameworkCore.BulkInsert.Metadata;
using PhenX.EntityFrameworkCore.BulkInsert.Options;

namespace PhenX.EntityFrameworkCore.BulkInsert.Abstractions;

/// <summary>
/// Internal bulk insert provider interface.
/// </summary>
internal interface IBulkInsertProvider
{
    /// <summary>
    /// Calls the provider to perform a bulk insert operation.
    /// </summary>
    internal IAsyncEnumerable<T> BulkInsertReturnEntities<T>(
        bool sync,
        DbContext context,
        TableMetadata tableInfo,
        IEnumerable<T> entities,
        BulkInsertOptions options,
        OnConflictOptions<T>? onConflict = null,
        CancellationToken ctk = default
    ) where T : class;

    /// <summary>
    /// Calls the provider to perform a bulk insert operation without returning the inserted entities.
    /// </summary>
    internal Task BulkInsert<T>(
        bool sync,
        DbContext context,
        TableMetadata tableInfo,
        IEnumerable<T> entities,
        BulkInsertOptions options,
        OnConflictOptions<T>? onConflict = null,
        CancellationToken ctk = default
    ) where T : class;

    SqlDialectBuilder SqlDialect { get; }

    /// <summary>
    /// Make the default options for the provider, can be a subclass of <see cref="BulkInsertOptions"/>.
    /// </summary>
    internal BulkInsertOptions CreateDefaultOptions();
}
