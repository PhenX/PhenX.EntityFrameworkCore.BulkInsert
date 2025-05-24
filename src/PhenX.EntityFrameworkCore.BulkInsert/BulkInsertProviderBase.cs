using System.Runtime.CompilerServices;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;

using PhenX.EntityFrameworkCore.BulkInsert.Abstractions;
using PhenX.EntityFrameworkCore.BulkInsert.Dialect;
using PhenX.EntityFrameworkCore.BulkInsert.Extensions;
using PhenX.EntityFrameworkCore.BulkInsert.Metadata;
using PhenX.EntityFrameworkCore.BulkInsert.Options;

namespace PhenX.EntityFrameworkCore.BulkInsert;

internal abstract class BulkInsertProviderBase<TDialect, TOptions>(ILogger<BulkInsertProviderBase<TDialect, TOptions>> logger) : IBulkInsertProvider
    where TDialect : SqlDialectBuilder, new()
    where TOptions : BulkInsertOptions, new()
{
    protected readonly TDialect SqlDialect = new();

    protected virtual string BulkInsertId => "_bulk_insert_id";

    protected abstract string AddTableCopyBulkInsertId { get; }

    protected virtual string GetTempTableName(string tableName) => $"_temp_bulk_insert_{tableName}";

    SqlDialectBuilder IBulkInsertProvider.SqlDialect => SqlDialect;

    public BulkInsertOptions InternalCreateDefaultOptions() => CreateDefaultOptions();

    /// <summary>
    /// Create the default options for the provider, can be a subclass of <see cref="BulkInsertOptions"/>.
    /// </summary>
    protected abstract TOptions CreateDefaultOptions();

    public virtual async IAsyncEnumerable<T> BulkInsertReturnEntities<T>(
        bool sync,
        DbContext context,
        TableMetadata tableInfo,
        IEnumerable<T> entities,
        BulkInsertOptions options,
        OnConflictOptions? onConflict,
        [EnumeratorCancellation] CancellationToken ctk) where T : class
    {
        if (options is not TOptions providerOptions)
        {
            throw new InvalidOperationException($"Invalid options type: {options.GetType().Name}. Expected: {typeof(TOptions).Name}");
        }

        using var activity = Telemetry.ActivitySource.StartActivity("BulkInsertReturnEntities");
        activity?.AddTag("tableName", tableInfo.TableName);
        activity?.AddTag("synchronous", sync);

        var connection = await context.GetConnection(sync, ctk);
        try
        {
            if (logger != null)
            {
                Log.UsingTempTableToReturnData(logger);
            }

            var tableName = await PerformBulkInsertAsync(sync, context, tableInfo, entities, providerOptions, tempTableRequired: true, ctk: ctk);

            var result =
                await CopyFromTempTableAsync<T, T>(sync, context, tableInfo, tableName, true, providerOptions, onConflict, ctk: ctk)
                    ?? throw new InvalidOperationException("Copy returns null enumerable.");

            await foreach (var item in result.WithCancellation(ctk))
            {
                yield return item;
            }

            // Commit the transaction if we own them.
            await connection.Commit(sync, ctk);
        }
        finally
        {
            await connection.Close(sync, ctk);
        }
    }

    public virtual async Task BulkInsert<T>(
        bool sync,
        DbContext context,
        TableMetadata tableInfo,
        IEnumerable<T> entities,
        BulkInsertOptions options,
        OnConflictOptions? onConflict,
        CancellationToken ctk) where T : class
    {
        if (options is not TOptions providerOptions)
        {
            throw new InvalidOperationException($"Invalid options type: {options.GetType().Name}. Expected: {typeof(TOptions).Name}");
        }

        using var activity = Telemetry.ActivitySource.StartActivity("BulkInsert");
        activity?.AddTag("tableName", tableInfo.TableName);
        activity?.AddTag("synchronous", sync);

        var connection = await context.GetConnection(sync, ctk);
        try
        {
            if (onConflict != null)
            {
                if (logger != null)
                {
                    Log.UsingTempTableToResolveConflicts(logger);
                }

                var tableName = await PerformBulkInsertAsync(sync, context, tableInfo, entities, providerOptions, tempTableRequired: true, ctk: ctk);

                await CopyFromTempTableAsync<T, T>(sync, context, tableInfo, tableName, false, providerOptions, onConflict, ctk);
            }
            else
            {
                if (logger != null)
                {
                    Log.UsingDirectInsert(logger);
                }

                await PerformBulkInsertAsync(sync, context, tableInfo, entities, providerOptions, tempTableRequired: false, ctk: ctk);
            }

            // Commit the transaction if we own them.
            await connection.Commit(sync, ctk);
        }
        finally
        {
            await connection.Close(sync, ctk);
        }
    }

    private async Task<string> PerformBulkInsertAsync<T>(
        bool sync,
        DbContext context,
        TableMetadata tableInfo,
        IEnumerable<T> entities,
        TOptions options,
        bool tempTableRequired,
        CancellationToken ctk) where T : class
    {
        if (entities.TryGetNonEnumeratedCount(out var count) && count == 0)
        {
            throw new InvalidOperationException("No entities to insert.");
        }

        var tableName = tempTableRequired
            ? await CreateTableCopyAsync<T>(sync, context, options, tableInfo, ctk)
            : tableInfo.QuotedTableName;

        var columns = tableInfo.GetColumns(options.CopyGeneratedColumns);

        using var activity = Telemetry.ActivitySource.StartActivity("Insert");
        activity?.AddTag("tempTable", tempTableRequired);
        activity?.AddTag("synchronous", sync);

        await BulkInsert(false, context, tableInfo, entities, tableName, columns, options, ctk);
        return tableName;
    }

    /// <summary>
    /// The main bulk insert method: will insert either in a temp table or directly in the target table.
    /// </summary>
    protected abstract Task BulkInsert<T>(
        bool sync,
        DbContext context,
        TableMetadata tableInfo,
        IEnumerable<T> entities,
        string tableName,
        IReadOnlyList<ColumnMetadata> columns,
        TOptions options,
        CancellationToken ctk) where T : class;

    protected async Task<string> CreateTableCopyAsync<T>(
        bool sync,
        DbContext context,
        BulkInsertOptions options,
        TableMetadata tableInfo,
        CancellationToken ctk) where T : class
    {
        var tempTableName = SqlDialect.QuoteTableName(null, GetTempTableName(tableInfo.TableName));
        var tempColumns = tableInfo.GetColumns(options.CopyGeneratedColumns);

        var query = SqlDialect.CreateTableCopySql(tempTableName, tableInfo, tempColumns);

        await ExecuteAsync(sync, context, query, ctk);
        await AddBulkInsertIdColumn<T>(sync, context, tempTableName, ctk);

        return tempTableName;
    }

    protected virtual async Task AddBulkInsertIdColumn<T>(
        bool sync,
        DbContext context,
        string tempTableName,
        CancellationToken ctk) where T : class
    {
        var alterQuery = string.Format(AddTableCopyBulkInsertId, tempTableName);

        await ExecuteAsync(sync, context, alterQuery, ctk);
    }

    private async Task<IAsyncEnumerable<TResult>?> CopyFromTempTableAsync<T, TResult>(
        bool sync,
        DbContext context,
        TableMetadata tableInfo,
        string tempTableName,
        bool returnData,
        TOptions options,
        OnConflictOptions? onConflict,
        CancellationToken ctk) where T : class where TResult : class
    {
        var query =
            SqlDialect.BuildMoveDataSql<T>(
                tableInfo,
                tempTableName,
                tableInfo.GetColumns(options.CopyGeneratedColumns),
                returnData ? tableInfo.GetColumns() : [],
                options,
                onConflict);

        if (returnData)
        {
            // Use EF to execute the query and return the results
            return context.Set<TResult>().FromSqlRaw(query).AsAsyncEnumerable();
        }

        // If not returning data, just execute the command
        await ExecuteAsync(sync, context, query, ctk);
        return null;
    }

    protected static async Task ExecuteAsync(
        bool sync,
        DbContext context,
        string query,
        CancellationToken ctk)
    {
        var command = context.Database.GetDbConnection().CreateCommand();
        command.Transaction = context.Database.CurrentTransaction!.GetDbTransaction();
        command.CommandText = query;

        if (sync)
        {
            // ReSharper disable once MethodHasAsyncOverloadWithCancellation
            command.ExecuteNonQuery();
        }
        else
        {
            await command.ExecuteNonQueryAsync(ctk);
        }
    }
}
