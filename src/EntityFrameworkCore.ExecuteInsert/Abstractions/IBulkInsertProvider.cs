using System.Data.Common;

using EntityFrameworkCore.ExecuteInsert;

using Microsoft.EntityFrameworkCore;

public interface IBulkInsertProvider
{
    Task<List<T>> BulkInsertWithIdentityAsync<T>(
        DbContext context,
        IEnumerable<T> entities,
        BulkInsertOptions options,
        CancellationToken ctk = default
    ) where T : class;

    Task BulkInsertWithoutReturnAsync<T>(
        DbContext context,
        IEnumerable<T> entities,
        BulkInsertOptions options,
        CancellationToken ctk = default
    ) where T : class;

    Task<string> CreateTableCopyAsync<T>(DbContext context,
        DbConnection connection,
        CancellationToken cancellationToken = default) where T : class;

    string OpenDelimiter { get; }
    string CloseDelimiter { get; }
}
