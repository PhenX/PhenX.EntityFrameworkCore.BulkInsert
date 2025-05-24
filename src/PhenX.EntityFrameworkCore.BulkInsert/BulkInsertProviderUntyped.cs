using Microsoft.EntityFrameworkCore;

using PhenX.EntityFrameworkCore.BulkInsert.Abstractions;
using PhenX.EntityFrameworkCore.BulkInsert.Dialect;
using PhenX.EntityFrameworkCore.BulkInsert.Metadata;
using PhenX.EntityFrameworkCore.BulkInsert.Options;

namespace PhenX.EntityFrameworkCore.BulkInsert;

internal abstract class BulkInsertProviderUntyped<TDialect, TOptions> : IBulkInsertProvider
    where TDialect : SqlDialectBuilder, new()
    where TOptions : BulkInsertOptions, new()
{
    protected readonly TDialect SqlDialect = new();

    SqlDialectBuilder IBulkInsertProvider.SqlDialect => SqlDialect;

    BulkInsertOptions IBulkInsertProvider.CreateDefaultOptions() => CreateDefaultOptions();

    /// <summary>
    /// Create the default options for the provider, can be a subclass of <see cref="BulkInsertOptions"/>.
    /// </summary>
    protected abstract TOptions CreateDefaultOptions();

    public IAsyncEnumerable<T> BulkInsertReturnEntities<T>(
        bool sync,
        DbContext context,
        TableMetadata tableInfo,
        IEnumerable<T> entities,
        BulkInsertOptions options,
        OnConflictOptions<T>? onConflict,
        CancellationToken ctk) where T : class
    {
        if (options is not TOptions providerOptions)
        {
            throw new InvalidOperationException($"Invalid options type: {options.GetType().Name}. Expected: {typeof(TOptions).Name}");
        }

        return BulkInsertReturnEntities(sync, context, tableInfo, entities, providerOptions, onConflict, ctk);
    }

    protected abstract IAsyncEnumerable<T> BulkInsertReturnEntities<T>(
        bool sync,
        DbContext context,
        TableMetadata tableInfo,
        IEnumerable<T> entities,
        TOptions options,
        OnConflictOptions<T>? onConflict,
        CancellationToken ctk) where T : class;

    public Task BulkInsert<T>(
        bool sync,
        DbContext context,
        TableMetadata tableInfo,
        IEnumerable<T> entities,
        BulkInsertOptions options,
        OnConflictOptions<T>? onConflict,
        CancellationToken ctk) where T : class
    {
        if (options is not TOptions providerOptions)
        {
            throw new InvalidOperationException($"Invalid options type: {options.GetType().Name}. Expected: {typeof(TOptions).Name}");
        }

        return BulkInsert(sync, context, tableInfo, entities, providerOptions, onConflict, ctk);
    }

    protected abstract Task BulkInsert<T>(
        bool sync,
        DbContext context,
        TableMetadata tableInfo,
        IEnumerable<T> entities,
        TOptions options,
        OnConflictOptions<T>? onConflict,
        CancellationToken ctk) where T : class;
}
