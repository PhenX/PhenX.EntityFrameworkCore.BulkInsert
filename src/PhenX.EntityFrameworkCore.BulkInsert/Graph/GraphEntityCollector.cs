using System.Collections;
using System.Reflection;

using Microsoft.EntityFrameworkCore;

using PhenX.EntityFrameworkCore.BulkInsert.Metadata;
using PhenX.EntityFrameworkCore.BulkInsert.Options;

namespace PhenX.EntityFrameworkCore.BulkInsert.Graph;

/// <summary>
/// Result of collecting entities from an object graph.
/// </summary>
internal sealed class GraphCollectionResult
{
    /// <summary>
    /// Entities grouped by type.
    /// </summary>
    public required Dictionary<Type, List<object>> EntitiesByType { get; init; }

    /// <summary>
    /// Types in topological insertion order (parents before children).
    /// </summary>
    public required IReadOnlyList<Type> InsertionOrder { get; init; }

    /// <summary>
    /// Many-to-many join records to insert after both sides are inserted.
    /// </summary>
    public required List<JoinRecord> JoinRecords { get; init; }
}

/// <summary>
/// Represents a join table record for many-to-many relationships.
/// </summary>
internal sealed class JoinRecord
{
    public required Type JoinEntityType { get; init; }
    public required object LeftEntity { get; init; }
    public required object RightEntity { get; init; }
    public required NavigationMetadata Navigation { get; init; }
}

/// <summary>
/// Collects all entities from an object graph for bulk insertion.
/// </summary>
internal sealed class GraphEntityCollector
{
    private readonly GraphMetadata _graphMetadata;
    private readonly BulkInsertOptions _options;
    private readonly HashSet<object> _visited;
    private readonly Dictionary<Type, List<object>> _entitiesByType;
    private readonly List<JoinRecord> _joinRecords;

    public GraphEntityCollector(DbContext context, BulkInsertOptions options)
    {
        _options = options;
        _graphMetadata = new GraphMetadata(context, options);
        // Use ReferenceEqualityComparer to track visited entity instances by reference,
        // not by property values, to correctly handle cycles in the object graph
        _visited = new HashSet<object>(ReferenceEqualityComparer.Instance);
        _entitiesByType = [];
        _joinRecords = [];
    }

    /// <summary>
    /// Traverses the entity graph and returns entities grouped by type in insertion order.
    /// </summary>
    public GraphCollectionResult Collect<T>(IEnumerable<T> rootEntities) where T : class
    {
        foreach (var entity in rootEntities)
        {
            CollectEntity(entity, 0);
        }

        var insertionOrder = _graphMetadata.GetInsertionOrder(_entitiesByType.Keys);

        return new GraphCollectionResult
        {
            EntitiesByType = _entitiesByType,
            InsertionOrder = insertionOrder,
            JoinRecords = _joinRecords,
        };
    }

    private void CollectEntity(object entity, int depth)
    {
        if (entity == null || !_visited.Add(entity))
        {
            // Already visited or null
            return;
        }

        // Check max depth
        if (_options.MaxGraphDepth > 0 && depth > _options.MaxGraphDepth)
        {
            return;
        }

        var entityType = entity.GetType();
        var efEntityType = _graphMetadata.GetEntityType(entityType);

        if (efEntityType == null)
        {
            // Not a known entity type
            return;
        }

        // Add to collection
        if (!_entitiesByType.TryGetValue(entityType, out var entities))
        {
            entities = [];
            _entitiesByType[entityType] = entities;
        }

        entities.Add(entity);

        // Traverse navigation properties
        var navigations = _graphMetadata.GetNavigations(entityType);

        foreach (var navigation in navigations)
        {
            var propertyInfo = entityType.GetProperty(navigation.PropertyName, BindingFlags.Public | BindingFlags.Instance);
            if (propertyInfo == null)
            {
                continue;
            }

            var value = propertyInfo.GetValue(entity);
            if (value == null)
            {
                continue;
            }

            if (navigation.IsCollection)
            {
                if (value is IEnumerable collection)
                {
                    foreach (var item in collection)
                    {
                        if (item != null)
                        {
                            if (navigation.IsManyToMany)
                            {
                                // Record join table entry
                                _joinRecords.Add(new JoinRecord
                                {
                                    JoinEntityType = navigation.JoinEntityType!.ClrType,
                                    LeftEntity = entity,
                                    RightEntity = item,
                                    Navigation = navigation,
                                });
                            }
                            else
                            {
                                // For one-to-many, set the inverse navigation property
                                // so that FK propagation can find the parent
                                SetInverseNavigation(entity, item, navigation);
                            }

                            CollectEntity(item, depth + 1);
                        }
                    }
                }
            }
            else
            {
                // For reference navigations (one-to-one), set the inverse navigation
                SetInverseNavigation(entity, value, navigation);
                CollectEntity(value, depth + 1);
            }
        }
    }

    private static void SetInverseNavigation(object parentEntity, object childEntity, NavigationMetadata navigation)
    {
        // For one-to-many navigations, find and set the inverse navigation property
        // (e.g., if Blog.Posts is the navigation, set Post.Blog = blog)
        var nav = navigation.Navigation;
        if (nav is not Microsoft.EntityFrameworkCore.Metadata.INavigation regularNav)
        {
            return;
        }

        var inverse = regularNav.Inverse;
        if (inverse == null)
        {
            return;
        }

        // Set the inverse navigation property on the child
        var inversePropertyInfo = childEntity.GetType().GetProperty(inverse.Name, BindingFlags.Public | BindingFlags.Instance);
        if (inversePropertyInfo != null && inversePropertyInfo.CanWrite)
        {
            var currentValue = inversePropertyInfo.GetValue(childEntity);
            if (currentValue == null)
            {
                inversePropertyInfo.SetValue(childEntity, parentEntity);
            }
        }
    }
}
