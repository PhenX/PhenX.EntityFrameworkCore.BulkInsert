using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

using PhenX.EntityFrameworkCore.BulkInsert.Abstractions;
using PhenX.EntityFrameworkCore.BulkInsert.Options;

namespace PhenX.EntityFrameworkCore.BulkInsert.Extensions;

/// <summary>
/// DbSet extensions for bulk insert operations.
/// </summary>
public static class DbSetExtensions
{
    /// <summary>
    /// Executes a bulk insert operation returning the inserted/updated entities, from the DbSet (synchronous variant).
    /// </summary>
    public static List<T> ExecuteBulkInsertReturnEntities<T>(
        this DbSet<T> dbSet,
        IEnumerable<T> entities,
        Action<BulkInsertOptions>? configure = null,
        OnConflictOptions? onConflict = null
    ) where T : class
    {
        return dbSet.ExecuteBulkInsertReturnEntitiesCoreAsync(true, entities, configure, onConflict, default).GetAwaiter().GetResult();
    }

    /// <summary>
    /// Executes a bulk insert operation returning the inserted/updated entities, from the DbContext (synchronous variant).
    /// </summary>
    public static List<T> ExecuteBulkInsertReturnEntities<T>(
        this DbContext dbContext,
        IEnumerable<T> entities,
        Action<BulkInsertOptions>? configure = null,
        OnConflictOptions? onConflict = null
    ) where T : class
    {
        var dbSet = dbContext.Set<T>() ?? throw new InvalidOperationException($"DbSet of type {typeof(T).Name} not found in DbContext.");

        return dbSet.ExecuteBulkInsertReturnEntitiesCoreAsync(true, entities, configure, onConflict, default).GetAwaiter().GetResult();
    }

    /// <summary>
    /// Executes a bulk insert operation returning the inserted/updated entities, from the DbContext.
    /// </summary>
    public static Task<List<T>> ExecuteBulkInsertReturnEntitiesAsync<T>(
        this DbContext dbContext,
        IEnumerable<T> entities,
        Action<BulkInsertOptions>? configure = null,
        OnConflictOptions? onConflict = null,
        CancellationToken ctk = default
    ) where T : class
    {
        var dbSet = dbContext.Set<T>() ?? throw new InvalidOperationException($"DbSet of type {typeof(T).Name} not found in DbContext.");

        return dbSet.ExecuteBulkInsertReturnEntitiesCoreAsync(false, entities, configure, onConflict, ctk);
    }

    /// <summary>
    /// Executes a bulk insert operation returning the inserted/updated entities, from the DbSet.
    /// </summary>
    public static Task<List<T>> ExecuteBulkInsertReturnEntitiesAsync<T>(
        this DbSet<T> dbSet,
        IEnumerable<T> entities,
        Action<BulkInsertOptions>? configure = null,
        OnConflictOptions? onConflict = null,
        CancellationToken ctk = default
    ) where T : class
    {
        return dbSet.ExecuteBulkInsertReturnEntitiesCoreAsync(false, entities, configure, onConflict, ctk);
    }

    private static async Task<List<T>> ExecuteBulkInsertReturnEntitiesCoreAsync<T>(
        this DbSet<T> dbSet,
        bool sync,
        IEnumerable<T> entities,
        Action<BulkInsertOptions>? configure,
        OnConflictOptions? onConflict,
        CancellationToken ctk
    ) where T : class
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

    /// <summary>
    /// Executes a bulk insert operation returning the inserted/updated entities, from the DbContext.
    /// </summary>
    public static IAsyncEnumerable<T> ExecuteBulkInsertReturnEnumerableAsync<T>(
        this DbContext dbContext,
        IEnumerable<T> entities,
        Action<BulkInsertOptions>? configure = null,
        OnConflictOptions? onConflict = null,
        CancellationToken ctk = default
    ) where T : class
    {
        var dbSet = dbContext.Set<T>() ?? throw new InvalidOperationException($"DbSet of type {typeof(T).Name} not found in DbContext.");

        return dbSet.ExecuteBulkInsertReturnEnumerableAsync(entities, configure, onConflict, ctk);
    }

    /// <summary>
    /// Executes a bulk insert operation returning the inserted/updated entities, from the DbSet.
    /// </summary>
    public static IAsyncEnumerable<T> ExecuteBulkInsertReturnEnumerableAsync<T>(
        this DbSet<T> dbSet,
        IEnumerable<T> entities,
        Action<BulkInsertOptions>? configure = null,
        OnConflictOptions? onConflict = null,
        CancellationToken ctk = default
    ) where T : class
    {
        var provider = InitProvider(dbSet, configure, out var context, out var options);

        return provider.BulkInsertReturnEntities(false, context, dbSet.GetDbContext().GetTableInfo<T>(), entities, options, onConflict, ctk);
    }

    /// <summary>
    /// Executes a bulk insert operation without returning the inserted/updated entities, from the DbContext.
    /// </summary>
    public static async Task ExecuteBulkInsertAsync<T>(
        this DbContext dbContext,
        IEnumerable<T> entities,
        Action<BulkInsertOptions>? configure = null,
        OnConflictOptions? onConflict = null,
        CancellationToken ctk = default
    ) where T : class
    {
        var dbSet = dbContext.Set<T>() ?? throw new InvalidOperationException($"DbSet of type {typeof(T).Name} not found in DbContext.");

        await dbSet.ExecuteBulkInsertAsync(entities, configure, onConflict, ctk);
    }

    /// <summary>
    /// Executes a bulk insert operation without returning the inserted/updated entities, from the DbSet.
    /// </summary>
    public static async Task ExecuteBulkInsertAsync<T>(
        this DbSet<T> dbSet,
        IEnumerable<T> entities,
        Action<BulkInsertOptions>? configure = null,
        OnConflictOptions? onConflict = null,
        CancellationToken ctk = default
    ) where T : class
    {
        var provider = InitProvider(dbSet, configure, out var context, out var options);

        await provider.BulkInsert(false, context, dbSet.GetDbContext().GetTableInfo<T>(), entities, options, onConflict, ctk);
    }

    /// <summary>
    /// Executes a bulk insert operation without returning the inserted/updated entities, from the DbContext (synchronous variant).
    /// </summary>
    public static void ExecuteBulkInsert<T>(
        this DbContext dbContext,
        IEnumerable<T> entities,
        Action<BulkInsertOptions>? configure = null,
        OnConflictOptions? onConflict = null
    ) where T : class
    {
        var dbSet = dbContext.Set<T>() ?? throw new InvalidOperationException($"DbSet of type {typeof(T).Name} not found in DbContext.");

        dbSet.ExecuteBulkInsert(entities, configure, onConflict);
    }

    /// <summary>
    /// Executes a bulk insert operation without returning the inserted/updated entities, from the DbSet (synchronous variant).
    /// </summary>
    public static void ExecuteBulkInsert<T>(
        this DbSet<T> dbSet,
        IEnumerable<T> entities,
        Action<BulkInsertOptions>? configure = null,
        OnConflictOptions? onConflict = null
    ) where T : class
    {
        var provider = InitProvider(dbSet, configure, out var context, out var options);

        provider.BulkInsert(true, context, dbSet.GetDbContext().GetTableInfo<T>(), entities, options, onConflict).GetAwaiter().GetResult();
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
