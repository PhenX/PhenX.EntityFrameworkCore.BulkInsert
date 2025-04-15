using System.Collections;
using System.Data;
using System.Data.Common;
using System.Reflection;

using EntityFrameworkCore.ExecuteInsert.Abstractions;
using EntityFrameworkCore.ExecuteInsert.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

using Npgsql;

namespace EntityFrameworkCore.ExecuteInsert.PostgreSql;

public class BulkInsertProvider : IBulkInsertProvider
{
    private static readonly MethodInfo CastMethod = typeof(Enumerable).GetMethod("Cast")!;
    private static readonly MethodInfo BulkInsertMethod = typeof(BulkInsertProvider).GetMethod(nameof(BulkInsertAsync))!;
    private static readonly MethodInfo GetChildrenMethod = typeof(BulkInsertProvider).GetMethod(nameof(GetChildrenEntities))!;

    public async Task<string> CreateTableCopyAsync<T>(DbConnection connection, DbContext context,
        CancellationToken cancellationToken = default) where T : class
    {
        var tableInfo = DatabaseHelper.GetTableInfo(context, typeof(T));
        var tableName = DatabaseHelper.GetEscapedTableName(tableInfo.SchemaName, tableInfo.TableName, OpenDelimiter, CloseDelimiter);
        var tempTableName = DatabaseHelper.GetEscapedTableName(null, $"_temp_bulk_insert_{tableInfo.TableName}", OpenDelimiter, CloseDelimiter);

        //language=sql
        const string createTable = "CREATE TEMPORARY TABLE {0} AS TABLE {1} WITH NO DATA;";

        var query = string.Format(createTable, tempTableName, tableName);

        var command = connection.CreateCommand();
        command.CommandText = query;

        await command.ExecuteNonQueryAsync(cancellationToken);

        return tempTableName;
    }

    public async Task<List<T>> CopyFromTempTableAsync<T>(DbConnection connection, DbContext context, string tempTableName, bool returnIdentity = false,
        CancellationToken cancellationToken = default) where T : class
    {
        var tableInfo = DatabaseHelper.GetTableInfo(context, typeof(T));
        var tableName = DatabaseHelper.GetEscapedTableName(tableInfo.SchemaName, tableInfo.TableName, OpenDelimiter, CloseDelimiter);

        if (returnIdentity)
        {
            var columnAliases = DatabaseHelper.GetProperties(context, typeof(T))
                .Select(p => $"{DatabaseHelper.GetEscapedColumnName(p.GetColumnName(), OpenDelimiter, CloseDelimiter)} AS {DatabaseHelper.GetEscapedColumnName(p.Name, OpenDelimiter, CloseDelimiter)}")
                .ToArray();

            //language=sql
            const string insertIntoSelect = "INSERT INTO {0} ({1}) SELECT {1} FROM {2} RETURNING {3};";

            var columns = GetEscapedColumns(context, typeof(T));
            var query = string.Format(insertIntoSelect, tableName, string.Join(", ", columns), tempTableName, string.Join(", ", columnAliases));

            return await context.Set<T>().FromSqlRaw(query).ToListAsync(cancellationToken);
        }
        else
        {
            //language=sql
            const string insertIntoSelect = "INSERT INTO {0} ({1}) SELECT {1} FROM {2};";

            var columns = GetEscapedColumns(context, typeof(T));
            var query = string.Format(insertIntoSelect, tableName, string.Join(", ", columns), tempTableName);

            var command = connection.CreateCommand();
            command.CommandText = query;

            await command.ExecuteNonQueryAsync(cancellationToken);

            return [];
        }
    }

    public string OpenDelimiter => "\"";
    public string CloseDelimiter => "\"";

    public async Task<IEnumerable<T>> BulkInsertAsync<T>(
        DbContext context,
        IEnumerable<T> entities,
        BulkInsertOptions options,
        CancellationToken ctk = default
        ) where T : class
    {
        if (entities.TryGetNonEnumeratedCount(out var count) && count == 0)
        {
            return [];
        }

        if (!options.OnlyRootEntities)
        {
            // Insert children first
            var navigationProperties = DatabaseHelper.GetNavigationProperties(context, typeof(T));

            foreach (var navigationProperty in navigationProperties)
            {
                var itemType = navigationProperty.ClrType;

                // Call GetChildrenEntities with reflection because the type is not known at compile time
                var allChildren = GetChildrenMethod.MakeGenericMethod(typeof(T)).Invoke(this, [entities, navigationProperty]) as IEnumerable;

                // Cast the IEnumerable to the correct type
                var items = CastMethod.MakeGenericMethod(itemType).Invoke(null, [allChildren]);

                // Call BulkInsertAsync
                var method = BulkInsertMethod.MakeGenericMethod(itemType);
                await (method.Invoke(this, [context, items, options, ctk]) as Task)!;
            }
        }

        var connection = (NpgsqlConnection)context.Database.GetDbConnection();
        var wasClosed = connection.State == ConnectionState.Closed;

        if (wasClosed)
        {
            await connection.OpenAsync(ctk);
        }

        var tempTableInsertion = !options.OnlyRootEntities || options.ReturnIdentity;

        // Then insert the main entities
        string tableName;

        if (tempTableInsertion)
        {
            tableName = await CreateTableCopyAsync<T>(connection, context, ctk);
        }
        else
        {
            tableName = GetEscapedTableName(context, typeof(T));
        }

        var importCommand = GetBinaryImportCommand(context, typeof(T), tableName);

        await using (var writer = await connection.BeginBinaryImportAsync(importCommand, ctk))
        {
            foreach (var entity in entities)
            {
                await writer.StartRowAsync(ctk);
                foreach (var property in DatabaseHelper.GetProperties(context, typeof(T)))
                {
                    var value = property.PropertyInfo!.GetValue(entity);
                    await writer.WriteAsync(value ?? DBNull.Value, ctk);
                }
            }

            await writer.CompleteAsync(ctk);
        }

        var result = new List<T>();

        if (tempTableInsertion)
        {
            result = await CopyFromTempTableAsync<T>(connection, context, tableName, options.ReturnIdentity, ctk);
        }

        if (wasClosed)
        {
            await connection.CloseAsync();
        }

        return result;
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
