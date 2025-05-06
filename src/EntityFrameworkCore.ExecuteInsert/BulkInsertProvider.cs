using System.Collections;
using System.Data;
using System.Data.Common;
using System.Reflection;

using EntityFrameworkCore.ExecuteInsert.Abstractions;
using EntityFrameworkCore.ExecuteInsert.Helpers;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace EntityFrameworkCore.ExecuteInsert;

public abstract class BulkInsertProviderBase : IBulkInsertProvider
{
    private static readonly MethodInfo CastMethod = typeof(Enumerable).GetMethod("Cast")!;
    private static readonly MethodInfo BulkInsertPkMethod = typeof(BulkInsertProviderBase).GetMethod(nameof(BulkInsertWithPrimaryKeyAsync))!;
    private static readonly MethodInfo GetChildrenMethod = typeof(BulkInsertProviderBase).GetMethod(nameof(GetChildrenEntities))!;

    protected abstract string CreateTableCopySql { get; }
    protected abstract string AddTableCopyBulkInsertId { get; }

    private static readonly MethodInfo GetFieldValueMethod =
        typeof(DbDataReader).GetMethod(nameof(DbDataReader.GetFieldValue))!;

    public async Task<string> CreateTableCopyAsync<T>(
        DbContext context,
        DbConnection connection,
        CancellationToken cancellationToken = default) where T : class
    {
        var tableInfo = DatabaseHelper.GetTableInfo(context, typeof(T));
        var tableName = DatabaseHelper.GetEscapedTableName(tableInfo.SchemaName, tableInfo.TableName, OpenDelimiter, CloseDelimiter);
        var tempTableName = DatabaseHelper.GetEscapedTableName(null, GetTempTableName<T>(tableInfo.TableName), OpenDelimiter, CloseDelimiter);

        var keepedColumns = string.Join(", ", GetEscapedColumns(context, typeof(T), false));
        var query = string.Format(CreateTableCopySql, tempTableName, tableName, keepedColumns);
        await ExecuteAsync(connection, query, cancellationToken);

        var alterQuery = string.Format(AddTableCopyBulkInsertId, tempTableName);
        await ExecuteAsync(connection, alterQuery, cancellationToken);

        return tempTableName;
    }

    protected virtual string GetTempTableName<T>(string tableName) where T : class
    {
        return $"_temp_bulk_insert_{tableName}";
    }

    public abstract string OpenDelimiter { get; }
    public abstract string CloseDelimiter { get; }

