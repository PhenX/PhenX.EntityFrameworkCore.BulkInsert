using System.Collections;
using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace EntityFrameworkCore.ExecuteInsert.Helpers;

public static class DatabaseHelper
{
    // Reflection cache to store property metadata for each entity type
    private static readonly ConcurrentDictionary<Type, IProperty[]?> PropertyCache = new();

    /// <summary>
    /// Escapes a schema and table name using database-specific delimiters.
    /// </summary>
    public static string GetEscapedTableName(string? schema, string tableName, string openDelimiter, string closeDelimiter)
    {
        return schema != null
            ? $"{openDelimiter}{schema}{closeDelimiter}.{openDelimiter}{tableName}{closeDelimiter}"
            : $"{openDelimiter}{tableName}{closeDelimiter}";
    }

    /// <summary>
    /// Escapes a schema and table name using database-specific delimiters.
    /// </summary>
    public static string GetEscapedTableName(DbContext context, Type entityType, string openDelimiter, string closeDelimiter)
    {
        var (schema, tableName, _) = GetTableInfo(context, entityType);

        return schema != null
            ? $"{openDelimiter}{schema}{closeDelimiter}.{openDelimiter}{tableName}{closeDelimiter}"
            : $"{openDelimiter}{tableName}{closeDelimiter}";
    }

    /// <summary>
    /// Escapes a schema and table name using database-specific delimiters.
    /// </summary>
    public static (string? SchemaName, string TableName, IKey PrimaryKey) GetTableInfo(DbContext context, Type entityType)
    {
        var entityTypeInfo = context.Model.FindEntityType(entityType);
        var schema = (entityTypeInfo ?? throw new InvalidOperationException($"Could not determine entity type for type {entityType.Name}")).GetSchema();
        var tableName = entityTypeInfo.GetTableName();

        if (string.IsNullOrWhiteSpace(tableName))
        {
            throw new InvalidOperationException($"Could not determine table name for type {entityType.Name}");
        }

        return (schema, tableName, entityTypeInfo.FindPrimaryKey()!);
    }

    /// <summary>
    /// Escapes a column name using database-specific delimiters.
    /// </summary>
    public static string GetEscapedColumnName(string columnName, string openDelimiter, string closeDelimiter)
    {
        return $"{openDelimiter}{columnName}{closeDelimiter}";
    }

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
