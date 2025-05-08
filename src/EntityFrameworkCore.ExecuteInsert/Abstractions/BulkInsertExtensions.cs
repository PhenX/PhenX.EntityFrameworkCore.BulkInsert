using EntityFrameworkCore.ExecuteInsert.OnConflict;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace EntityFrameworkCore.ExecuteInsert.Abstractions;

public static class BulkInsertExtensions
{
    public static async Task<List<T>> ExecuteInsertWithIdentityAsync<T>(
        this DbSet<T> dbSet,
        IEnumerable<T> entities,
        Action<BulkInsertOptions>? configure = null,
        OnConflictOptions? onConflict = null,
        CancellationToken ctk = default
    ) where T : class
    {
        var provider = InitProvider(dbSet, configure, out var context, out var options);

        return await provider.BulkInsertWithIdentityAsync(context, entities, options, onConflict, ctk);
    }

    public static async Task ExecuteInsertWithIdentityAsync<T>(this DbContext dbContext, IEnumerable<T> entities, Action<BulkInsertOptions>? configure = null, OnConflictOptions? onConflict = null, CancellationToken cancellationToken = default) where T : class
    {
        var dbSet = dbContext.Set<T>();
        if (dbSet == null)
        {
            throw new InvalidOperationException($"DbSet of type {typeof(T).Name} not found in DbContext.");
        }

        await dbSet.ExecuteInsertWithIdentityAsync(entities, configure, onConflict, cancellationToken);
    }

    // public static async Task<List<object>> ExecuteInsertWithPrimaryKeyAsync<T>(
    //     this DbSet<T> dbSet,
    //     IEnumerable<T> entities,
    //     Action<BulkInsertOptions>? configure = null,
    //     CancellationToken ctk = default
    // ) where T : class
    // {
    //     var provider = InitProvider(dbSet, configure, out var context, out var options);
    //
    //     return await provider.BulkInsertWithPrimaryKeyAsync(context, entities, options, ctk);
    // }
    //
    // public static async Task<List<object>> ExecuteInsertWithPrimaryKeyAsync<T>(this DbContext dbContext, IEnumerable<T> entities, Action<BulkInsertOptions>? configure = null, CancellationToken cancellationToken = default) where T : class
    // {
    //     var dbSet = dbContext.Set<T>();
    //     if (dbSet == null)
    //     {
    //         throw new InvalidOperationException($"DbSet of type {typeof(T).Name} not found in DbContext.");
    //     }
    //
    //     return await dbSet.ExecuteInsertWithPrimaryKeyAsync(entities, configure, cancellationToken);
    // }

    public static async Task ExecuteInsertAsync<T>(
        this DbSet<T> dbSet,
        IEnumerable<T> entities,
        Action<BulkInsertOptions>? configure = null,
        OnConflictOptions? onConflict = null,
        CancellationToken ctk = default
    ) where T : class
    {
        var provider = InitProvider(dbSet, configure, out var context, out var options);

        await provider.BulkInsertWithoutReturnAsync(context, entities, options, onConflict, ctk);
    }

    public static async Task ExecuteInsertAsync<T>(this DbContext dbContext, IEnumerable<T> entities, Action<BulkInsertOptions>? configure = null, OnConflictOptions? onConflict = null, CancellationToken cancellationToken = default) where T : class
    {
        var dbSet = dbContext.Set<T>();
        if (dbSet == null)
        {
            throw new InvalidOperationException($"DbSet of type {typeof(T).Name} not found in DbContext.");
        }

        await dbSet.ExecuteInsertAsync(entities, configure, onConflict, cancellationToken);
    }

    private static DbContext GetDbContext<T>(this DbSet<T> dbSet) where T : class
    {
        IInfrastructure<IServiceProvider> infrastructure = dbSet;
        return (infrastructure.Instance.GetService(typeof(ICurrentDbContext)) as ICurrentDbContext)!.Context;
    }

    private static IBulkInsertProvider InitProvider<T>(DbSet<T> dbSet, Action<BulkInsertOptions>? configure, out DbContext context,
        out BulkInsertOptions options) where T : class
    {
        context = dbSet.GetDbContext();
        var provider = context.GetService<IBulkInsertProvider>();

        options = new BulkInsertOptions();
        configure?.Invoke(options);
        return provider;
    }
}
