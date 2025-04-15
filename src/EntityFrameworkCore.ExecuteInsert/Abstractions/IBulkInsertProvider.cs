using System.Data.Common;

using Microsoft.EntityFrameworkCore;

namespace EntityFrameworkCore.ExecuteInsert.Abstractions;

public interface IBulkInsertProvider
{
    Task<IEnumerable<T>> BulkInsertAsync<T>(
        DbContext context,
        IEnumerable<T> entities,
        BulkInsertOptions options,
        CancellationToken ctk = default
    )
        where T : class;

    Task<string> CreateTableCopyAsync<T>(
        DbConnection connection,
        DbContext context,
        CancellationToken cancellationToken = default
    )
        where T : class;

    string OpenDelimiter { get; }
    string CloseDelimiter { get; }
}
