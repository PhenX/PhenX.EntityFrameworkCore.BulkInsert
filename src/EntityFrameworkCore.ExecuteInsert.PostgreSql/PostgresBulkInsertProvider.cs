using System.Collections;
using System.Data;
using System.Data.Common;

using EntityFrameworkCore.ExecuteInsert.Abstractions;
using EntityFrameworkCore.ExecuteInsert.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

using Npgsql;

namespace EntityFrameworkCore.ExecuteInsert.PostgreSql;

public class PostgresBulkInsertProvider : IBulkInsertProvider
{
    public async Task<string> CreateTableCopyAsync<T>(DbConnection connection, DbContext context,
        CancellationToken cancellationToken = default) where T : class
    {
        var tableInfo = DatabaseHelper.GetTableInfo(context, typeof(T));
        var tableName = DatabaseHelper.GetEscapedTableName(tableInfo.SchemaName, tableInfo.TableName, OpenDelimiter, CloseDelimiter);
        var tempTableName = DatabaseHelper.GetEscapedTableName(tableInfo.SchemaName, $"_temp_bulk_insert_{tableInfo.TableName}", OpenDelimiter, CloseDelimiter);

        //language=sql
        const string createTable = "CREATE TEMPORARY TABLE {0} AS TABLE {1} WITH NO DATA;";

        var query = string.Format(createTable, tempTableName, tableName);

        var command = connection.CreateCommand();
        command.CommandText = query;

        await command.ExecuteNonQueryAsync(cancellationToken);

        return tempTableName;
    }

    public async Task CopyFromTempTableAsync<T>(DbConnection connection, DbContext context, string tempTableName,
        CancellationToken cancellationToken = default) where T : class
    {
        var tableInfo = DatabaseHelper.GetTableInfo(context, typeof(T));
        var tableName = DatabaseHelper.GetEscapedTableName(tableInfo.SchemaName, tableInfo.TableName, OpenDelimiter, CloseDelimiter);

        //language=sql
        const string insertIntoSelect = "INSERT INTO {0} ({1}) SELECT {1} FROM {2};";

        var columns = GetEscapedColumns(context, typeof(T));
        var query = string.Format(insertIntoSelect, tableName, string.Join(", ", columns), tempTableName);

        var command = connection.CreateCommand();
        command.CommandText = query;

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public string OpenDelimiter => "\"";
    public string CloseDelimiter => "\"";

    public async Task BulkInsertAsync<T>(DbContext context, IEnumerable<T> entities, BulkInsertOptions? options = null,
        CancellationToken cancellationToken = default) where T : class
    {
        if (entities.TryGetNonEnumeratedCount(out var count) && count == 0)
        {
            return;
        }

        // Insert children first
        var navigationProperties = DatabaseHelper.GetNavigationProperties(context, typeof(T));

        var bulkInsert = typeof(PostgresBulkInsertProvider).GetMethod(nameof(BulkInsertAsync))!;
        var getChildren = typeof(PostgresBulkInsertProvider).GetMethod(nameof(GetChildrenEntities))!;
        var cast = typeof(Enumerable).GetMethod("Cast")!;

        foreach (var navigationProperty in navigationProperties)
        {
            var itemType = navigationProperty.ClrType;

            // Call BulkInsertAsync with reflection because the type is not known at compile time
            var method = bulkInsert.MakeGenericMethod(itemType);

            // Call GetChildrenEntities with reflection because the type is not known at compile time
            var getChildrenMethod = getChildren.MakeGenericMethod(typeof(T));

            var allChildren = getChildrenMethod.Invoke(this, [entities, navigationProperty]) as IEnumerable;

            var castMethod = cast.MakeGenericMethod(itemType);

            var result = castMethod.Invoke(null, [allChildren]);

            await (method.Invoke(this, [context, result, null, cancellationToken]) as Task)!;
        }

        var connection = (NpgsqlConnection)context.Database.GetDbConnection();
        var wasClosed = connection.State == ConnectionState.Closed;

        if (wasClosed)
        {
            await connection.OpenAsync(cancellationToken);
        }

        var returningId = true;

        // Then insert the main entities
        string tableName;

        if (returningId)
        {
            tableName = await CreateTableCopyAsync<T>(connection, context, cancellationToken);
        }
        else
        {
            tableName = GetEscapedTableName(context, typeof(T));
        }

        await using (var writer =
                     await connection.BeginBinaryImportAsync(GetBinaryImportCommand(context, typeof(T), tableName),
                         cancellationToken))
        {
            foreach (var entity in entities)
            {
                await writer.StartRowAsync(cancellationToken);
                foreach (var property in DatabaseHelper.GetProperties(context, typeof(T)))
                {
                    var value = property.PropertyInfo!.GetValue(entity);
                    await writer.WriteAsync(value ?? DBNull.Value, cancellationToken);
                }
            }

            await writer.CompleteAsync(cancellationToken);
        }

        if (returningId)
        {
            await CopyFromTempTableAsync<T>(connection, context, tableName, cancellationToken);
        }

        if (wasClosed)
        {
            await connection.CloseAsync();
        }
    }

    public IEnumerable GetChildrenEntities<T>(IEnumerable<T> entities, INavigation navigationProperty) where T : class
    {
        var navProp = navigationProperty.PropertyInfo;

        foreach (var e in entities)
        {
            var value = navProp!.GetValue(e);

            if (value is IEnumerable enumerable)
            {
                foreach (var childEntity in enumerable)
                {
                    yield return childEntity;
                }
            }
            else
            {
                yield return value;
            }
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
