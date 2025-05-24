using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

using PhenX.EntityFrameworkCore.BulkInsert.Abstractions;
using PhenX.EntityFrameworkCore.BulkInsert.Options;

namespace PhenX.EntityFrameworkCore.BulkInsert.Extensions;

/// <summary>
/// DbSet extensions for bulk insert operations.
/// </summary>
public static partial class PublicExtensions
{
    private static async Task<List<T>> ExecuteBulkInsertReturnEntitiesCoreAsync<T, TOptions>(
        this DbSet<T> dbSet,
        bool sync,
        IEnumerable<T> entities,
        Action<TOptions> configure,
        OnConflictOptions<T>? onConflict,
        CancellationToken ctk
    )
        where T : class
        where TOptions : BulkInsertOptions
    {
        var provider = InitProvider(dbSet, configure, out var context, out var options);

        var enumerable = provider.BulkInsertReturnEntities(sync, context, dbSet.GetDbContext().GetTableInfo<T>(), entities, options, onConflict, ctk);

        var result = new List<T>();
        await foreach (var item in enumerable.WithCancellation(ctk))
        {
            result.Add(item);
        }

        return result;
    }

    private static DbContext GetDbContext<T>(this DbSet<T> dbSet) where T : class
    {
        IInfrastructure<IServiceProvider> infrastructure = dbSet;
        return (infrastructure.Instance.GetService(typeof(ICurrentDbContext)) as ICurrentDbContext)!.Context;
    }

    private static IBulkInsertProvider InitProvider<T, TOptions>(
        DbSet<T> dbSet,
        Action<TOptions>? configure,
        out DbContext context,
        out TOptions options
    )
        where T : class where TOptions : BulkInsertOptions
    {
        context = dbSet.GetDbContext();
        var provider = context.GetService<IBulkInsertProvider>();

        var defaultOptions = provider.InternalCreateDefaultOptions();

        if (defaultOptions is not TOptions castedOptions)
        {
            throw new InvalidOperationException($"Options type mismatch. Expected {defaultOptions.GetType().Name}, but got {typeof(TOptions).Name}.");
        }

        options = castedOptions;
        configure?.Invoke(options);

        return provider;
    }
}
