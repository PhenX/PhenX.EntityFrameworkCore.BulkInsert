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
    private static async Task<List<TEntity>> ExecuteBulkInsertReturnEntitiesCoreAsync<TEntity, TOptions>(
        this DbSet<TEntity> dbSet,
        bool sync,
        IEnumerable<TEntity> entities,
        Action<TOptions> configure,
        OnConflictOptions<TEntity>? onConflict,
        CancellationToken ctk
    )
        where TEntity : class
        where TOptions : BulkInsertOptions
    {
        var (provider, context, options) = InitProvider(dbSet, configure);

        var enumerable = provider.BulkInsertReturnEntities(sync, context, dbSet.GetDbContext().GetTableInfo<TEntity>(), entities, options, onConflict, ctk);

        var result = new List<TEntity>();
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

    private static (IBulkInsertProvider, DbContext, TOptions) InitProvider<T, TOptions>(
        DbSet<T> dbSet,
        Action<TOptions>? configure
    )
        where T : class where TOptions : BulkInsertOptions
    {
        var context = dbSet.GetDbContext();
        var provider = context.GetService<IBulkInsertProvider>();
        var options = provider.InternalCreateDefaultOptions();

        if (options is not TOptions castedOptions)
        {
            throw new InvalidOperationException($"Options type mismatch. Expected {options.GetType().Name}, but got {typeof(TOptions).Name}.");
        }

        configure?.Invoke(castedOptions);

        return (provider, context, castedOptions);
    }
}
