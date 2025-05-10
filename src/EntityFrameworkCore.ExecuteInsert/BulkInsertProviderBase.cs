using System.Collections;
using System.Data;
using System.Data.Common;
using System.Linq.Expressions;
using System.Reflection;

using EntityFrameworkCore.ExecuteInsert.Abstractions;
using EntityFrameworkCore.ExecuteInsert.Helpers;
using EntityFrameworkCore.ExecuteInsert.OnConflict;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace EntityFrameworkCore.ExecuteInsert;

public abstract class BulkInsertProviderBase : IBulkInsertProvider
{
    protected virtual string BulkInsertId => "_bulk_insert_id";

    private static readonly MethodInfo CastMethod = typeof(Enumerable).GetMethod("Cast")!;
    private static readonly MethodInfo BulkInsertPkMethod = typeof(BulkInsertProviderBase).GetMethod(nameof(BulkInsertWithPrimaryKeyAsync))!;
    private static readonly MethodInfo GetChildrenMethod = typeof(BulkInsertProviderBase).GetMethod(nameof(GetChildrenEntities))!;

    protected abstract string CreateTableCopySql { get; }
    protected abstract string AddTableCopyBulkInsertId { get; }

    private static readonly MethodInfo GetFieldValueMethod =
        typeof(DbDataReader).GetMethod(nameof(DbDataReader.GetFieldValue))!;

