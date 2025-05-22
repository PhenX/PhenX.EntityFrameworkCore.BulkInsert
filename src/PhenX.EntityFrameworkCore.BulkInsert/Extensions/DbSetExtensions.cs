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
    /// Executes a bulk insert operation returning the inserted/updated entities, from the DbSet.
    /// </summary>
    public static async Task<List<T>> ExecuteBulkInsertReturnEntitiesAsync<T>(
        this DbSet<T> dbSet,
        IEnumerable<T> entities,
        Action<BulkInsertOptions>? configure = null,
        OnConflictOptions<T>? onConflict = null,
        CancellationToken ctk = default
    ) where T : class
    {
        var provider = InitProvider(dbSet, configure, out var context, out var options);
        var tableInfo = dbSet.GetDbContext().GetTableInfo<T>();

        return await provider.BulkInsertReturnEntities(false, context, tableInfo, entities, options, onConflict, ctk);
    }

    /// <summary>
    /// Executes a bulk insert operation returning the inserted/updated entities, from the DbContext.
    /// </summary>
    public static async Task<List<T>> ExecuteBulkInsertReturnEntitiesAsync<T>(this DbContext dbContext, IEnumerable<T> entities, Action<BulkInsertOptions>? configure = null, OnConflictOptions<T>? onConflict = null, CancellationToken cancellationToken = default) where T : class
    {
        var dbSet = dbContext.Set<T>();
        if (dbSet == null)
        {
            throw new InvalidOperationException($"DbSet of type {typeof(T).Name} not found in DbContext.");
        }

        return await dbSet.ExecuteBulkInsertReturnEntitiesAsync(entities, configure, onConflict, cancellationToken);
    }

    /// <summary>
    /// Executes a bulk insert operation without returning the inserted/updated entities, from the DbSet.
    /// </summary>
    public static async Task ExecuteBulkInsertAsync<T>(
        this DbSet<T> dbSet,
        IEnumerable<T> entities,
        Action<BulkInsertOptions>? configure = null,
        OnConflictOptions<T>? onConflict = null,
        CancellationToken ctk = default
    ) where T : class
    {
        var provider = InitProvider(dbSet, configure, out var context, out var options);
        var tableInfo = dbSet.GetDbContext().GetTableInfo<T>();

        await provider.BulkInsert(false, context, tableInfo, entities, options, onConflict, ctk);
    }

    /// <summary>
    /// Executes a bulk insert operation without returning the inserted/updated entities, from the DbContext.
    /// </summary>
    public static async Task ExecuteBulkInsertAsync<T>(this DbContext dbContext, IEnumerable<T> entities, Action<BulkInsertOptions>? configure = null, OnConflictOptions<T>? onConflict = null, CancellationToken cancellationToken = default) where T : class
    {
        var dbSet = dbContext.Set<T>();
        if (dbSet == null)
        {
            throw new InvalidOperationException($"DbSet of type {typeof(T).Name} not found in DbContext.");
        }

        await dbSet.ExecuteBulkInsertAsync(entities, configure, onConflict, cancellationToken);
    }

    /// <summary>
    /// Executes a bulk insert operation returning the inserted/updated entities, from the DbSet (synchronous variant).
    /// </summary>
    public static List<T> ExecuteBulkInsertReturnEntities<T>(
        this DbSet<T> dbSet,
        IEnumerable<T> entities,
        Action<BulkInsertOptions>? configure = null,
        OnConflictOptions<T>? onConflict = null
    ) where T : class
    {
        var provider = InitProvider(dbSet, configure, out var context, out var options);
        var tableInfo = dbSet.GetDbContext().GetTableInfo<T>();

        return provider.BulkInsertReturnEntities(true, context, tableInfo, entities, options, onConflict).GetAwaiter().GetResult();
    }

    /// <summary>
    /// Executes a bulk insert operation returning the inserted/updated entities, from the DbContext (synchronous variant).
    /// </summary>
    public static List<T> ExecuteBulkInsertReturnEntities<T>(
        this DbContext dbContext,
        IEnumerable<T> entities,
        Action<BulkInsertOptions>? configure = null,
        OnConflictOptions<T>? onConflict = null
    ) where T : class
    {
        var dbSet = dbContext.Set<T>();
        if (dbSet == null)
        {
            throw new InvalidOperationException($"DbSet of type {typeof(T).Name} not found in DbContext.");
        }

        return dbSet.ExecuteBulkInsertReturnEntities(entities, configure, onConflict);
    }

    /// <summary>
    /// Executes a bulk insert operation without returning the inserted/updated entities, from the DbSet (synchronous variant).
    /// </summary>
    public static void ExecuteBulkInsert<T>(
        this DbSet<T> dbSet,
        IEnumerable<T> entities,
        Action<BulkInsertOptions>? configure = null,
        OnConflictOptions<T>? onConflict = null
    ) where T : class
    {
        var provider = InitProvider(dbSet, configure, out var context, out var options);
        var tableInfo = dbSet.GetDbContext().GetTableInfo<T>();

        provider.BulkInsert(true, context, tableInfo, entities, options, onConflict).GetAwaiter().GetResult();
    }

    /// <summary>
    /// Executes a bulk insert operation without returning the inserted/updated entities, from the DbContext (synchronous variant).
    /// </summary>
    public static void ExecuteBulkInsert<T>(
        this DbContext dbContext,
        IEnumerable<T> entities,
        Action<BulkInsertOptions>? configure = null,
        OnConflictOptions<T>? onConflict = null
    ) where T : class
    {
        var dbSet = dbContext.Set<T>();
        if (dbSet == null)
        {
            throw new InvalidOperationException($"DbSet of type {typeof(T).Name} not found in DbContext.");
        }

        dbSet.ExecuteBulkInsert(entities, configure, onConflict);
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
