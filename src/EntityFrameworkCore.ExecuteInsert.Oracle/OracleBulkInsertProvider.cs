using System.Reflection;
using EntityFrameworkCore.ExecuteInsert.Abstractions;
using Microsoft.EntityFrameworkCore;
using Oracle.ManagedDataAccess.Client;
using System.Data;

namespace EntityFrameworkCore.ExecuteInsert.Oracle;

public class OracleBulkInsertProvider : IBulkInsertProvider
{
    public string OpenDelimiter => "\"";
    public string CloseDelimiter => "\"";

    public async Task BulkInsertAsync<T>(DbContext context, IEnumerable<T> entities, CancellationToken cancellationToken = default) where T : class
    {
        if (entities.TryGetNonEnumeratedCount(out var count) && count == 0)
        {
            return;
        }

        var tableName = GetFullTableName(context, typeof(T));
        var columns = GetProperties(typeof(T)).Select(p => GetDelimitedColumnName(p.Name)).ToArray();
        var columnList = string.Join(", ", columns);

        var connection = (OracleConnection)context.Database.GetDbConnection();
        var wasClosed = connection.State == ConnectionState.Closed;

        if (wasClosed)
        {
            await connection.OpenAsync(cancellationToken);
        }

        await using var transaction = connection.BeginTransaction();
        await using var command = new OracleCommand();
        command.Connection = connection;
        command.Transaction = transaction;
        command.CommandText = $"INSERT INTO {tableName} ({columnList}) VALUES ({string.Join(", ", Enumerable.Repeat("?", columns.Length))})";

        foreach (var entity in entities)
        {
            command.Parameters.Clear();
            foreach (var value in GetPropertyValues(entity))
            {
                command.Parameters.Add(new OracleParameter { Value = value ?? DBNull.Value });
            }

            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);

        if (wasClosed)
        {
            await connection.CloseAsync();
        }
    }

    private string GetFullTableName(DbContext context, Type entityType)
    {
        var entityTypeInfo = context.Model.FindEntityType(entityType);
        var schema = entityTypeInfo.GetSchema();
        var tableName = entityTypeInfo.GetTableName();
        return schema != null ? $"\"{schema}\".\"{tableName}\"" : $"\"{tableName}\""; // Oracle uses double quotes for delimiters
    }

    private string GetDelimitedColumnName(string columnName)
    {
        return $"\"{columnName}\""; // Oracle uses double quotes for column name delimiters
    }

    private IEnumerable<object> GetPropertyValues<T>(T entity)
    {
        return GetProperties(typeof(T)).Select(p => p.GetValue(entity));
    }

    private IEnumerable<PropertyInfo> GetProperties(Type type)
    {
        return type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead && p.GetMethod.IsPublic);
    }
}
