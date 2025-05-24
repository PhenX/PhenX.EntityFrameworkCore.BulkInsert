using System.Data.Common;

using Microsoft.EntityFrameworkCore.Storage;

namespace PhenX.EntityFrameworkCore.BulkInsert.Metadata;

internal readonly record struct ConnectionInfo(DbConnection Connection, bool WasClosed, IDbContextTransaction Transaction, bool WasBegan)
{
    public async Task Commit(bool sync, CancellationToken ctk)
    {
        if (!WasBegan)
        {
            if (sync)
            {
                // ReSharper disable once MethodHasAsyncOverloadWithCancellation
                Transaction.Commit();
            }
            else
            {
                await Transaction.CommitAsync(ctk);
            }
        }
    }

    public async Task Close(bool sync, CancellationToken ctk)
    {
        if (!WasBegan)
        {
            if (sync)
            {
                // ReSharper disable once MethodHasAsyncOverloadWithCancellation
                Transaction.Dispose();
            }
            else
            {
                await Transaction.DisposeAsync();
            }
        }

        if (WasClosed)
        {
            if (sync)
            {
                // ReSharper disable once MethodHasAsyncOverload
                Connection.Close();
            }
            else
            {
                await Connection.CloseAsync();
            }
        }
    }
}
