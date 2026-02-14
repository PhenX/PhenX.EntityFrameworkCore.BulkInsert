using Microsoft.EntityFrameworkCore;

using PhenX.EntityFrameworkCore.BulkInsert.Graph;
using PhenX.EntityFrameworkCore.BulkInsert.Options;

namespace PhenX.EntityFrameworkCore.BulkInsert.Extensions;

public static partial class PublicExtensions
{
    /// <summary>
    /// Executes a bulk insert operation returning the inserted/updated entities, from the DbSet (synchronous variant), with provider specific options.
    /// </summary>
    public static List<T> ExecuteBulkInsertReturnEntities<T, TOptions>(
        this DbSet<T> dbSet,
        IEnumerable<T> entities,
        Action<TOptions> configure,
        OnConflictOptions<T>? onConflict = null
    )
        where T : class
        where TOptions : BulkInsertOptions
    {
        return ExecuteBulkInsertReturnEntitiesCoreAsync(dbSet, true, entities, configure, onConflict, CancellationToken.None)
            .GetAwaiter().GetResult();
    }

    /// <summary>
    /// Executes a bulk insert operation returning the inserted/updated entities, from the DbSet (synchronous variant), with common options.
    /// </summary>
    public static List<T> ExecuteBulkInsertReturnEntities<T>(
        this DbSet<T> dbSet,
        IEnumerable<T> entities,
        Action<BulkInsertOptions> configure,
        OnConflictOptions<T>? onConflict = null
    )
        where T : class
    {
        return ExecuteBulkInsertReturnEntities<T, BulkInsertOptions>(dbSet, entities, configure, onConflict);
    }

    /// <summary>
    /// Executes a bulk insert operation returning the inserted/updated entities, from the DbSet (synchronous variant), without options.
    /// </summary>
    public static List<T> ExecuteBulkInsertReturnEntities<T>(
        this DbSet<T> dbSet,
        IEnumerable<T> entities,
        OnConflictOptions<T>? onConflict = null
    )
        where T : class
    {
        return ExecuteBulkInsertReturnEntities<T, BulkInsertOptions>(dbSet, entities, _ => { }, onConflict);
    }

    /// <summary>
    /// Executes a bulk insert operation returning the inserted/updated entities, from the DbSet, with provider specific options.
    /// </summary>
    public static Task<List<T>> ExecuteBulkInsertReturnEntitiesAsync<T, TOptions>(
        this DbSet<T> dbSet,
        IEnumerable<T> entities,
        Action<TOptions> configure,
        OnConflictOptions<T>? onConflict = null,
        CancellationToken cancellationToken = default
    )
        where T : class
        where TOptions : BulkInsertOptions
    {
        return ExecuteBulkInsertReturnEntitiesCoreAsync(dbSet, false, entities, configure, onConflict, cancellationToken);
    }

    /// <summary>
    /// Executes a bulk insert operation returning the inserted/updated entities, from the DbSet, with common options.
    /// </summary>
    public static Task<List<T>> ExecuteBulkInsertReturnEntitiesAsync<T>(
        this DbSet<T> dbSet,
        IEnumerable<T> entities,
        Action<BulkInsertOptions> configure,
        OnConflictOptions<T>? onConflict = null,
        CancellationToken cancellationToken = default
    )
        where T : class
    {
        return ExecuteBulkInsertReturnEntitiesAsync<T, BulkInsertOptions>(dbSet, entities, configure, onConflict, cancellationToken);
    }

    /// <summary>
    /// Executes a bulk insert operation returning the inserted/updated entities, from the DbSet, without options.
    /// </summary>
    public static Task<List<T>> ExecuteBulkInsertReturnEntitiesAsync<T>(
        this DbSet<T> dbSet,
        IEnumerable<T> entities,
        OnConflictOptions<T>? onConflict = null,
        CancellationToken cancellationToken = default
    )
        where T : class
    {
        return ExecuteBulkInsertReturnEntitiesAsync<T, BulkInsertOptions>(dbSet, entities, _ => { }, onConflict, cancellationToken);
    }

    /// <summary>
    /// Executes a bulk insert operation returning the inserted/updated entities, from the DbSet, with provider specific options.
    /// </summary>
    public static IAsyncEnumerable<T> ExecuteBulkInsertReturnEnumerableAsync<T, TOptions>(
        this DbSet<T> dbSet,
        IEnumerable<T> entities,
        Action<TOptions> configure,
        OnConflictOptions<T>? onConflict = null,
        CancellationToken cancellationToken = default
    )
        where T : class
        where TOptions : BulkInsertOptions
    {
        var (provider, context, options) = InitProvider(dbSet, configure);

        return provider.BulkInsertReturnEntities(false, context, dbSet.GetDbContext().GetTableInfo<T>(), entities,
            options, onConflict, cancellationToken);
    }

    /// <summary>
    /// Executes a bulk insert operation returning the inserted/updated entities, from the DbSet, with common options.
    /// </summary>
    public static IAsyncEnumerable<T> ExecuteBulkInsertReturnEnumerableAsync<T>(
        this DbSet<T> dbSet,
        IEnumerable<T> entities,
        Action<BulkInsertOptions> configure,
        OnConflictOptions<T>? onConflict = null,
        CancellationToken cancellationToken = default
    )
        where T : class
    {
        return ExecuteBulkInsertReturnEnumerableAsync<T, BulkInsertOptions>(dbSet, entities, configure, onConflict, cancellationToken);
    }

