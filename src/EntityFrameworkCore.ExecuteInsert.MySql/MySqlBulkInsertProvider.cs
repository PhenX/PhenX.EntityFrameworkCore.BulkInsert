using EntityFrameworkCore.ExecuteInsert.Abstractions;
using EntityFrameworkCore.ExecuteInsert.Helpers;
using Microsoft.EntityFrameworkCore;
using MySql.Data.MySqlClient;
using System.Data;

namespace EntityFrameworkCore.ExecuteInsert.MySql;

public class MySqlBulkInsertProvider : IBulkInsertProvider
{
    public string OpenDelimiter => "`";
    public string CloseDelimiter => "`";

    public async Task BulkInsertAsync<T>(DbContext context, IEnumerable<T> entities, CancellationToken cancellationToken = default) where T : class
    {
        if (entities.TryGetNonEnumeratedCount(out var count) && count == 0)
        {
            return;
        }

        var tableName = DatabaseHelper.GetEscapedTableName(context, typeof(T), OpenDelimiter, CloseDelimiter);
        var columns = DatabaseHelper.GetCachedProperties(typeof(T))
            .Select(p => DatabaseHelper.GetEscapedColumnName(p.Name, OpenDelimiter, CloseDelimiter))
            .ToArray();

        var valuesList = string.Join(", ",
            entities.Select(e => $"({string.Join(", ", GetPropertyValues(e))})"));

        var query = $"INSERT INTO {tableName} ({string.Join(", ", columns)}) VALUES {valuesList}";

        await using var connection = (MySqlConnection)context.Database.GetDbConnection();
        var wasClosed = connection.State == ConnectionState.Closed;

        if (wasClosed)
        {
            await connection.OpenAsync(cancellationToken);
        }

        await using var command = new MySqlCommand(query, connection);
        await command.ExecuteNonQueryAsync(cancellationToken);

        if (wasClosed)
        {
            await connection.CloseAsync();
        }
    }

    private IEnumerable<string> GetPropertyValues<T>(T entity)
    {
        return DatabaseHelper.GetCachedProperties(typeof(T)).Select(p =>
        {
            var value = p.GetValue(entity);
            return value == null ? "NULL" : $"'{value.ToString().Replace("'", "''")}'";
        });
    }
}
