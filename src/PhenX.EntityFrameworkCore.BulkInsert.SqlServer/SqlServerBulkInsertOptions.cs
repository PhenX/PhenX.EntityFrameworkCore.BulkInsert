using Microsoft.Data.SqlClient;

using PhenX.EntityFrameworkCore.BulkInsert.Options;

namespace PhenX.EntityFrameworkCore.BulkInsert.SqlServer;

/// <summary>
/// Options specific to SQL Server bulk insert.
/// </summary>
public class SqlServerBulkInsertOptions : BulkInsertOptions
{
    /// <inheritdoc cref="SqlBulkCopyOptions"/>
    public SqlBulkCopyOptions CopyOptions { get; set; } = SqlBulkCopyOptions.Default;

    /// <inheritdoc cref="SqlBulkCopy.EnableStreaming"/>
    public bool EnableStreaming { get; set; } = false;

}
