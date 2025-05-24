using Microsoft.EntityFrameworkCore;

using PhenX.EntityFrameworkCore.BulkInsert.Options;

namespace PhenX.EntityFrameworkCore.BulkInsert.Extensions;

public static partial class PublicExtensions
{
    /// <summary>
    /// Executes a bulk insert operation returning the inserted/updated entities, from the DbContext (synchronous variant), with provider specific options.
    /// </summary>
    public static List<T> ExecuteBulkInsertReturnEntities<T, TConfig>(
        this DbContext dbContext,
        IEnumerable<T> entities,
        Action<TConfig> configure,
        OnConflictOptions<T>? onConflict = null
    )
        where T : class
        where TConfig : BulkInsertOptions
    {
        var dbSet = dbContext.Set<T>() ??
                    throw new InvalidOperationException($"DbSet of type {typeof(T).Name} not found in DbContext.");

        return ExecuteBulkInsertReturnEntitiesCoreAsync(dbSet, true, entities, configure, onConflict, CancellationToken.None)
            .GetAwaiter().GetResult();
    }

    /// <summary>
    /// Executes a bulk insert operation returning the inserted/updated entities, from the DbContext (synchronous variant), with common options.
    /// </summary>
    public static List<T> ExecuteBulkInsertReturnEntities<T>(
        this DbContext dbContext,
        IEnumerable<T> entities,
        Action<BulkInsertOptions> configure,
        OnConflictOptions<T>? onConflict = null
    )
        where T : class
    {
        return ExecuteBulkInsertReturnEntities<T, BulkInsertOptions>(dbContext, entities, configure, onConflict);
    }

    /// <summary>
    /// Executes a bulk insert operation returning the inserted/updated entities, from the DbContext (synchronous variant), without options.
    /// </summary>
    public static List<T> ExecuteBulkInsertReturnEntities<T>(
        this DbContext dbContext,
        IEnumerable<T> entities,
        OnConflictOptions<T>? onConflict = null
    )
        where T : class
    {
        return ExecuteBulkInsertReturnEntities<T, BulkInsertOptions>(dbContext, entities, _ => { }, onConflict);
    }

    /// <summary>
    /// Executes a bulk insert operation returning the inserted/updated entities, from the DbContext, with provider specific options.
    /// </summary>
    public static Task<List<T>> ExecuteBulkInsertReturnEntitiesAsync<T, TConfig>(
        this DbContext dbContext,
        IEnumerable<T> entities,
        Action<TConfig> configure,
        OnConflictOptions<T>? onConflict = null,
        CancellationToken cancellationToken = default
    )
        where T : class
        where TConfig : BulkInsertOptions
    {
        var dbSet = dbContext.Set<T>() ??
                    throw new InvalidOperationException($"DbSet of type {typeof(T).Name} not found in DbContext.");

        return ExecuteBulkInsertReturnEntitiesCoreAsync(dbSet,false, entities, configure, onConflict, cancellationToken);
    }

    /// <summary>
    /// Executes a bulk insert operation returning the inserted/updated entities, from the DbContext, with common options.
    /// </summary>
    public static Task<List<T>> ExecuteBulkInsertReturnEntitiesAsync<T>(
        this DbContext dbContext,
        IEnumerable<T> entities,
        Action<BulkInsertOptions> configure,
        OnConflictOptions<T>? onConflict = null,
        CancellationToken cancellationToken = default
    )
        where T : class
    {
        return ExecuteBulkInsertReturnEntitiesAsync<T, BulkInsertOptions>(dbContext, entities, configure, onConflict, cancellationToken);
    }

    /// <summary>
    /// Executes a bulk insert operation returning the inserted/updated entities, from the DbContext, without options.
    /// </summary>
    public static Task<List<T>> ExecuteBulkInsertReturnEntitiesAsync<T>(
        this DbContext dbContext,
        IEnumerable<T> entities,
        OnConflictOptions<T>? onConflict = null,
        CancellationToken cancellationToken = default
    )
        where T : class
    {
        return ExecuteBulkInsertReturnEntitiesAsync<T, BulkInsertOptions>(dbContext, entities, _ => { }, onConflict, cancellationToken);
    }

    /// <summary>
    /// Executes a bulk insert operation returning the inserted/updated entities, from the DbContext, with provider specific options.
    /// </summary>
    public static IAsyncEnumerable<T> ExecuteBulkInsertReturnEnumerableAsync<T, TConfig>(
        this DbContext dbContext,
        IEnumerable<T> entities,
        Action<TConfig> configure,
        OnConflictOptions<T>? onConflict = null,
        CancellationToken cancellationToken = default
    )
        where T : class
        where TConfig : BulkInsertOptions
    {
        var dbSet = dbContext.Set<T>() ??
                    throw new InvalidOperationException($"DbSet of type {typeof(T).Name} not found in DbContext.");

        return dbSet.ExecuteBulkInsertReturnEnumerableAsync(entities, configure, onConflict, cancellationToken);
    }

