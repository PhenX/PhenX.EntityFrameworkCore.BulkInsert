using System.Data;
using System.Data.Common;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage;

namespace PhenX.EntityFrameworkCore.BulkInsert.Extensions;

internal static class DbContextExtensions
{
    /// <summary>
    /// Gets properties for an entity type
    /// </summary>
    private static IProperty[] GetSimpleProperties(this DbContext context, Type entityType, bool includeGenerated = true)
    {
        var entityTypeInfo = context.Model.FindEntityType(entityType) ?? throw new InvalidOperationException($"Could not determine entity type for type {entityType.Name}");

        return entityTypeInfo
            .GetProperties()
            .Where(p => !p.IsShadowProperty() && (includeGenerated || p.ValueGenerated != ValueGenerated.OnAdd))
            .ToArray();
    }

    /// <summary>
    /// Gets property accessors from an entity type
    /// </summary>
    internal static PropertyAccessor[] GetPropertyAccessors(this DbContext context, Type entityType, bool includeGenerated = true)
    {
        var simpleProperties = GetSimpleProperties(context, entityType, includeGenerated)
            .Select(p => new PropertyAccessor(p));

        var entityTypeInfo = context.Model.FindEntityType(entityType) ?? throw new InvalidOperationException($"Could not determine entity type for type {entityType.Name}");

        var jsonColumns = entityTypeInfo
            .GetNavigations()
            .Where(n => n.TargetEntityType.IsOwned() && n.TargetEntityType.IsMappedToJson());

        var jsonProperties = jsonColumns.Select(n => new PropertyAccessor(n));

        return simpleProperties.Concat(jsonProperties).ToArray();
    }

    /// <summary>
    /// Gets the DbContext connection and transaction, opening the connection if it is closed and beginning a transaction if one does not exist.
    /// </summary>
    internal static async Task<(DbConnection connection, bool wasClosed, IDbContextTransaction transaction, bool wasBegan)> GetConnection(
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

        return (connection, wasClosed, transaction, wasBegan);
    }
}
