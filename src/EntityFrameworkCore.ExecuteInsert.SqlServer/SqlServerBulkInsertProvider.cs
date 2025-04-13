using System.Data;
using EntityFrameworkCore.ExecuteInsert.Abstractions;
using EntityFrameworkCore.ExecuteInsert.Helpers;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace EntityFrameworkCore.ExecuteInsert.SqlServer;

public class SqlServerBulkInsertProvider : IBulkInsertProvider
{
    public string OpenDelimiter => "[";
    public string CloseDelimiter => "]";

    public async Task BulkInsertAsync<T>(DbContext context, IEnumerable<T> entities, CancellationToken cancellationToken = default) where T : class
    {
        if (entities.TryGetNonEnumeratedCount(out var count) && count == 0)
        {
            return;
        }

        var table = ConvertToDataTable(context, entities);
        var tableName = DatabaseHelper.GetEscapedTableName(context, typeof(T), OpenDelimiter, CloseDelimiter);

        await using var connection = (SqlConnection)context.Database.GetDbConnection();
        var wasClosed = connection.State == ConnectionState.Closed;

        if (wasClosed)
        {
            await connection.OpenAsync(cancellationToken);
        }

        using var bulkCopy = new SqlBulkCopy(connection);
        bulkCopy.DestinationTableName = tableName;
        bulkCopy.BatchSize = 1000;
        bulkCopy.BulkCopyTimeout = 60;

        foreach (DataColumn column in table.Columns)
        {
            bulkCopy.ColumnMappings.Add(new SqlBulkCopyColumnMapping(column.ColumnName, DatabaseHelper.GetEscapedColumnName(column.ColumnName, OpenDelimiter, CloseDelimiter)));
        }

        await bulkCopy.WriteToServerAsync(table, cancellationToken);

        if (wasClosed)
        {
            await connection.CloseAsync();
        }
    }

    private DataTable ConvertToDataTable<T>(DbContext context, IEnumerable<T> entities)
    {
        var dataTable = new DataTable(typeof(T).Name);
        var properties = DatabaseHelper.GetProperties(context, typeof(T));

        if (!properties.Any())
        {
            throw new InvalidOperationException($"No properties found for type {typeof(T).Name}");
        }

        foreach (var prop in properties)
        {
            dataTable.Columns.Add(prop.Name, Nullable.GetUnderlyingType(prop.ClrType) ?? prop.ClrType);
        }

        foreach (var entity in entities)
        {
            var values = properties.Select(p => p.PropertyInfo!.GetValue(entity, null)).ToArray();
            dataTable.Rows.Add(values);
        }

        return dataTable;
    }
}
