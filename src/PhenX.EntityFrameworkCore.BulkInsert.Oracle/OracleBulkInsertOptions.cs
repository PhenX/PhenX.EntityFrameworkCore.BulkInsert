using Oracle.ManagedDataAccess.Client;

using PhenX.EntityFrameworkCore.BulkInsert.Options;

namespace PhenX.EntityFrameworkCore.BulkInsert.Oracle;

/// <summary>
/// Options specific to Oracle bulk insert.
/// </summary>
public class OracleBulkInsertOptions : BulkInsertOptions
{
    /// <inheritdoc cref="OracleBulkCopyOptions"/>
    public OracleBulkCopyOptions CopyOptions { get; set; } = OracleBulkCopyOptions.Default;

}
