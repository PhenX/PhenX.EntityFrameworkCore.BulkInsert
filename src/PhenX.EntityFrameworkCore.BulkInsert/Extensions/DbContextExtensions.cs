using System.Data;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

using PhenX.EntityFrameworkCore.BulkInsert.Abstractions;
using PhenX.EntityFrameworkCore.BulkInsert.Metadata;

namespace PhenX.EntityFrameworkCore.BulkInsert.Extensions;

internal static class DbContextExtensions
{
    public static TableMetadata GetTableInfo<T>(this DbContext context)
    {
        var provider = context.GetService<MetadataProvider>();

        return provider.GetTableInfo<T>(context);
    }

    public static DbContextOptionsBuilder UseProvider<TProvider>(this DbContextOptionsBuilder optionsBuilder)
        where TProvider : class, IBulkInsertProvider
    {
        var extension = optionsBuilder.Options.FindExtension<BulkInsertOptionsExtension<TProvider>>() ?? new BulkInsertOptionsExtension<TProvider>();

        ((IDbContextOptionsBuilderInfrastructure)optionsBuilder).AddOrUpdateExtension(extension);

        ((IDbContextOptionsBuilderInfrastructure)optionsBuilder).AddOrUpdateExtension(
            optionsBuilder.Options.FindExtension<MetadataProviderExtension>() ?? new());

        return optionsBuilder;
    }

    internal static async Task<ConnectionInfo> GetConnection(
            this DbContext context, bool sync, CancellationToken ctk = default)
    {
        var connection = context.Database.GetDbConnection();
        var wasClosed = connection.State == ConnectionState.Closed;

        if (wasClosed)
        {
            if (sync)
            {
                // ReSharper disable once MethodHasAsyncOverloadWithCancellation
                connection.Open();
            }
            else
            {
                await connection.OpenAsync(ctk);
            }
        }

        var wasBegan = true;
        var transaction = context.Database.CurrentTransaction;

        if (transaction == null)
        {
            wasBegan = false;

            if (sync)
            {
                // ReSharper disable once MethodHasAsyncOverloadWithCancellation
                transaction = context.Database.BeginTransaction();
            }
            else
            {
                transaction = await context.Database.BeginTransactionAsync(ctk);
            }
        }

        return new ConnectionInfo(connection, wasClosed, transaction, wasBegan);
    }
}