    private static async Task ExecuteAsync(DbConnection connection, string query, CancellationToken cancellationToken = default)
    {
        var command = connection.CreateCommand();
        command.CommandText = query;

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<List<T>> CopyFromTempTableAsync<T>(
        DbContext context,
        DbConnection connection,
        string tempTableName,
        bool moveRows = false,
        CancellationToken cancellationToken = default) where T : class
    {
        return await CopyFromTempTableWithoutKeysAsync<T, T>(
            context,
            connection,
            tempTableName,
            moveRows,
            cancellationToken: cancellationToken);
    }

    public async Task<List<KeyValuePair<long, object[]>>> CopyFromTempTablePrimaryKeyAsync<T>(
        DbContext context,
        DbConnection connection,
        string tempTableName,
        bool moveRows = false,
        CancellationToken cancellationToken = default) where T : class
    {
        return await CopyFromTempTableWithKeysAsync<T, object[]>(
            context,
            connection,
            tempTableName,
            moveRows,
            cancellationToken: cancellationToken
        );
    }

    private async Task<List<KeyValuePair<long, TResult>>> CopyFromTempTableWithKeysAsync<T, TResult>(
        DbContext context,
        DbConnection connection,
        string tempTableName,
        bool moveRows,
        CancellationToken cancellationToken = default
    )
        where T : class
        where TResult : class
    {
        var (schemaName, tableName, primaryKey) = DatabaseHelper.GetTableInfo(context, typeof(T));
        var escapedTableName = DatabaseHelper.GetEscapedTableName(schemaName, tableName, OpenDelimiter, CloseDelimiter);

        var indexColumn = Escape("_bulk_insert_id");
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

    private async Task<List<TResult>> CopyFromTempTableWithoutKeysAsync<T, TResult>(
        DbContext context,
        DbConnection connection,
        string tempTableName,
        bool moveRows,
        CancellationToken cancellationToken = default
    )
        where T : class
        where TResult : class
    {
        var (schemaName, tableName, _) = DatabaseHelper.GetTableInfo(context, typeof(T));
        var escapedTableName = DatabaseHelper.GetEscapedTableName(schemaName, tableName, OpenDelimiter, CloseDelimiter);

        var movedProperties = DatabaseHelper.GetProperties(context, typeof(T), false);
        var returnedProperties = DatabaseHelper.GetProperties(context, typeof(T));

        var query = BuildInsertSelectQuery(tempTableName, escapedTableName, movedProperties, returnedProperties, moveRows);

        return await context.Set<TResult>().FromSqlRaw(query).ToListAsync(cancellationToken);
    }

    protected string Escape(string columnName)
    {
        return DatabaseHelper.GetEscapedColumnName(columnName, OpenDelimiter, CloseDelimiter);
    }

    protected virtual string BuildInsertSelectQuery(string tempTableName, string targetTableName,
        IProperty[] insertedProperties, IProperty[] properties, bool moveRows)
    {
        var insertedColumns = insertedProperties.Select(p => Escape(p.GetColumnName()));
        var insertedColumnList = string.Join(", ", insertedColumns);

        var returnedColumns = properties.Select(p => $"{Escape(p.GetColumnName())} AS {Escape(p.Name)}");
        var columnList = string.Join(", ", returnedColumns);

        if (moveRows)
        {
            //language=sql
            return $"""
                    WITH moved_rows AS (
                       DELETE FROM {tempTableName}
                           RETURNING {insertedColumnList}
                    )
                    INSERT INTO {targetTableName} ({insertedColumnList})
                    SELECT {insertedColumnList}
                    FROM moved_rows
                        RETURNING {columnList};
                    """;
        }

        //language=sql
        return $"""
                INSERT INTO {targetTableName} ({insertedColumnList})
                SELECT {insertedColumnList}
                FROM {tempTableName}
                    RETURNING {columnList};
                """;
    }

    public async Task<List<T>> BulkInsertWithIdentityAsync<T>(
        DbContext context,
        IEnumerable<T> entities,
        BulkInsertOptions options,
        CancellationToken ctk = default
    ) where T : class
    {
        var (connection, wasClosed) = await GetConnection(context, ctk);

        var (tableName, _) = await PerformBulkInsertAsync(context, entities, options, tempTableRequired: true, ctk: ctk);

        var result = await CopyFromTempTableAsync<T>(context, connection, tableName, options.MoveRows, ctk);

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
        var (connection, wasClosed) = await GetConnection(context, ctk);

        var (tableName, _) = await PerformBulkInsertAsync(context, entities, options, tempTableRequired: true, ctk: ctk);

        var result = await CopyFromTempTablePrimaryKeyAsync<T>(context, connection, tableName, options.MoveRows, ctk);

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
        CancellationToken ctk = default
    ) where T : class
    {
        await PerformBulkInsertAsync(context, entities, options, tempTableRequired: false, ctk: ctk);
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

        var (connection, wasClosed) = await GetConnection(context, ctk);

        if (options.Recursive)
        {
            // Insert children first
            var navigationProperties = DatabaseHelper.GetNavigationProperties(context, typeof(T));

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
        var properties = DatabaseHelper
            .GetProperties(context, typeof(T), false)
            .Select(p => new PropertyAccessor(p))
            .ToArray();

        await BulkImport(context, connection, entities, tableName, properties, ctk);

        if (wasClosed)
        {
            await connection.CloseAsync();
        }

        return (tableName, connection);
    }

    protected abstract Task BulkImport<T>(DbContext context, DbConnection connection, IEnumerable<T> entities,
        string tableName, PropertyAccessor[] properties, CancellationToken ctk) where T : class;

    private static async Task<(DbConnection connection, bool wasClosed)> GetConnection(DbContext context, CancellationToken ctk)
    {
        var connection = context.Database.GetDbConnection();
        var wasClosed = connection.State == ConnectionState.Closed;

        if (wasClosed)
        {
            await connection.OpenAsync(ctk);
        }

        return (connection, wasClosed);
    }

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

    protected string[] GetEscapedColumns(DbContext context, Type entityType, bool includeGenerated = true)
    {
        return DatabaseHelper.GetProperties(context, entityType, includeGenerated)
            .Select(p => Escape(p.Name))
            .ToArray();
    }

    protected string GetEscapedTableName(DbContext context, Type entityType)
    {
        return DatabaseHelper.GetEscapedTableName(context, entityType, OpenDelimiter, CloseDelimiter);
    }
}
