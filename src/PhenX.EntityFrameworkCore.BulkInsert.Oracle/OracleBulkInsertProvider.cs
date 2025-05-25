using JetBrains.Annotations;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using Oracle.ManagedDataAccess.Client;

using PhenX.EntityFrameworkCore.BulkInsert.Metadata;

namespace PhenX.EntityFrameworkCore.BulkInsert.Oracle;

[UsedImplicitly]
internal class OracleBulkInsertProvider(ILogger<OracleBulkInsertProvider>? logger) : BulkInsertProviderBase<OracleDialectBuilder, OracleBulkInsertOptions>(logger)
{
    //language=sql
    /// <inheritdoc />
    protected override string AddTableCopyBulkInsertId => $"ALTER TABLE {{0}} ADD {BulkInsertId} INT IDENTITY PRIMARY KEY;";

    /// <inheritdoc />
    protected override string GetTempTableName(string tableName) => $"#_temp_bulk_insert_{tableName}";

    protected override OracleBulkInsertOptions CreateDefaultOptions() => new()
    {
        BatchSize = 50_000,
        Converters = [OracleGeometryConverter.Instance]
    };

    /// <inheritdoc />
    protected override Task BulkInsert<T>(
        bool sync,
        DbContext context,
        TableMetadata tableInfo,
        IEnumerable<T> entities,
        string tableName,
        IReadOnlyList<ColumnMetadata> columns,
        OracleBulkInsertOptions options,
        CancellationToken ctk)
    {
        var connection = (OracleConnection) context.Database.GetDbConnection();

        using var bulkCopy = new OracleBulkCopy(connection, options.CopyOptions);

        bulkCopy.DestinationTableName = tableName;
        bulkCopy.BatchSize = options.BatchSize;
        bulkCopy.BulkCopyTimeout = options.GetCopyTimeoutInSeconds();

        foreach (var column in columns)
        {
            bulkCopy.ColumnMappings.Add(column.PropertyName, column.QuotedColumName);
        }

        var dataReader = new EnumerableDataReader<T>(entities, columns, options.Converters);

        bulkCopy.WriteToServer(dataReader);

        return Task.CompletedTask;
    }
}
