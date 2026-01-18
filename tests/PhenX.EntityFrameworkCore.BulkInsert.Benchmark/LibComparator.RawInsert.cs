using System.Data;

using Microsoft.Data.SqlClient;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

#if MYSQL_SUPPORTED
using MySqlConnector;
#endif

using Npgsql;

using Oracle.ManagedDataAccess.Client;

namespace PhenX.EntityFrameworkCore.BulkInsert.Benchmark;

public abstract partial class LibComparator
{
    private void RawInsertPostgreSql()
    {
        using var connection = (NpgsqlConnection)DbContext.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
        {
            connection.Open();
        }

        const string copyCommand = $"""
                                    COPY "{nameof(TestEntity)}" (
                                        "Name",
                                        "Price",
                                        "Identifier",
                                        "CreatedAt",
                                        "UpdatedAt",
                                        "NumericEnumValue"
                                    ) FROM STDIN (FORMAT BINARY)
                                    """;

        using var writer = connection.BeginBinaryImport(copyCommand);
        foreach (var entity in data)
        {
            writer.StartRow();
            writer.Write(entity.Name);
            writer.Write(entity.Price);
            writer.Write(entity.Identifier);
            writer.Write(entity.CreatedAt);
            writer.Write(entity.UpdatedAt);
            writer.Write((int)entity.NumericEnumValue);
        }

        writer.Complete();
    }

    private void RawInsertSqlite()
    {
        var connection = (SqliteConnection)DbContext.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
        {
            connection.Open();
        }

        using var transaction = connection.BeginTransaction();
        using var command = connection.CreateCommand();
        command.CommandText = $"""
                               INSERT INTO "{nameof(TestEntity)}" (
                                   "Name",
                                   "Price",
                                   "Identifier",
                                   "CreatedAt",
                                   "UpdatedAt",
                                   "NumericEnumValue"
                               ) VALUES (@Name, @Price, @Identifier, @CreatedAt, @UpdatedAt, @NumericEnumValue)
                               """;

        command.Parameters.Add(new SqliteParameter("@Name", DbType.String));
        command.Parameters.Add(new SqliteParameter("@Price", DbType.Decimal));
        command.Parameters.Add(new SqliteParameter("@Identifier", DbType.Guid));
        command.Parameters.Add(new SqliteParameter("@CreatedAt", DbType.DateTime));
        command.Parameters.Add(new SqliteParameter("@UpdatedAt", DbType.DateTime2));
        command.Parameters.Add(new SqliteParameter("@NumericEnumValue", DbType.Int32));

        foreach (var entity in data)
        {
            command.Parameters["@Name"].Value = entity.Name;
            command.Parameters["@Price"].Value = entity.Price;
            command.Parameters["@Identifier"].Value = entity.Identifier;
            command.Parameters["@CreatedAt"].Value = entity.CreatedAt;
            command.Parameters["@UpdatedAt"].Value = entity.UpdatedAt;
            command.Parameters["@NumericEnumValue"].Value = (int)entity.NumericEnumValue;

            command.ExecuteNonQuery();
        }

        transaction.Commit();
    }

