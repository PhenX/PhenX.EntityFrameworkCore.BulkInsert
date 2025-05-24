using PhenX.EntityFrameworkCore.BulkInsert.Options;

namespace PhenX.EntityFrameworkCore.BulkInsert.PostgreSql;

/// <summary>
/// Options specific to SQL Server bulk insert.
/// </summary>
public class PostgreSqlBulkInsertOptions : BulkInsertOptions
{
    /// <summary>
    /// A list of type providers.
    /// </summary>
    public List<IPostgresTypeProvider>? TypeProviders { get; set; }
}
