using System.Data.Common;

using Microsoft.EntityFrameworkCore;

namespace EntityFrameworkCore.ExecuteInsert.Abstractions;

public interface IBulkInsertProvider
{
    Task BulkInsertAsync<T>(DbContext context, IEnumerable<T> entities, BulkInsertOptions? options = null,
        CancellationToken cancellationToken = default) where T : class;

    Task<string> CreateTableCopyAsync<T>(DbConnection connection, DbContext context,
        CancellationToken cancellationToken = default) where T : class;

    string OpenDelimiter { get; }
    string CloseDelimiter { get; }
}