    private void RawInsertSqlServer()
    {
        var connection = (SqlConnection)DbContext.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
        {
            connection.Open();
        }

        using var bulkCopy = new SqlBulkCopy(connection);

        bulkCopy.DestinationTableName = nameof(TestEntity);
        bulkCopy.BatchSize = 50_000;
        bulkCopy.BulkCopyTimeout = 60;

        bulkCopy.ColumnMappings.Add("Name", "Name");
        bulkCopy.ColumnMappings.Add("Price", "Price");
        bulkCopy.ColumnMappings.Add("Identifier", "Identifier");
        bulkCopy.ColumnMappings.Add("CreatedAt", "CreatedAt");
        bulkCopy.ColumnMappings.Add("UpdatedAt", "UpdatedAt");
        bulkCopy.ColumnMappings.Add("NumericEnumValue", "NumericEnumValue");

        var dataTable = new DataTable();
        dataTable.Columns.Add("Name", typeof(string));
        dataTable.Columns.Add("Price", typeof(decimal));
        dataTable.Columns.Add("Identifier", typeof(Guid));
        dataTable.Columns.Add("CreatedAt", typeof(DateTime));
        dataTable.Columns.Add("UpdatedAt", typeof(DateTimeOffset));
        dataTable.Columns.Add("NumericEnumValue", typeof(int));

        foreach (var entity in data)
        {
            var row = dataTable.NewRow();
            row["Name"] = entity.Name;
            row["Price"] = entity.Price;
            row["Identifier"] = entity.Identifier;
            row["CreatedAt"] = entity.CreatedAt;
            row["UpdatedAt"] = entity.UpdatedAt;
            row["NumericEnumValue"] = (int)entity.NumericEnumValue;
            dataTable.Rows.Add(row);

            if (dataTable.Rows.Count >= 50_000)
            {
                bulkCopy.WriteToServer(dataTable);
                dataTable.Clear();
            }
        }

        if (dataTable.Rows.Count > 0)
        {
            bulkCopy.WriteToServer(dataTable);
        }
    }

#if MYSQL_SUPPORTED
    private void RawInsertMySql()
    {
        var connection = (MySqlConnection)DbContext.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
        {
            connection.Open();
        }

        var bulkCopy = new MySqlBulkCopy(connection);

        bulkCopy.DestinationTableName = nameof(TestEntity);
        bulkCopy.BulkCopyTimeout = 60;

        var i = 0;
        bulkCopy.ColumnMappings.Add(new MySqlBulkCopyColumnMapping(i++, "Name"));
        bulkCopy.ColumnMappings.Add(new MySqlBulkCopyColumnMapping(i++, "Price"));
        bulkCopy.ColumnMappings.Add(new MySqlBulkCopyColumnMapping(i++, "Identifier"));
        bulkCopy.ColumnMappings.Add(new MySqlBulkCopyColumnMapping(i++, "CreatedAt"));
        bulkCopy.ColumnMappings.Add(new MySqlBulkCopyColumnMapping(i++, "UpdatedAt"));
        bulkCopy.ColumnMappings.Add(new MySqlBulkCopyColumnMapping(i++, "NumericEnumValue"));

        var dataTable = new DataTable();
        dataTable.Columns.Add("Name", typeof(string));
        dataTable.Columns.Add("Price", typeof(decimal));
        dataTable.Columns.Add("Identifier", typeof(Guid));
        dataTable.Columns.Add("CreatedAt", typeof(DateTime));
        dataTable.Columns.Add("UpdatedAt", typeof(DateTimeOffset));
        dataTable.Columns.Add("NumericEnumValue", typeof(int));

        foreach (var entity in data)
        {
            var row = dataTable.NewRow();
            row["Name"] = entity.Name;
            row["Price"] = entity.Price;
            row["Identifier"] = entity.Identifier;
            row["CreatedAt"] = entity.CreatedAt;
            row["UpdatedAt"] = entity.UpdatedAt;
            row["NumericEnumValue"] = (int)entity.NumericEnumValue;
            dataTable.Rows.Add(row);

            if (dataTable.Rows.Count >= 50_000)
            {
                bulkCopy.WriteToServer(dataTable);
                dataTable.Clear();
            }
        }

        if (dataTable.Rows.Count > 0)
        {
            bulkCopy.WriteToServer(dataTable);
        }
    }
#endif

    private void RawInsertOracle()
    {
        var connection = (OracleConnection)DbContext.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
        {
            connection.Open();
        }

        using var bulkCopy = new OracleBulkCopy(connection);

        bulkCopy.DestinationTableName = "\"" + nameof(TestEntity) + "\"";
        bulkCopy.BatchSize = 50_000;
        bulkCopy.BulkCopyTimeout = 60;

        bulkCopy.ColumnMappings.Add("Name", "\"Name\"");
        bulkCopy.ColumnMappings.Add("Price", "\"Price\"");
        bulkCopy.ColumnMappings.Add("Identifier", "\"Identifier\"");
        bulkCopy.ColumnMappings.Add("CreatedAt", "\"CreatedAt\"");
        bulkCopy.ColumnMappings.Add("UpdatedAt", "\"UpdatedAt\"");
        bulkCopy.ColumnMappings.Add("NumericEnumValue", "\"NumericEnumValue\"");

        var dataTable = new DataTable();
        dataTable.Columns.Add("Name", typeof(string));
        dataTable.Columns.Add("Price", typeof(decimal));
        dataTable.Columns.Add("Identifier", typeof(Guid));
        dataTable.Columns.Add("CreatedAt", typeof(DateTime));
        dataTable.Columns.Add("UpdatedAt", typeof(DateTimeOffset));
        dataTable.Columns.Add("NumericEnumValue", typeof(int));

        foreach (var entity in data)
        {
            var row = dataTable.NewRow();
            row["Name"] = entity.Name;
            row["Price"] = entity.Price;
            row["Identifier"] = entity.Identifier;
            row["CreatedAt"] = entity.CreatedAt;
            row["UpdatedAt"] = entity.UpdatedAt;
            row["NumericEnumValue"] = (int)entity.NumericEnumValue;
            dataTable.Rows.Add(row);

            if (dataTable.Rows.Count >= 50_000)
            {
                bulkCopy.WriteToServer(dataTable);
                dataTable.Clear();
            }
        }

        if (dataTable.Rows.Count > 0)
        {
            bulkCopy.WriteToServer(dataTable);
        }
    }
}
