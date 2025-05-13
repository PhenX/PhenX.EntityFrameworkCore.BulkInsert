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
    public static async Task<List<T>> ExecuteInsertReturnEntitiesAsync<T>(
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

    /// <summary>
    /// Executes a bulk insert operation returning the inserted/updated entities, from the DbContext.
    /// </summary>
    public static async Task<List<T>> ExecuteInsertReturnEntitiesAsync<T>(this DbContext dbContext, IEnumerable<T> entities, Action<BulkInsertOptions>? configure = null, OnConflictOptions? onConflict = null, CancellationToken cancellationToken = default) where T : class
    {
        var dbSet = dbContext.Set<T>();
        if (dbSet == null)
        {
            throw new InvalidOperationException($"DbSet of type {typeof(T).Name} not found in DbContext.");
        }

        return await dbSet.ExecuteInsertReturnEntitiesAsync(entities, configure, onConflict, cancellationToken);
    }

    /// <summary>
    /// Executes a bulk insert operation without returning the inserted/updated entities, from the DbSet.
    /// </summary>
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

    /// <summary>
    /// Executes a bulk insert operation without returning the inserted/updated entities, from the DbContext.
    /// </summary>
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
