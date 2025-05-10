using EntityFrameworkCore.ExecuteInsert.Options;

using Microsoft.EntityFrameworkCore;

namespace EntityFrameworkCore.ExecuteInsert.Abstractions;

public interface IBulkInsertProvider
{
    internal Task<List<T>> BulkInsertWithIdentityAsync<T>(
        DbContext context,
        IEnumerable<T> entities,
        BulkInsertOptions options,
        OnConflictOptions? onConflict = null,
        CancellationToken ctk = default
    ) where T : class;

    internal Task BulkInsertWithoutReturnAsync<T>(
        DbContext context,
        IEnumerable<T> entities,
        BulkInsertOptions options,
        OnConflictOptions? onConflict = null,
        CancellationToken ctk = default
    ) where T : class;
}
