using System.Collections.Concurrent;
using System.Data;
using System.Data.Common;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace EntityFrameworkCore.ExecuteInsert.Extensions;

public static class DbContextExtensions
{
    // Reflection cache to store property metadata for each entity type
    private static readonly ConcurrentDictionary<Type, IProperty[]?> PropertyCache = new();

    /// <summary>
    /// Gets cached properties for an entity type, using reflection if not already cached.
    /// </summary>
    public static IProperty[] GetProperties(this DbContext context, Type entityType, bool includeGenerated = true)
    {
        var entityTypeInfo = context.Model.FindEntityType(entityType) ?? throw new InvalidOperationException($"Could not determine entity type for type {entityType.Name}");

        return entityTypeInfo
            .GetProperties()
            .Where(p => !p.IsShadowProperty() && (includeGenerated || p.ValueGenerated != ValueGenerated.OnAdd))
            .ToArray();
    }

    public static INavigation[] GetCollectionNavigationProperties(this DbContext context, Type getType)
    {
        var entityType = context.Model.FindEntityType(getType);
        if (entityType == null)
        {
            throw new InvalidOperationException($"Could not determine entity type for type {getType.Name}");
        }

        return entityType
            .GetNavigations()
            .Where(n => n.IsCollection)
            .ToArray();
    }

    public static INavigation[] GetNavigationProperties(this DbContext context, Type getType)
    {
        var entityType = context.Model.FindEntityType(getType);
        if (entityType == null)
        {
            throw new InvalidOperationException($"Could not determine entity type for type {getType.Name}");
        }

        return entityType
            .GetNavigations()
            .Where(n => !n.IsCollection)
            .ToArray();
    }

    public static async Task<(DbConnection connection, bool wasClosed)> GetConnection(this DbContext context, CancellationToken ctk = default)
    {
        var connection = context.Database.GetDbConnection();
        var wasClosed = connection.State == ConnectionState.Closed;

        if (wasClosed)
        {
            await connection.OpenAsync(ctk);
        }

        return (connection, wasClosed);
    }
}