    protected async Task<string> CreateTableCopyAsync<T>(
        DbContext context,
        DbConnection connection,
        CancellationToken cancellationToken = default) where T : class
    {
        var tableInfo = DatabaseHelper.GetTableInfo(context, typeof(T));
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

    public abstract string OpenDelimiter { get; }
    public abstract string CloseDelimiter { get; }

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
        var (schemaName, tableName, primaryKey) = DatabaseHelper.GetTableInfo(context, typeof(T));
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
        var (schemaName, tableName, _) = DatabaseHelper.GetTableInfo(context, typeof(T));
        var escapedTableName = EscapeTableName(schemaName, tableName);

        var movedProperties = DatabaseHelper.GetProperties(context, typeof(T), false);
        var returnedProperties = returnData ? DatabaseHelper.GetProperties(context, typeof(T)) : [];

        var query = BuildInsertSelectQuery<T>(tempTableName, escapedTableName, movedProperties, returnedProperties, options, onConflict);

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

    protected string Escape(string columnName)
    {
        return DatabaseHelper.GetEscapedColumnName(columnName, OpenDelimiter, CloseDelimiter);
    }

    protected abstract string BuildInsertSelectQuery<T>(string tableName,
        string targetTableName,
        IProperty[] insertedProperties,
        IProperty[] properties,
        BulkInsertOptions options,
        OnConflictOptions? onConflict = null);

    public async Task<List<T>> BulkInsertWithIdentityAsync<T>(
        DbContext context,
        IEnumerable<T> entities,
        BulkInsertOptions options,
        OnConflictOptions? onConflict = null,
        CancellationToken ctk = default
    ) where T : class
    {
        var (connection, wasClosed) = await GetConnection(context, ctk);

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
        var (connection, wasClosed) = await GetConnection(context, ctk);

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
            var (connection, wasClosed) = await GetConnection(context, ctk);

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

        await BulkImport(context, connection, entities, tableName, properties, options, ctk);

        if (wasClosed)
        {
            await connection.CloseAsync();
        }

        return (tableName, connection);
    }

    protected abstract Task BulkImport<T>(DbContext context, DbConnection connection, IEnumerable<T> entities,
        string tableName, PropertyAccessor[] properties, BulkInsertOptions options, CancellationToken ctk) where T : class;

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

    protected string EscapeTableName(string? schema, string table)
    {
        return DatabaseHelper.GetEscapedTableName(schema, table, OpenDelimiter, CloseDelimiter);
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

    protected IEnumerable<string> GetUpdates<T>(Expression<Func<T, object>> update)
    {
        switch (update.Body)
        {
            case NewExpression { Members: not null } newExpr:
            {
                foreach (var arg in newExpr.Arguments.Zip(newExpr.Members, (expr, member) => (expr, member)))
                {
                    yield return $"{Escape(arg.member.Name)} = {ToSqlExpression(arg.expr)}";
                }

                break;
            }
            case MemberInitExpression memberInit:
            {
                foreach (var binding in memberInit.Bindings.OfType<MemberAssignment>())
                {
                    yield return $"{Escape(binding.Member.Name)} = {ToSqlExpression(binding.Expression)}";
                }

                break;
            }
            case MemberExpression memberExpr:
                yield return $"{Escape(memberExpr.Member.Name)} = {ToSqlExpression(memberExpr)}";
                break;
            default:
                throw new NotSupportedException("Unsupported expression type for update");
        }
    }

    protected virtual string ConcatOperator => "||";

    protected virtual string GetExcludedColumnName(MemberExpression member)
    {
        var prefix = "EXCLUDED";
        return $"{prefix}.{Escape(member.Member.Name)}";
    }

    private string ToSqlExpression(Expression expr)
    {
        switch (expr)
        {
            case MemberExpression m:
                return GetExcludedColumnName(m);

            case BinaryExpression b:
                var left = ToSqlExpression(b.Left);
                var right = ToSqlExpression(b.Right);
                var op = b.NodeType switch
                {
                    ExpressionType.Add => b.Type == typeof(string) ? ConcatOperator : "+",
                    ExpressionType.Subtract => "-",
                    ExpressionType.Multiply => "*",
                    ExpressionType.Divide => "/",
                    ExpressionType.Modulo => "%",
                    ExpressionType.AndAlso => "AND",
                    ExpressionType.OrElse => "OR",
                    ExpressionType.Equal => "=",
                    ExpressionType.NotEqual => "<>",
                    ExpressionType.LessThan => "<",
                    ExpressionType.LessThanOrEqual => "<=",
                    ExpressionType.GreaterThan => ">",
                    ExpressionType.GreaterThanOrEqual => ">=",
                    _ => throw new NotSupportedException($"Unsupported operator: {b.NodeType}")
                };
                return $"({left} {op} {right})";

            case ConstantExpression c:
                if (c.Type == typeof(RawSqlValue) && c.Value != null)
                {
                    return ((RawSqlValue)c.Value!).Sql;
                }

                if (c.Type == typeof(string) ||
                    c.Type == typeof(Guid))
                {
                    return $"'{c.Value}'";
                }

                if (c.Type == typeof(bool))
                {
                    return (bool)c.Value! ? "TRUE" : "FALSE";
                }

                return c.Value?.ToString() ?? "NULL";

            case UnaryExpression u:
                if (u.NodeType == ExpressionType.Convert)
                {
                    return ToSqlExpression(u.Operand);
                }
                if (u.NodeType == ExpressionType.Not)
                {
                    return $"NOT ({ToSqlExpression(u.Operand)})";
                }
                throw new NotSupportedException($"Unary operator not supported: {u.NodeType}");

            case MethodCallExpression mce:
                // Supporte quelques méthodes courantes (ToLower, ToUpper, Trim, etc.)
                var objSql = mce.Object != null ? ToSqlExpression(mce.Object) : null;
                var argsSql = mce.Arguments.Select(ToSqlExpression).ToArray();
                switch (mce.Method.Name)
                {
                    case "ToLower":
                        return $"LOWER({objSql})";
                    case "ToUpper":
                        return $"UPPER({objSql})";
                    case "Trim":
                        return $"BTRIM({objSql})";
                    case "Contains" when mce is { Object: not null, Arguments.Count: 1 }:
                        return $"{objSql} LIKE '%' || {argsSql[0]} || '%'";
                    case "StartsWith" when mce is { Object: not null, Arguments.Count: 1 }:
                        return $"{objSql} LIKE {argsSql[0]} || '%'";
                    case "EndsWith" when mce is { Object: not null, Arguments.Count: 1 }:
                        return $"{objSql} LIKE '%' || {argsSql[0]}";
                    default:
                        throw new NotSupportedException($"Method not supported: {mce.Method.Name}");
                }

            case ParameterExpression p:
                return Escape(p.Name ?? "param");

            default:
                throw new NotSupportedException($"Expression not supported: {expr.NodeType}");
        }
    }

    protected string[] GetColumns<T>(Expression<Func<T, object>> columns)
    {
        return columns.Body switch
        {
            NewExpression newExpression => newExpression.Arguments.OfType<MemberExpression>()
                .Select(m => m.Member.Name)
                .ToArray(),
            MemberExpression memberExpression => [
                memberExpression.Member.Name
            ],
            _ => throw new NotSupportedException("Unsupported expression type")
        };
    }
}
