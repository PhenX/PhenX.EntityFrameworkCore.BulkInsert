using Microsoft.EntityFrameworkCore;

using PhenX.EntityFrameworkCore.BulkInsert.Options;

namespace PhenX.EntityFrameworkCore.BulkInsert.Abstractions;

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
