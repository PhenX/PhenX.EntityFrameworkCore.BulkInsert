using System.Data.Common;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;

using PhenX.EntityFrameworkCore.BulkInsert.Abstractions;
using PhenX.EntityFrameworkCore.BulkInsert.Dialect;
using PhenX.EntityFrameworkCore.BulkInsert.Extensions;
using PhenX.EntityFrameworkCore.BulkInsert.Options;

namespace PhenX.EntityFrameworkCore.BulkInsert;

internal abstract class BulkInsertProviderBase<TDialect> : IBulkInsertProvider
    where TDialect : SqlDialectBuilder, new()
{
    protected readonly TDialect SqlDialect = new();
    private readonly ILogger<BulkInsertProviderBase<TDialect>>? Logger;

    protected virtual string BulkInsertId => "_bulk_insert_id";

    protected abstract string CreateTableCopySql { get; }
    protected abstract string AddTableCopyBulkInsertId { get; }

    protected BulkInsertProviderBase(ILogger<BulkInsertProviderBase<TDialect>>? logger = null)
    {
        Logger = logger;
    }

    protected async Task<string> CreateTableCopyAsync<T>(
        bool sync,
        DbContext context,
        CancellationToken cancellationToken = default) where T : class
    {
        var tableInfo = GetTableInfo(context, typeof(T));
        var tableName = QuoteTableName(tableInfo.SchemaName, tableInfo.TableName);
        var tempTableName = QuoteTableName(null, GetTempTableName(tableInfo.TableName));

        var keptColumns = string.Join(", ", GetQuotedColumns(context, typeof(T), false));
        var query = string.Format(CreateTableCopySql, tempTableName, tableName, keptColumns);

        await ExecuteAsync(sync, context, query, cancellationToken);

        await AddBulkInsertIdColumn<T>(sync, context, tempTableName, cancellationToken);

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
        string tempTableName,
        bool returnData,
        BulkInsertOptions options,
        OnConflictOptions? onConflict = null,
        CancellationToken cancellationToken = default) where T : class
    {
        return await CopyFromTempTableWithoutKeysAsync<T, T>(
            sync,
            context,
            tempTableName,
            returnData,
            options,
            onConflict,
            cancellationToken: cancellationToken);
    }

    private async Task<List<TResult>> CopyFromTempTableWithoutKeysAsync<T, TResult>(
        bool sync,
        DbContext context,
        string tempTableName,
        bool returnData,
        BulkInsertOptions options,
        OnConflictOptions? onConflict = null,
        CancellationToken cancellationToken = default)
        where T : class
        where TResult : class
    {
        var (schemaName, tableName, _) = GetTableInfo(context, typeof(T));
        var quotedTableName = QuoteTableName(schemaName, tableName);

        var movedProperties = context.GetProperties(typeof(T), false);
        var returnedProperties = returnData ? context.GetProperties(typeof(T)) : [];

        var query = SqlDialect.BuildMoveDataSql<T>(context, tempTableName, quotedTableName, movedProperties, returnedProperties, options, onConflict);

        if (returnData)
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

        // If not returning data, just execute the command
        await ExecuteAsync(sync, context, query, cancellationToken);
        return [];
    }

    public async Task<List<T>> BulkInsertReturnEntities<T>(
        bool sync,
        DbContext context,
        IEnumerable<T> entities,
        BulkInsertOptions options,
        OnConflictOptions? onConflict = null,
        CancellationToken ctk = default
    ) where T : class
    {
        var (connection, wasClosed, transaction, wasBegan) = await context.GetConnection(sync, ctk);

        var (tableName, _) = await PerformBulkInsertAsync(sync, context, entities, options, tempTableRequired: true, ctk: ctk);

        var result = await CopyFromTempTableAsync<T>(sync, context, tableName, true, options, onConflict, cancellationToken: ctk);

        await Finish(sync, connection, wasClosed, transaction, wasBegan, ctk);

        return result;
    }

    private static async Task Finish(bool sync, DbConnection connection, bool wasClosed,
        IDbContextTransaction transaction, bool wasBegan, CancellationToken ctk)
    {
        if (!wasBegan)
        {
            if (sync)
            {
                // ReSharper disable once MethodHasAsyncOverloadWithCancellation
                transaction.Commit();
            }
            else
            {
                await transaction.CommitAsync(ctk);
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

    public async Task BulkInsert<T>(
        bool sync,
        DbContext context,
        IEnumerable<T> entities,
        BulkInsertOptions options,
        OnConflictOptions? onConflict = null,
        CancellationToken ctk = default
    ) where T : class
    {
        if (onConflict != null)
        {
            var (connection, wasClosed, transaction, wasBegan) = await context.GetConnection(sync, ctk);

            var (tableName, _) = await PerformBulkInsertAsync(sync, context, entities, options, tempTableRequired: true, ctk: ctk);

            await CopyFromTempTableAsync<T>(sync, context, tableName, false, options, onConflict, ctk);

            await Finish(sync, connection, wasClosed, transaction, wasBegan, ctk);
        }
        else
        {
            await PerformBulkInsertAsync(sync, context, entities, options, tempTableRequired: false, ctk: ctk);
        }
    }

    private async Task<(string TableName, DbConnection Connection)> PerformBulkInsertAsync<T>(
        bool sync,
        DbContext context,
        IEnumerable<T> entities,
        BulkInsertOptions options,
        bool tempTableRequired,
        CancellationToken ctk = default) where T : class
    {
        if (entities.TryGetNonEnumeratedCount(out var count) && count == 0)
        {
            throw new InvalidOperationException("No entities to insert.");
        }

        var (connection, wasClosed, transaction, wasBegan) = await context.GetConnection(sync, ctk);

        var tableName = tempTableRequired
            ? await CreateTableCopyAsync<T>(sync, context, ctk)
            : GetQuotedTableName(context, typeof(T));

        var properties = context
            .GetProperties(typeof(T), false)
            .Select(p => new PropertyAccessor(p))
            .ToArray();

        await BulkInsert(false, context, entities, tableName, properties, options, ctk);

        await Finish(sync, connection, wasClosed, transaction, wasBegan, ctk);

        return (tableName, connection);
    }

    /// <summary>
    /// The main bulk insert method: will insert either in a temp table or directly in the target table.
    /// </summary>
    protected abstract Task BulkInsert<T>(
        bool sync,
        DbContext context,
        IEnumerable<T> entities,
        string tableName,
        PropertyAccessor[] properties,
        BulkInsertOptions options,
        CancellationToken ctk
    ) where T : class;

    /// <summary>
    /// Get table information for the given entity type : schema name, table name and primary key.
    /// </summary>
    public static (string? SchemaName, string TableName, IKey PrimaryKey) GetTableInfo(DbContext context, Type entityType)
    {
        var entityTypeInfo = context.Model.FindEntityType(entityType);
        var schema = (entityTypeInfo ?? throw new InvalidOperationException($"Could not determine entity type for type {entityType.Name}")).GetSchema();
        var tableName = entityTypeInfo.GetTableName();

        if (string.IsNullOrWhiteSpace(tableName))
        {
            throw new InvalidOperationException($"Could not determine table name for type {entityType.Name}");
        }

        return (schema, tableName, entityTypeInfo.FindPrimaryKey()!);
    }

    protected string GetQuotedTableName(DbContext context, Type entityType)
    {
        var (schema, tableName, _) = GetTableInfo(context, entityType);

        return QuoteTableName(schema, tableName);
    }

    protected string QuoteTableName(string? schema, string table) => SqlDialect.QuoteTableName(schema, table);

    protected string[] GetQuotedColumns(DbContext context, Type entityType, bool includeGenerated = true)
    {
        return context.GetProperties(entityType, includeGenerated)
            .Select(p => Quote(p.GetColumnName()))
            .ToArray();
    }
}
