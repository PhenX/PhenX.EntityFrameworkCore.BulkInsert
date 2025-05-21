using System.Data.Common;

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

    protected abstract string CreateTableCopySql { get; }
    protected abstract string AddTableCopyBulkInsertId { get; }

    SqlDialectBuilder IBulkInsertProvider.SqlDialect => SqlDialect;

    protected async Task<string> CreateTableCopyAsync<T>(
        bool sync,
        DbContext context,
        TableMetadata tableInfo,
        CancellationToken cancellationToken = default) where T : class
    {
        var tempTableName = await CreateTemporaryTableAsync(sync, context, tableInfo, cancellationToken);

        await AddBulkInsertIdColumn<T>(sync, context, tempTableName, cancellationToken);

        return tempTableName;
    }

    private async Task<string> CreateTemporaryTableAsync(
        bool sync,
        DbContext context,
        TableMetadata tableInfo,
        CancellationToken cancellationToken)
    {
        var tempTableName = SqlDialect.QuoteTableName(null, GetTempTableName(tableInfo.TableName));
        var tempColumns = string.Join(", ", tableInfo.GetProperties(false).Select(x => x.QuotedColumName));

        var query = string.Format(CreateTableCopySql, tempTableName, tableInfo.QuotedTableName, tempColumns);

        await ExecuteAsync(sync, context, query, cancellationToken);
        return tempTableName;
    }

    protected virtual async Task AddBulkInsertIdColumn<T>(bool sync, DbContext context,
        string tempTableName, CancellationToken cancellationToken) where T : class
    {
        var alterQuery = string.Format(AddTableCopyBulkInsertId, tempTableName);

        await ExecuteAsync(sync, context, alterQuery, cancellationToken);
    }

    protected virtual string GetTempTableName(string tableName) => $"_temp_bulk_insert_{tableName}";

    protected string Quote(string name) => SqlDialect.Quote(name);

    protected static async Task ExecuteAsync(bool sync, DbContext context, string query, CancellationToken cancellationToken = default)
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
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    public async Task<List<T>> CopyFromTempTableAsync<T>(
        bool sync,
        DbContext context,
        TableMetadata tableInfo,
        string tempTableName,
        bool returnData,
        BulkInsertOptions options,
        OnConflictOptions? onConflict = null,
        CancellationToken cancellationToken = default) where T : class
    {
        return await CopyFromTempTableWithoutKeysAsync<T, T>(
            sync,
            context,
            tableInfo,
            tempTableName,
            returnData,
            options,
            onConflict,
            cancellationToken: cancellationToken);
    }

    private async Task<List<TResult>> CopyFromTempTableWithoutKeysAsync<T, TResult>(
        bool sync,
        DbContext context,
        TableMetadata tableInfo,
        string tempTableName,
        bool returnData,
        BulkInsertOptions options,
        OnConflictOptions? onConflict = null,
        CancellationToken cancellationToken = default)
        where T : class
        where TResult : class
    {
        var movedProperties = tableInfo.GetProperties(options.CopyGeneratedColumns);
        var returnedProperties = returnData ? tableInfo.GetProperties() : [];

        var query = SqlDialect.BuildMoveDataSql<T>(tableInfo, tempTableName, movedProperties, returnedProperties, options, onConflict);

        if (returnData)
        {
            return await QueryAsync(sync, context, query, cancellationToken);
        }

        // If not returning data, just execute the command
        await ExecuteAsync(sync, context, query, cancellationToken);
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
        OnConflictOptions? onConflict = null,
        CancellationToken ctk = default
    ) where T : class
    {
        var connectionInfo = await context.GetConnection(sync, ctk);

        var (tableName, _) = await PerformBulkInsertAsync(sync, context, tableInfo, entities, options, tempTableRequired: true, ctk: ctk);

        var result = await CopyFromTempTableAsync<T>(sync, context, tableInfo, tableName, true, options, onConflict, cancellationToken: ctk);

        await Finish(sync, connectionInfo, ctk);

        return result;
    }

    private static async Task Finish(bool sync, ConnectionInfo connectionInfo, CancellationToken ctk)
    {
        var (connection, wasClosed, transaction, wasBegan) = connectionInfo;

        if (!wasBegan)
        {
            if (sync)
            {
                // ReSharper disable once MethodHasAsyncOverloadWithCancellation
                transaction.Commit();
                transaction.Dispose();
            }
            else
            {
                await transaction.CommitAsync(ctk);
                await transaction.DisposeAsync();
            }
        }

        if (wasClosed)
        {
            if (sync)
            {
                // ReSharper disable once MethodHasAsyncOverload
                connection.Close();
            }
            else
            {
                await connection.CloseAsync();
            }
        }
    }

    public virtual async Task BulkInsert<T>(
        bool sync,
        DbContext context,
        TableMetadata tableInfo,
        IEnumerable<T> entities,
        BulkInsertOptions options,
        OnConflictOptions? onConflict = null,
        CancellationToken ctk = default
    ) where T : class
    {
        if (onConflict != null)
        {
            var connectionInfo = await context.GetConnection(sync, ctk);

            var (tableName, _) = await PerformBulkInsertAsync(sync, context, tableInfo, entities, options, tempTableRequired: true, ctk: ctk);

            await CopyFromTempTableAsync<T>(sync, context, tableInfo, tableName, false, options, onConflict, ctk);

            await Finish(sync, connectionInfo, ctk);
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
        CancellationToken ctk = default) where T : class
    {
        if (entities.TryGetNonEnumeratedCount(out var count) && count == 0)
        {
            throw new InvalidOperationException("No entities to insert.");
        }

        var connectionInfo = await context.GetConnection(sync, ctk);

        var tableName = tempTableRequired
            ? await CreateTableCopyAsync<T>(sync, context, tableInfo, ctk)
            : tableInfo.QuotedTableName;

        var properties = tableInfo.GetProperties(options.CopyGeneratedColumns);

        await BulkInsert(false, context, tableInfo, entities, tableName, properties, options, ctk);

        await Finish(sync, connectionInfo, ctk);

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
        CancellationToken ctk
    ) where T : class;
}
