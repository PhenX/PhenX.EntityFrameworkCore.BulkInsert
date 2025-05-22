using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;

using PhenX.EntityFrameworkCore.BulkInsert.Abstractions;
using PhenX.EntityFrameworkCore.BulkInsert.Dialect;
using PhenX.EntityFrameworkCore.BulkInsert.Extensions;
using PhenX.EntityFrameworkCore.BulkInsert.Metadata;
using PhenX.EntityFrameworkCore.BulkInsert.Options;

namespace PhenX.EntityFrameworkCore.BulkInsert;

internal abstract class BulkInsertProviderBase<TDialect>(ILogger<BulkInsertProviderBase<TDialect>>? logger = null) : IBulkInsertProvider where TDialect : SqlDialectBuilder, new()
{
    protected readonly TDialect SqlDialect = new();

    protected virtual string BulkInsertId => "_bulk_insert_id";

    protected abstract string AddTableCopyBulkInsertId { get; }

    protected virtual string GetTempTableName(string tableName) => $"_temp_bulk_insert_{tableName}";

    SqlDialectBuilder IBulkInsertProvider.SqlDialect => SqlDialect;

    public virtual async Task<List<T>> BulkInsertReturnEntities<T>(
        bool sync,
        DbContext context,
        TableMetadata tableInfo,
        IEnumerable<T> entities,
        BulkInsertOptions options,
        OnConflictOptions? onConflict,
        CancellationToken ctk) where T : class
    {
        using var activity = Telemetry.ActivitySource.StartActivity("BulkInsertReturnEntities");
        activity?.AddTag("tableName", tableInfo.TableName);
        activity?.AddTag("synchronous", true);

        var connection = await context.GetConnection(sync, ctk);
        try
        {
            if (logger != null)
            {
                Log.UsingTempTablToReturnData(logger);
            }

            var tableName = await PerformBulkInsertAsync(sync, context, tableInfo, entities, options, tempTableRequired: true, ctk: ctk);

            var result = await CopyFromTempTableAsync<T, T>(sync, context, tableInfo, tableName, true, options, onConflict, ctk: ctk);

            // Commit the transaction if we own them.
            await connection.Commit(sync, ctk);
            return result;
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
        using var activity = Telemetry.ActivitySource.StartActivity("BulkInsert");
        activity?.AddTag("tableName", tableInfo.TableName);
        activity?.AddTag("synchronous", true);

        var connection = await context.GetConnection(sync, ctk);
        try
        {
            if (onConflict != null)
            {
                if (logger != null)
                {
                    Log.UsingTempTableToResolveConflicts(logger);
                }

                var tableName = await PerformBulkInsertAsync(sync, context, tableInfo, entities, options, tempTableRequired: true, ctk: ctk);

                await CopyFromTempTableAsync<T, T>(sync, context, tableInfo, tableName, false, options, onConflict, ctk);
            }
            else
            {
                if (logger != null)
                {
                    Log.UsingDirectInsert(logger);
                }

                await PerformBulkInsertAsync(sync, context, tableInfo, entities, options, tempTableRequired: false, ctk: ctk);
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
        BulkInsertOptions options,
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

        var properties = tableInfo.GetProperties(options.CopyGeneratedColumns);

        using var activity = Telemetry.ActivitySource.StartActivity("Insert");
        activity?.AddTag("tempTable", tempTableRequired);
        activity?.AddTag("synchronous", true);

        await BulkInsert(false, context, tableInfo, entities, tableName, properties, options, ctk);
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
        IReadOnlyList<PropertyMetadata> properties,
        BulkInsertOptions options,
        CancellationToken ctk) where T : class;

    protected async Task<string> CreateTableCopyAsync<T>(
        bool sync,
        DbContext context,
        BulkInsertOptions options,
        TableMetadata tableInfo,
        CancellationToken ctk) where T : class
    {
        var tempTableName = SqlDialect.QuoteTableName(null, GetTempTableName(tableInfo.TableName));
        var tempColumns = tableInfo.GetProperties(options.CopyGeneratedColumns);

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

    private async Task<List<TResult>> CopyFromTempTableAsync<T, TResult>(
        bool sync,
        DbContext context,
        TableMetadata tableInfo,
        string tempTableName,
        bool returnData,
        BulkInsertOptions options,
        OnConflictOptions? onConflict,
        CancellationToken ctk) where T : class where TResult : class
    {
        var query =
            SqlDialect.BuildMoveDataSql<T>(
                tableInfo,
                tempTableName,
                tableInfo.GetProperties(options.CopyGeneratedColumns),
                returnData ? tableInfo.GetProperties() : [],
                options,
                onConflict);

        if (returnData)
        {
            // Use EF to execute the query and return the results
            var queryable = context.Set<TResult>().FromSqlRaw(query);

            if (sync)
            {
                return [.. queryable];
            }

            return await queryable.ToListAsync(ctk);
        }

        // If not returning data, just execute the command
        await ExecuteAsync(sync, context, query, ctk);
        return [];
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
