using System.Collections;
using System.Data;
using System.Data.Common;
using System.Reflection;

using EntityFrameworkCore.ExecuteInsert.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

using Npgsql;

namespace EntityFrameworkCore.ExecuteInsert.PostgreSql;

public class BulkInsertProvider : IBulkInsertProvider
{
    private static readonly MethodInfo CastMethod = typeof(Enumerable).GetMethod("Cast")!;
    private static readonly MethodInfo BulkInsertPkMethod = typeof(BulkInsertProvider).GetMethod(nameof(BulkInsertWithPrimaryKeyAsync))!;
    private static readonly MethodInfo GetChildrenMethod = typeof(BulkInsertProvider).GetMethod(nameof(GetChildrenEntities))!;

    private static readonly MethodInfo GetFieldValueMethod =
        typeof(DbDataReader).GetMethod(nameof(DbDataReader.GetFieldValue))!;

    public async Task<string> CreateTableCopyAsync<T>(
        DbContext context,
        DbConnection connection,
        CancellationToken cancellationToken = default) where T : class
    {
        var tableInfo = DatabaseHelper.GetTableInfo(context, typeof(T));
        var tableName = DatabaseHelper.GetEscapedTableName(tableInfo.SchemaName, tableInfo.TableName, OpenDelimiter, CloseDelimiter);
        var tempTableName = DatabaseHelper.GetEscapedTableName(null, $"_temp_bulk_insert_{tableInfo.TableName}", OpenDelimiter, CloseDelimiter);

        //language=sql
        const string createTable = "CREATE TABLE {0} AS TABLE {1} WITH NO DATA;";
        var query = string.Format(createTable, tempTableName, tableName);
        await ExecuteAsync(connection, query, cancellationToken);

        //language=sql
        const string addPrimaryKey = "ALTER TABLE {0} ADD COLUMN _bulk_insert_id SERIAL PRIMARY KEY;";
        var alterQuery = string.Format(addPrimaryKey, tempTableName);
        await ExecuteAsync(connection, alterQuery, cancellationToken);

        return tempTableName;
    }

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

        var indexColumn = DatabaseHelper.GetEscapedColumnName("_bulk_insert_id", OpenDelimiter, CloseDelimiter);
        var returnedColumns = new[] { indexColumn }
            .Concat(primaryKey.Properties.Select(p =>
                DatabaseHelper.GetEscapedColumnName(p.GetColumnName(), OpenDelimiter, CloseDelimiter)));

        var query = BuildInsertSelectQuery(tempTableName, escapedTableName, returnedColumns, moveRows);

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

        var movedProperties = DatabaseHelper.GetProperties(context, typeof(T));
        var returnedColumns = movedProperties.Select(p =>
            $"{DatabaseHelper.GetEscapedColumnName(p.GetColumnName(), OpenDelimiter, CloseDelimiter)} AS {DatabaseHelper.GetEscapedColumnName(p.Name, OpenDelimiter, CloseDelimiter)}");

        var query = BuildInsertSelectQuery(tempTableName, escapedTableName, returnedColumns, moveRows);

        return await context.Set<TResult>().FromSqlRaw(query).ToListAsync(cancellationToken);
    }

    private string BuildInsertSelectQuery(string tempTableName, string targetTableName, IEnumerable<string> columns, bool moveRows)
    {
        var columnList = string.Join(", ", columns);

        return moveRows
            ? $"WITH moved_rows AS (DELETE FROM {tempTableName} RETURNING {columnList}) INSERT INTO {targetTableName} SELECT * FROM moved_rows RETURNING {columnList};"
            : $"INSERT INTO {targetTableName} ({columnList}) SELECT {columnList} FROM {tempTableName} RETURNING {columnList};";
    }

    public string OpenDelimiter => "\"";
    public string CloseDelimiter => "\"";

    public async Task<List<T>> BulkInsertWithIdentityAsync<T>(
        DbContext context,
        IEnumerable<T> entities,
        BulkInsertOptions options,
        CancellationToken ctk = default
    ) where T : class
    {
        var (tableName, connection) = await PerformBulkInsertAsync(context, entities, options, tempTableRequired: true, ctk: ctk);
        return await CopyFromTempTableAsync<T>(context, connection, tableName, options.MoveRows, ctk);
    }

    public async Task<List<KeyValuePair<long, object[]>>> BulkInsertWithPrimaryKeyAsync<T>(
        DbContext context,
        IEnumerable<T> entities,
        BulkInsertOptions options,
        CancellationToken ctk = default
    ) where T : class
    {
        var (tableName, connection) = await PerformBulkInsertAsync(context, entities, options, tempTableRequired: true, ctk: ctk);

        return await CopyFromTempTablePrimaryKeyAsync<T>(context, connection, tableName, options.MoveRows, ctk);
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

        var connection = (NpgsqlConnection)context.Database.GetDbConnection();
        var wasClosed = connection.State == ConnectionState.Closed;

        if (wasClosed)
        {
            await connection.OpenAsync(ctk);
        }

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
                var itemsCasted = CastMethod.MakeGenericMethod(tupleType).Invoke(null, [items]);
                // var items = allChildren.Cast<KeyValuePair<long, object>>();

                // Call BulkInsertWithPrimaryKeyAsync to insert elements and get their primary key values
                var bulkInsert = BulkInsertPkMethod.MakeGenericMethod(tupleType);

                var pkValues = await (bulkInsert.Invoke(this, [context, itemsCasted, options, ctk]) as Task<List<KeyValuePair<long, object[]>>>)!;
            }
        }

        var tableName = tempTableRequired || options.Recursive
            ? await CreateTableCopyAsync<T>(context, connection, ctk)
            : GetEscapedTableName(context, typeof(T));

        var importCommand = GetBinaryImportCommand(context, typeof(T), tableName);

        // Utilisation du wrapper PropertyAccessor
        var properties = DatabaseHelper
            .GetProperties(context, typeof(T))
            .Select(p => new PropertyAccessor(p))
            .ToArray();

        await using (var writer = await connection.BeginBinaryImportAsync(importCommand, ctk))
        {
            foreach (var entity in entities)
            {
                await writer.StartRowAsync(ctk);

                foreach (var property in properties)
                {
                    var value = property.GetValue(entity);

                    await writer.WriteAsync(value, ctk);
                }
            }

            await writer.CompleteAsync(ctk);
        }

        if (wasClosed)
        {
            await connection.CloseAsync();
        }

        return (tableName, connection);
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

    private string GetBinaryImportCommand(DbContext context, Type entityType, string tableName)
    {
        var columns = GetEscapedColumns(context, entityType);

        return $"COPY {tableName} ({string.Join(", ", columns)}) FROM STDIN (FORMAT BINARY)";
    }

    private string[] GetEscapedColumns(DbContext context, Type entityType)
    {
        return DatabaseHelper.GetProperties(context, entityType)
            .Select(p => DatabaseHelper.GetEscapedColumnName(p.Name, OpenDelimiter, CloseDelimiter))
            .ToArray();
    }

    private string GetEscapedTableName(DbContext context, Type entityType)
    {
        return DatabaseHelper.GetEscapedTableName(context, entityType, OpenDelimiter, CloseDelimiter);
    }
}
