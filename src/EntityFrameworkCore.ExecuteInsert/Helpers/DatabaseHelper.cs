using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace EntityFrameworkCore.ExecuteInsert.Helpers;

public static class DatabaseHelper
{
    // Reflection cache to store property metadata for each entity type
    private static readonly ConcurrentDictionary<Type, IProperty[]?> PropertyCache = new();

    /// <summary>
    /// Gets cached properties for an entity type, using reflection if not already cached.
    /// </summary>
    public static IProperty[] GetProperties(DbContext context, Type entityType, bool includeGenerated = true)
    {
        var entityTypeInfo = context.Model.FindEntityType(entityType) ?? throw new InvalidOperationException($"Could not determine entity type for type {entityType.Name}");

        return entityTypeInfo
            .GetProperties()
            .Where(p => !p.IsShadowProperty() && (includeGenerated || p.ValueGenerated != ValueGenerated.OnAdd))
            .ToArray();
    }

    public static INavigation[] GetCollectionNavigationProperties(DbContext context, Type getType)
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

    public static INavigation[] GetNavigationProperties(DbContext context, Type getType)
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
}
