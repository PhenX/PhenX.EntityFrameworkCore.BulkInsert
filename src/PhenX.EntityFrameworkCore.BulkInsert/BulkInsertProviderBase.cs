using System.Data.Common;
using System.Reflection;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;

using PhenX.EntityFrameworkCore.BulkInsert.Abstractions;
using PhenX.EntityFrameworkCore.BulkInsert.Dialect;
using PhenX.EntityFrameworkCore.BulkInsert.Extensions;
using PhenX.EntityFrameworkCore.BulkInsert.Metadata;
using PhenX.EntityFrameworkCore.BulkInsert.Options;

namespace PhenX.EntityFrameworkCore.BulkInsert;

#pragma warning disable CS9113 // Parameter is unread.
internal abstract class BulkInsertProviderBase<TDialect>(ILogger<BulkInsertProviderBase<TDialect>>? logger = null) : IBulkInsertProvider
#pragma warning restore CS9113 // Parameter is unread.
    where TDialect : SqlDialectBuilder, new()
{
    protected readonly TDialect SqlDialect = new();

    protected virtual string BulkInsertId => "_bulk_insert_id";

    protected abstract string AddTableCopyBulkInsertId { get; }

    protected virtual string GetTempTableName(string tableName) => $"_temp_bulk_insert_{tableName}";

    SqlDialectBuilder IBulkInsertProvider.SqlDialect => SqlDialect;

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

    public async Task<List<T>> CopyFromTempTableAsync<T>(
        bool sync,
        DbContext context,
        TableMetadata tableInfo,
        string tempTableName,
        bool returnData,
        BulkInsertOptions options,
        OnConflictOptions? onConflict,
        CancellationToken ctk) where T : class
    {
        return await CopyFromTempTableWithoutKeysAsync<T, T>(
            sync,
            context,
            tableInfo,
            tempTableName,
            returnData,
            options,
            onConflict,
            ctk);
    }

    private async Task<List<TResult>> CopyFromTempTableWithoutKeysAsync<T, TResult>(
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
            return await QueryAsync(sync, context, query, ctk);
        }

        // If not returning data, just execute the command
        await ExecuteAsync(sync, context, query, ctk);
        return [];

        static async Task<List<TResult>> QueryAsync(bool sync, DbContext context, string query, CancellationToken cancellationToken)
        {
            // Use EF to execute the query and return the results
            IQueryable<TResult> queryable = context
                .Set<TResult>()
                .FromSqlRaw(query);

            if (sync)
            {
                return queryable.ToList();
            }

            return await queryable.ToListAsync(cancellationToken: cancellationToken);
        }
    }

    public virtual async Task<List<T>> BulkInsertReturnEntities<T>(
        bool sync,
        DbContext context,
        TableMetadata tableInfo,
        IEnumerable<T> entities,
        BulkInsertOptions options,
        OnConflictOptions? onConflict,
        CancellationToken ctk) where T : class
    {
        List<T> result;

        var connectionInfo = await context.GetConnection(sync, ctk);
        try
        {
            var (tableName, _) = await PerformBulkInsertAsync(sync, context, tableInfo, entities, options, tempTableRequired: true, ctk: ctk);

            result = await CopyFromTempTableAsync<T>(sync, context, tableInfo, tableName, true, options, onConflict, ctk: ctk);

            // Commit the transaction if we own them.
            await connectionInfo.Commit(sync, ctk);
        }
        finally
        {
            await connectionInfo.Close(sync, ctk);
        }

        return result;
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
        if (onConflict != null)
        {
            var connectionInfo = await context.GetConnection(sync, ctk);
            try
            {
                var (tableName, _) = await PerformBulkInsertAsync(sync, context, tableInfo, entities, options, tempTableRequired: true, ctk: ctk);

                await CopyFromTempTableAsync<T>(sync, context, tableInfo, tableName, false, options, onConflict, ctk);

                // Commit the transaction if we own them.
                await connectionInfo.Commit(sync, ctk);
            }
            finally
            {
                await connectionInfo.Close(sync, ctk);
            }
        }
        else
        {
            await PerformBulkInsertAsync(sync, context, tableInfo, entities, options, tempTableRequired: false, ctk: ctk);
        }
    }

    private async Task<(string TableName, DbConnection Connection)> PerformBulkInsertAsync<T>(
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

        var connectionInfo = await context.GetConnection(sync, ctk);

        var tableName = tempTableRequired
            ? await CreateTableCopyAsync<T>(sync, context, options, tableInfo, ctk)
            : tableInfo.QuotedTableName;

        var properties = tableInfo.GetProperties(options.CopyGeneratedColumns);

        try
        {
            await BulkInsert(false, context, tableInfo, entities, tableName, properties, options, ctk);

            // Commit the transaction if we own them.
            await connectionInfo.Commit(sync, ctk);
        }
        finally
        {
            await connectionInfo.Close(sync, ctk);
        }

        return (tableName, connectionInfo.Connection);
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
}
