using System.Data.Common;

using EntityFrameworkCore.ExecuteInsert.OnConflict;

using Microsoft.EntityFrameworkCore;

namespace EntityFrameworkCore.ExecuteInsert.Abstractions;

public interface IBulkInsertProvider
{
    Task<List<T>> BulkInsertWithIdentityAsync<T>(
        DbContext context,
        IEnumerable<T> entities,
        BulkInsertOptions options,
        OnConflictOptions? onConflict = null,
        CancellationToken ctk = default
    ) where T : class;

    Task BulkInsertWithoutReturnAsync<T>(
        DbContext context,
        IEnumerable<T> entities,
        BulkInsertOptions options,
        OnConflictOptions? onConflict = null,
        CancellationToken ctk = default
    ) where T : class;

    string OpenDelimiter { get; }
    string CloseDelimiter { get; }
}
