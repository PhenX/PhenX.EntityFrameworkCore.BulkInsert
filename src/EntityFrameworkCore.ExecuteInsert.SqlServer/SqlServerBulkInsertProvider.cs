using System.Data;
using System.Data.Common;
using System.Text;

using EntityFrameworkCore.ExecuteInsert.Helpers;
using EntityFrameworkCore.ExecuteInsert.OnConflict;

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
    protected override string AddTableCopyBulkInsertId => $"ALTER TABLE {{0}} ADD {BulkInsertId} INT IDENTITY PRIMARY KEY;";

    protected override string GetTempTableName(string tableName) => $"#_temp_bulk_insert_{tableName}";

    protected override async Task BulkImport<T>(DbContext context, DbConnection connection, IEnumerable<T> entities, string tableName,
        PropertyAccessor[] properties, CancellationToken ctk)
    {
        await using var t = (SqlTransaction) await connection.BeginTransactionAsync(ctk); // TODO option

        using var bulkCopy = new SqlBulkCopy(connection as SqlConnection, SqlBulkCopyOptions.TableLock, t);
        bulkCopy.DestinationTableName = tableName;
        bulkCopy.BatchSize = 50_000; // TODO option
        bulkCopy.BulkCopyTimeout = 60;

        foreach (var prop in properties)
        {
            bulkCopy.ColumnMappings.Add(prop.Name, DatabaseHelper.GetEscapedColumnName(prop.Name, OpenDelimiter, CloseDelimiter));
        }

        await bulkCopy.WriteToServerAsync(new EnumerableDataReader<T>(entities, properties), ctk);

        await t.CommitAsync(ctk);
    }

    protected override string BuildInsertSelectQuery<T>(string tableName,
        string targetTableName,
        IProperty[] insertedProperties,
        IProperty[] properties,
        BulkInsertOptions options, OnConflictOptions? onConflict = null)
    {
        var insertedColumns = insertedProperties.Select(p => Escape(p.GetColumnName())).ToArray();
        var insertedColumnList = string.Join(", ", insertedColumns);

        var returnedColumns = properties.Select(p => $"INSERTED.{p.GetColumnName()} AS [{p.Name}]");
        var columnList = string.Join(", ", returnedColumns);

        var q = new StringBuilder();

//         if (options.MoveRows)
//         {
//             var deletedColumnList = string.Join(", ", insertedColumns.Select(c => $"DELETED.{c}"));
//
//             q.AppendLine($"""
//                 DELETE FROM {tableName}
//                 OUTPUT {deletedColumnList}
//                 """);
//         }

        q.AppendLine($"INSERT INTO {targetTableName} ({insertedColumnList})");

        if (columnList.Length != 0)
        {
            q.AppendLine($"OUTPUT {columnList}");
        }

        q.AppendLine($"""
            SELECT {insertedColumnList}
            FROM {tableName}
            """);

        // SQL Server ne supporte pas ON CONFLICT DO NOTHING, mais on garde la signature pour homogénéité
        // if (options.OnConflictIgnore) { ... }

        q.AppendLine(";");

        return q.ToString();
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
