using System.Collections.Concurrent;

using Microsoft.EntityFrameworkCore.Metadata;

namespace PhenX.EntityFrameworkCore.BulkInsert.Metadata;

/// <summary>
/// Metadata for an entity type with optimized property accessors.
/// Getters and setters are computed on demand and cached.
/// </summary>
internal sealed class EntityMetadata
{
    private readonly ConcurrentDictionary<string, Func<object, object?>> _getters = new();
    private readonly ConcurrentDictionary<string, Action<object, object?>> _setters = new();
    private readonly IEntityType _entityType;

    public EntityMetadata(IEntityType entityType)
    {
        _entityType = entityType;
        ClrType = entityType.ClrType;
    }

    public Type ClrType { get; }

    /// <summary>
    /// Gets the value of a property from an entity using an optimized getter.
    /// The getter is created on first access and cached for subsequent calls.
    /// Returns null if the property is not found or is a shadow property.
    /// </summary>
    public object? GetPropertyValue(object entity, string propertyName)
    {
        var getter = _getters.GetOrAdd(propertyName, static (name, entityType) =>
        {
            var property = entityType.FindProperty(name);
            if (property == null || property.IsShadowProperty())
            {
                // Try to find a navigation property
                var navigation = entityType.FindNavigation(name);
                if (navigation?.PropertyInfo != null)
                {
                    return PropertyAccessor.CreateGetter(navigation.PropertyInfo);
                }

                return _ => null;
            }

            var propertyInfo = property.PropertyInfo;
            if (propertyInfo == null)
            {
                return _ => null;
            }

            return PropertyAccessor.CreateGetter(propertyInfo);
        }, _entityType);

        return getter(entity);
    }

    /// <summary>
    /// Sets the value of a property on an entity using an optimized setter.
    /// The setter is created on first access and cached for subsequent calls.
    /// Does nothing if the property is not found, is a shadow property, or is not writable.
    /// </summary>
    public void SetPropertyValue(object entity, string propertyName, object? value)
    {
        var setter = _setters.GetOrAdd(propertyName, static (name, entityType) =>
        {
            var property = entityType.FindProperty(name);
            if (property == null || property.IsShadowProperty())
            {
                return (_, _) => { };
            }

            var propertyInfo = property.PropertyInfo;
            if (propertyInfo == null || !propertyInfo.CanWrite)
            {
                return (_, _) => { };
            }

            return PropertyAccessor.CreateSetter(propertyInfo);
        }, _entityType);

        setter(entity, value);
    }
}


