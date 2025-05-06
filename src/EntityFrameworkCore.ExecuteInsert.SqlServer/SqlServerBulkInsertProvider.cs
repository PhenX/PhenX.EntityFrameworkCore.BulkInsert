using System.Data;
using System.Data.Common;

using EntityFrameworkCore.ExecuteInsert.Helpers;

using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace EntityFrameworkCore.ExecuteInsert.SqlServer;

public class SqlServerBulkInsertProvider : BulkInsertProviderBase
{
    public override string OpenDelimiter => "[";
    public override string CloseDelimiter => "]";

    //language=sql
    protected override string CreateTableCopySql => "SELECT {2} INTO {0} FROM {1} WHERE 1 = 0;";

    //language=sql
    protected override string AddTableCopyBulkInsertId => "ALTER TABLE {0} ADD _bulk_insert_id INT IDENTITY PRIMARY KEY;";

    protected override string GetTempTableName<T>(string tableName) where T : class
    {
        return $"#_temp_bulk_insert_{tableName}";
    }

    protected override async Task BulkImport<T>(DbContext context, DbConnection connection, IEnumerable<T> entities, string tableName,
        PropertyAccessor[] properties, CancellationToken ctk)
    {
        using var bulkCopy = new SqlBulkCopy(connection as SqlConnection);
        bulkCopy.DestinationTableName = tableName;
        bulkCopy.BatchSize = 10_000;
        bulkCopy.BulkCopyTimeout = 60;

        foreach (var prop in properties)
        {
            bulkCopy.ColumnMappings.Add(prop.Name, DatabaseHelper.GetEscapedColumnName(prop.Name, OpenDelimiter, CloseDelimiter));
        }

        await bulkCopy.WriteToServerAsync(new EnumerableDataReader<T>(entities, properties), ctk);
    }

    protected override string BuildInsertSelectQuery(string tempTableName, string targetTableName,
        IProperty[] insertedProperties, IProperty[] properties, bool moveRows)
    {
        var insertedColumns = insertedProperties.Select(p => Escape(p.GetColumnName()));
        var insertedColumnList = string.Join(", ", insertedColumns);
        var columnList = string.Join(", ", properties.Select(p => $"INSERTED.{p.GetColumnName()}"));

//         if (moveRows)
//         {
//             //language=sql
//             return $"""
//                     WITH moved_rows AS (
//                        DELETE FROM {tempTableName}
//                            OUTPUT {insertedColumnList}
//                     )
//                     INSERT INTO {targetTableName} ({insertedColumnList})
//                     SELECT {insertedColumnList}
//                     FROM moved_rows
//                         RETURNING {columnList};
//                     """;
//         }

        //language=sql
        return $"""
                INSERT INTO {targetTableName} ({insertedColumnList})
                OUTPUT {columnList}
                SELECT {insertedColumnList}
                FROM {tempTableName};
                """;
    }

    private DataTable ConvertToDataTable<T>(PropertyAccessor[] properties)
    {
        var dataTable = new DataTable(typeof(T).Name);

        if (properties.Length == 0)
        {
            throw new InvalidOperationException($"No properties found for type {typeof(T).Name}");
        }

        foreach (var prop in properties)
        {
            dataTable.Columns.Add(prop.Name, prop.ProviderClrType);
        }

        return dataTable;
    }
}