    /// <summary>
    /// Executes a bulk insert operation returning the inserted/updated entities, from the DbContext, with common options.
    /// </summary>
    public static IAsyncEnumerable<T> ExecuteBulkInsertReturnEnumerableAsync<T>(
        this DbContext dbContext,
        IEnumerable<T> entities,
        Action<BulkInsertOptions> configure,
        OnConflictOptions<T>? onConflict = null,
        CancellationToken cancellationToken = default
    ) where T : class
    {
        return ExecuteBulkInsertReturnEnumerableAsync<T, BulkInsertOptions>(dbContext, entities, configure, onConflict, cancellationToken);
    }

    /// <summary>
    /// Executes a bulk insert operation returning the inserted/updated entities, from the DbContext, without options.
    /// </summary>
    public static IAsyncEnumerable<T> ExecuteBulkInsertReturnEnumerableAsync<T>(
        this DbContext dbContext,
        IEnumerable<T> entities,
        OnConflictOptions<T>? onConflict = null,
        CancellationToken cancellationToken = default
    ) where T : class
    {
        return ExecuteBulkInsertReturnEnumerableAsync<T, BulkInsertOptions>(dbContext, entities, _ => { }, onConflict, cancellationToken);
    }

    /// <summary>
    /// Executes a bulk insert operation without returning the inserted/updated entities, from the DbContext, with provider specific options.
    /// </summary>
    public static async Task ExecuteBulkInsertAsync<T, TConfig>(
        this DbContext dbContext,
        IEnumerable<T> entities,
        Action<TConfig> configure,
        OnConflictOptions<T>? onConflict = null,
        CancellationToken cancellationToken = default
    )
        where T : class
        where TConfig : BulkInsertOptions
    {
        var dbSet = dbContext.Set<T>() ??
                    throw new InvalidOperationException($"DbSet of type {typeof(T).Name} not found in DbContext.");

        await dbSet.ExecuteBulkInsertAsync(entities, configure, onConflict, cancellationToken);
    }

    /// <summary>
    /// Executes a bulk insert operation without returning the inserted/updated entities, from the DbContext, with common options.
    /// </summary>
    public static async Task ExecuteBulkInsertAsync<T>(
        this DbContext dbContext,
        IEnumerable<T> entities,
        Action<BulkInsertOptions> configure,
        OnConflictOptions<T>? onConflict = null,
        CancellationToken cancellationToken = default
    )
        where T : class
    {
        await ExecuteBulkInsertAsync<T, BulkInsertOptions>(dbContext, entities, configure, onConflict, cancellationToken);
    }

    /// <summary>
    /// Executes a bulk insert operation without returning the inserted/updated entities, from the DbContext, without options.
    /// </summary>
    public static async Task ExecuteBulkInsertAsync<T>(
        this DbContext dbContext,
        IEnumerable<T> entities,
        OnConflictOptions<T>? onConflict = null,
        CancellationToken cancellationToken = default
    )
        where T : class
    {
        await ExecuteBulkInsertAsync<T, BulkInsertOptions>(dbContext, entities, _ => { }, onConflict, cancellationToken);
    }

    /// <summary>
    /// Executes a bulk insert operation without returning the inserted/updated entities, from the DbContext (synchronous variant), with provider specific options.
    /// </summary>
    public static void ExecuteBulkInsert<T, TConfig>(
        this DbContext dbContext,
        IEnumerable<T> entities,
        Action<TConfig> configure,
        OnConflictOptions<T>? onConflict = null
    )
        where T : class
        where TConfig : BulkInsertOptions
    {
        var dbSet = dbContext.Set<T>() ??
                    throw new InvalidOperationException($"DbSet of type {typeof(T).Name} not found in DbContext.");

        dbSet.ExecuteBulkInsert(entities, configure, onConflict);
    }

    /// <summary>
    /// Executes a bulk insert operation without returning the inserted/updated entities, from the DbContext (synchronous variant), with common options.
    /// </summary>
    public static void ExecuteBulkInsert<T>(
        this DbContext dbContext,
        IEnumerable<T> entities,
        Action<BulkInsertOptions> configure,
        OnConflictOptions<T>? onConflict = null
    ) where T : class
    {
        ExecuteBulkInsert<T, BulkInsertOptions>(dbContext, entities, configure, onConflict);
    }

    /// <summary>
    /// Executes a bulk insert operation without returning the inserted/updated entities, from the DbContext (synchronous variant), without options.
    /// </summary>
    public static void ExecuteBulkInsert<T>(
        this DbContext dbContext,
        IEnumerable<T> entities,
        OnConflictOptions<T>? onConflict = null
    ) where T : class
    {
        ExecuteBulkInsert<T, BulkInsertOptions>(dbContext, entities, _ => { }, onConflict);
    }
}
