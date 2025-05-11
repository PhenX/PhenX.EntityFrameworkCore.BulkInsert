using System.Collections;
using System.Data.Common;
using System.Reflection;

using EntityFrameworkCore.ExecuteInsert.Abstractions;
using EntityFrameworkCore.ExecuteInsert.Dialect;
using EntityFrameworkCore.ExecuteInsert.Extensions;
using EntityFrameworkCore.ExecuteInsert.Options;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace EntityFrameworkCore.ExecuteInsert;

public abstract class BulkInsertProviderBase<TDialect> : IBulkInsertProvider
    where TDialect : SqlDialectBuilder, new()
{
    protected readonly TDialect SqlDialect;

    protected BulkInsertProviderBase()
    {
        SqlDialect = new TDialect();
    }

    protected virtual string BulkInsertId => "_bulk_insert_id";

    private static readonly MethodInfo CastMethod = typeof(Enumerable).GetMethod("Cast")!;
    private static readonly MethodInfo BulkInsertPkMethod = typeof(BulkInsertProviderBase<TDialect>).GetMethod(nameof(BulkInsertWithPrimaryKeyAsync))!;
    private static readonly MethodInfo GetChildrenMethod = typeof(BulkInsertProviderBase<TDialect>).GetMethod(nameof(GetChildrenEntities))!;

    protected abstract string CreateTableCopySql { get; }
    protected abstract string AddTableCopyBulkInsertId { get; }

    private static readonly MethodInfo GetFieldValueMethod =
        typeof(DbDataReader).GetMethod(nameof(DbDataReader.GetFieldValue))!;

    protected async Task<string> CreateTableCopyAsync<T>(
        DbContext context,
        DbConnection connection,
        CancellationToken cancellationToken = default) where T : class
    {
        var tableInfo = GetTableInfo(context, typeof(T));
        var tableName = EscapeTableName(tableInfo.SchemaName, tableInfo.TableName);
        var tempTableName = EscapeTableName(null, GetTempTableName(tableInfo.TableName));

        var keptColumns = string.Join(", ", GetEscapedColumns(context, typeof(T), false));
        var query = string.Format(CreateTableCopySql, tempTableName, tableName, keptColumns);
        await ExecuteAsync(connection, query, cancellationToken);

        await AddBulkInsertIdColumn<T>(connection, cancellationToken, tempTableName);

        return tempTableName;
    }

    protected virtual async Task AddBulkInsertIdColumn<T>(DbConnection connection, CancellationToken cancellationToken,
        string tempTableName) where T : class
    {
        var alterQuery = string.Format(AddTableCopyBulkInsertId, tempTableName);
        await ExecuteAsync(connection, alterQuery, cancellationToken);
    }

    protected virtual string GetTempTableName(string tableName) => $"_temp_bulk_insert_{tableName}";

    protected string Escape(string name) => SqlDialect.Escape(name);

    protected static async Task ExecuteAsync(DbConnection connection, string query, CancellationToken cancellationToken = default)
    {
        var command = connection.CreateCommand();
        command.CommandText = query;

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<List<T>> CopyFromTempTableAsync<T>(DbContext context,
        DbConnection connection,
        string tempTableName,
        bool returnData,
        BulkInsertOptions options,
        OnConflictOptions? onConflict = null,
        CancellationToken cancellationToken = default) where T : class
    {
        return await CopyFromTempTableWithoutKeysAsync<T, T>(
            context,
            connection,
            tempTableName,
            returnData,
            options,
            onConflict,
            cancellationToken: cancellationToken);
    }

    public async Task<List<KeyValuePair<long, object[]>>> CopyFromTempTablePrimaryKeyAsync<T>(
        DbContext context,
        DbConnection connection,
        string tempTableName,
        BulkInsertOptions options,
        CancellationToken cancellationToken = default) where T : class
    {
        return await CopyFromTempTableWithKeysAsync<T, object[]>(
            context,
            connection,
            tempTableName,
            options,
            cancellationToken: cancellationToken
        );
    }

    private async Task<List<KeyValuePair<long, TResult>>> CopyFromTempTableWithKeysAsync<T, TResult>(
        DbContext context,
        DbConnection connection,
        string tempTableName,
        BulkInsertOptions options,
        CancellationToken cancellationToken = default
    )
        where T : class
        where TResult : class
    {
        var (schemaName, tableName, primaryKey) = GetTableInfo(context, typeof(T));
        var escapedTableName = EscapeTableName(schemaName, tableName);

        var indexColumn = Escape(BulkInsertId);
        var returnedColumns = new[] { indexColumn }
            .Concat(primaryKey.Properties.Select(p => Escape(p.GetColumnName()))).ToArray();

        var query = ""; //BuildInsertSelectQuery(tempTableName, escapedTableName, returnedColumns, returnedColumns, moveRows);

        var result = new List<KeyValuePair<long, TResult>>();

        await using var command = connection.CreateCommand();
        command.CommandText = query;

        var getFieldValueMethods = primaryKey.Properties
            .Select(p => GetFieldValueMethod.MakeGenericMethod(p.ClrType))
            .ToArray();

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var index = (long)reader.GetValue(0);
            var values = new object[primaryKey.Properties.Count];
            for (var i = 0; i < primaryKey.Properties.Count; i++)
            {
                values[i] = getFieldValueMethods[i].Invoke(reader, new object[] { i + 1 })!;
            }

            var entity = Activator.CreateInstance(typeof(TResult), values) as TResult;
            result.Add(new KeyValuePair<long, TResult>(index, entity!));
        }

        return result;
    }

    private async Task<List<TResult>> CopyFromTempTableWithoutKeysAsync<T, TResult>(DbContext context,
        DbConnection connection,
        string tempTableName,
        bool returnData,
        BulkInsertOptions options,
        OnConflictOptions? onConflict = null,
        CancellationToken cancellationToken = default)
        where T : class
        where TResult : class
    {
        var (schemaName, tableName, _) = GetTableInfo(context, typeof(T));
        var escapedTableName = EscapeTableName(schemaName, tableName);

        var movedProperties = context.GetProperties(typeof(T), false);
        var returnedProperties = returnData ? context.GetProperties(typeof(T)) : [];

        var query = SqlDialect.BuildMoveDataSql<T>(context, tempTableName, escapedTableName, movedProperties, returnedProperties, options, onConflict);

        if (returnData)
        {
            // Use EF to execute the query and return the results
            return await context
                .Set<TResult>()
                .FromSqlRaw(query)
                .ToListAsync(cancellationToken);
        }

        // If not returning data, just execute the command
        await ExecuteAsync(connection, query, cancellationToken);
        return [];
    }

    public async Task<List<T>> BulkInsertWithIdentityAsync<T>(
        DbContext context,
        IEnumerable<T> entities,
        BulkInsertOptions options,
        OnConflictOptions? onConflict = null,
        CancellationToken ctk = default
    ) where T : class
    {
        var (connection, wasClosed) = await context.GetConnection(ctk);

        var (tableName, _) = await PerformBulkInsertAsync(context, entities, options, tempTableRequired: true, ctk: ctk);

        var result = await CopyFromTempTableAsync<T>(context, connection, tableName, true, options, onConflict, cancellationToken: ctk);

        if (wasClosed)
        {
            await connection.CloseAsync();
        }

        return result;
    }

    public async Task<List<KeyValuePair<long, object[]>>> BulkInsertWithPrimaryKeyAsync<T>(
        DbContext context,
        IEnumerable<T> entities,
        BulkInsertOptions options,
        CancellationToken ctk = default
    ) where T : class
    {
        var (connection, wasClosed) = await context.GetConnection(ctk);

        var (tableName, _) = await PerformBulkInsertAsync(context, entities, options, tempTableRequired: true, ctk: ctk);

        var result = await CopyFromTempTablePrimaryKeyAsync<T>(context, connection, tableName, options, ctk);

        if (wasClosed)
        {
            await connection.CloseAsync();
        }

        return result;
    }

    public async Task BulkInsertWithoutReturnAsync<T>(
        DbContext context,
        IEnumerable<T> entities,
        BulkInsertOptions options,
        OnConflictOptions? onConflict = null,
        CancellationToken ctk = default
    ) where T : class
    {
        if (onConflict != null)
        {
            var (connection, wasClosed) = await context.GetConnection(ctk);

            var (tableName, _) = await PerformBulkInsertAsync(context, entities, options, tempTableRequired: true, ctk: ctk);

            await CopyFromTempTableAsync<T>(context, connection, tableName, false, options, onConflict, ctk);

            if (wasClosed)
            {
                await connection.CloseAsync();
            }
        }
        else
        {
            await PerformBulkInsertAsync(context, entities, options, tempTableRequired: false, ctk: ctk);
        }
    }

    private async Task<(string TableName, DbConnection Connection)> PerformBulkInsertAsync<T>(DbContext context,
        IEnumerable<T> entities,
        BulkInsertOptions options,
        bool tempTableRequired,
        CancellationToken ctk = default) where T : class
    {
        if (entities.TryGetNonEnumeratedCount(out var count) && count == 0)
        {
            throw new InvalidOperationException("No entities to insert.");
        }

        var (connection, wasClosed) = await context.GetConnection(ctk);

        if (options.Recursive)
        {
            // Insert children first
            var navigationProperties = context.GetNavigationProperties(typeof(T));

            foreach (var navigationProperty in navigationProperties)
            {
                var itemType = navigationProperty.ClrType;
                var tupleType = typeof(KeyValuePair<,>).MakeGenericType(typeof(long), itemType);

                // Call GetChildrenEntities with reflection because the type is not known at compile time
                var allChildren = GetChildrenMethod.MakeGenericMethod(typeof(T)).Invoke(this, [entities, navigationProperty]) as IEnumerable<KeyValuePair<long, object>>;

                // Cast the IEnumerable to the correct type
                var items = new List<KeyValuePair<long, object>>(allChildren);
                // var itemsCasted = CastMethod.MakeGenericMethod(tupleType).Invoke(null, [items]);
                // var items = allChildren.Cast<KeyValuePair<long, object>>();

                // Call BulkInsertWithPrimaryKeyAsync to insert elements and get their primary key values
                var bulkInsert = BulkInsertPkMethod.MakeGenericMethod(tupleType);

                var pkValues = await (bulkInsert.Invoke(this, [context, items, options, ctk]) as Task<List<KeyValuePair<long, object[]>>>)!;
            }
        }

        var tableName = tempTableRequired || options.Recursive
            ? await CreateTableCopyAsync<T>(context, connection, ctk)
            : GetEscapedTableName(context, typeof(T));

        // Utilisation du wrapper PropertyAccessor
        var properties = context
            .GetProperties(typeof(T), false)
            .Select(p => new PropertyAccessor(p))
            .ToArray();

        await BulkInsert(context, entities, tableName, properties, options, ctk);

        if (wasClosed)
        {
            await connection.CloseAsync();
        }

        return (tableName, connection);
    }

    protected abstract Task BulkInsert<T>(DbContext context, IEnumerable<T> entities,
        string tableName, PropertyAccessor[] properties, BulkInsertOptions options, CancellationToken ctk) where T : class;

    public IEnumerable<KeyValuePair<long, object>> GetChildrenEntities<T>(IEnumerable<T> entities, INavigation navigationProperty) where T : class
    {
        var navProp = navigationProperty.PropertyInfo;
        var isCollection = navigationProperty.IsCollection;
        long index = 0;

        foreach (var e in entities)
        {
            var value = navProp!.GetValue(e);

            if (isCollection && value is IEnumerable enumerable)
            {
                foreach (var childEntity in enumerable)
                {
                    yield return new KeyValuePair<long, object>(index, childEntity);
                }
            }
            else if (value != null)
            {
                yield return new KeyValuePair<long, object>(index, value);
            }

            index++;
        }
    }

    /// <summary>
    /// Escapes a schema and table name using database-specific delimiters.
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

    protected string GetEscapedTableName(DbContext context, Type entityType)
    {
        var (schema, tableName, _) = GetTableInfo(context, entityType);

        return EscapeTableName(schema, tableName);
    }

    protected string EscapeTableName(string? schema, string table) => SqlDialect.EscapeTableName(schema, table);

    protected string[] GetEscapedColumns(DbContext context, Type entityType, bool includeGenerated = true)
    {
        return context.GetProperties(entityType, includeGenerated)
            .Select(p => Escape(p.GetColumnName()))
            .ToArray();
    }
}