    /// <summary>
    /// Executes a bulk insert operation returning the inserted/updated entities, from the DbSet, without options.
    /// </summary>
    public static IAsyncEnumerable<T> ExecuteBulkInsertReturnEnumerableAsync<T>(
        this DbSet<T> dbSet,
        IEnumerable<T> entities,
        OnConflictOptions<T>? onConflict = null,
        CancellationToken cancellationToken = default
    )
        where T : class
    {
        return ExecuteBulkInsertReturnEnumerableAsync<T, BulkInsertOptions>(dbSet, entities, _ => { }, onConflict, cancellationToken);
    }

    /// <summary>
    /// Executes a bulk insert operation without returning the inserted/updated entities, from the DbSet, with provider specific options.
    /// </summary>
    public static async Task ExecuteBulkInsertAsync<T, TOptions>(
        this DbSet<T> dbSet,
        IEnumerable<T> entities,
        Action<TOptions> configure,
        OnConflictOptions<T>? onConflict = null,
        CancellationToken cancellationToken = default
    )
        where T : class
        where TOptions : BulkInsertOptions
    {
        var (provider, context, options) = InitProvider(dbSet, configure);

        if (options.IncludeGraph)
        {
            if (onConflict != null)
            {
                throw new InvalidOperationException(
                    "OnConflict options cannot be used together with IncludeGraph. " +
                    "Either disable IncludeGraph or remove the onConflict parameter.");
            }

            var orchestrator = new GraphBulkInsertOrchestrator(context);
            await orchestrator.InsertGraph(false, entities, options, provider, cancellationToken);

            return;
        }

        await provider.BulkInsert(false, context, dbSet.GetDbContext().GetTableInfo<T>(), entities, options, onConflict,
            cancellationToken);
    }

    /// <summary>
    /// Executes a bulk insert operation without returning the inserted/updated entities, from the DbSet, with common options.
    /// </summary>
    public static async Task ExecuteBulkInsertAsync<T>(
        this DbSet<T> dbSet,
        IEnumerable<T> entities,
        Action<BulkInsertOptions> configure,
        OnConflictOptions<T>? onConflict = null,
        CancellationToken cancellationToken = default
    )
        where T : class
    {
        await ExecuteBulkInsertAsync<T, BulkInsertOptions>(dbSet, entities, configure, onConflict, cancellationToken);
    }

    /// <summary>
    /// Executes a bulk insert operation without returning the inserted/updated entities, from the DbSet, without options.
    /// </summary>
    public static async Task ExecuteBulkInsertAsync<T>(
        this DbSet<T> dbSet,
        IEnumerable<T> entities,
        OnConflictOptions<T>? onConflict = null,
        CancellationToken cancellationToken = default
    )
        where T : class
    {
        await ExecuteBulkInsertAsync<T, BulkInsertOptions>(dbSet, entities, _ => { }, onConflict, cancellationToken);
    }

    /// <summary>
    /// Executes a bulk insert operation without returning the inserted/updated entities, from the DbSet (synchronous variant), with provider specific options.
    /// </summary>
    public static void ExecuteBulkInsert<T, TOptions>(
        this DbSet<T> dbSet,
        IEnumerable<T> entities,
        Action<TOptions> configure,
        OnConflictOptions<T>? onConflict = null
    )
        where T : class
        where TOptions : BulkInsertOptions
    {
        var (provider, context, options) = InitProvider(dbSet, configure);

        if (options.IncludeGraph)
        {
            if (onConflict != null)
            {
                throw new InvalidOperationException(
                    "OnConflict options cannot be used together with IncludeGraph. " +
                    "Either disable IncludeGraph or remove the onConflict parameter.");
            }

            var orchestrator = new GraphBulkInsertOrchestrator(context);
            orchestrator.InsertGraph(true, entities, options, provider, CancellationToken.None)
                .GetAwaiter().GetResult();

            return;
        }

        provider.BulkInsert(true, context, dbSet.GetDbContext().GetTableInfo<T>(), entities, options, onConflict)
            .GetAwaiter().GetResult();
    }

    /// <summary>
    /// Executes a bulk insert operation without returning the inserted/updated entities, from the DbSet (synchronous variant), with common options.
    /// </summary>
    public static void ExecuteBulkInsert<T>(
        this DbSet<T> dbSet,
        IEnumerable<T> entities,
        Action<BulkInsertOptions> configure,
        OnConflictOptions<T>? onConflict = null
    )
        where T : class
    {
        ExecuteBulkInsert<T, BulkInsertOptions>(dbSet, entities, configure, onConflict);
    }

    /// <summary>
    /// Executes a bulk insert operation without returning the inserted/updated entities, from the DbSet (synchronous variant), without options.
    /// </summary>
    public static void ExecuteBulkInsert<T>(
        this DbSet<T> dbSet,
        IEnumerable<T> entities,
        OnConflictOptions<T>? onConflict = null
    )
        where T : class
    {
        ExecuteBulkInsert<T, BulkInsertOptions>(dbSet, entities, _ => { }, onConflict);
    }
}
