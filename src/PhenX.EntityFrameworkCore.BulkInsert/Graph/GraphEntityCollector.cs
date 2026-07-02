using System.Collections;

using Microsoft.EntityFrameworkCore.Metadata;

using PhenX.EntityFrameworkCore.BulkInsert.Metadata;
using PhenX.EntityFrameworkCore.BulkInsert.Options;

namespace PhenX.EntityFrameworkCore.BulkInsert.Graph;

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
    private readonly List<object> _rootEntities;

    public GraphEntityCollector(GraphMetadata graphMetadata, BulkInsertOptions options)
    {
        _options = options;
        _graphMetadata = graphMetadata;
        // Use ReferenceEqualityComparer to track visited entity instances by reference,
        // not by property values, to correctly handle cycles in the object graph
        _visited = new HashSet<object>(ReferenceEqualityComparer.Instance);
        _entitiesByType = [];
        _joinRecords = [];
        _rootEntities = [];
    }

    /// <summary>
    /// Traverses the entity graph and returns entities grouped by type in insertion order.
    /// </summary>
    public GraphCollectionResult Collect<T>(IEnumerable<T> rootEntities) where T : class
    {
        foreach (var entity in rootEntities)
        {
            if (entity == null)
            {
                continue;
            }

            // Capture the actual root instances so the caller can return them without a type-keyed
            // lookup (which would miss TPH-derived roots and lazy-loading proxy types).
            _rootEntities.Add(entity);
            CollectEntity(entity);
        }

        var insertionOrder = _graphMetadata.GetInsertionOrder(_entitiesByType.Keys);

        return new GraphCollectionResult
        {
            EntitiesByType = _entitiesByType,
            InsertionOrder = insertionOrder,
            JoinRecords = _joinRecords,
            RootEntities = _rootEntities,
        };
    }

    private void CollectEntity(object rootEntity)
    {
        // Iterative traversal with an explicit stack to avoid StackOverflowException on deep graphs
        // (e.g. long self-referencing parent chains).
        var stack = new Stack<(object entity, int depth)>();
        stack.Push((rootEntity, 0));

        while (stack.Count > 0)
        {
            var (entity, depth) = stack.Pop();

            // Check max depth before marking as visited to avoid permanently
            // excluding entities that might be reachable at a valid depth later.
            if (_options.MaxGraphDepth > 0 && depth > _options.MaxGraphDepth)
            {
                continue;
            }

            if (!_visited.Add(entity))
            {
                // Already visited
                continue;
            }

            var efEntityType = _graphMetadata.GetEntityType(entity.GetType());

            if (efEntityType == null)
            {
                // Not a known entity type
                continue;
            }

            // Opt-in filter: treat filtered entities as already persisted. They are not inserted and
            // their navigations are not traversed, but their PK values remain available for FK
            // propagation on dependent entities that were already collected.
            if (_options.GraphEntityFilter != null && !_options.GraphEntityFilter(entity))
            {
                continue;
            }

            // Group under the resolved model CLR type so proxies and POCOs of the same entity share
            // one bulk insert.
            var modelType = efEntityType.ClrType;

            if (!_entitiesByType.TryGetValue(modelType, out var entities))
            {
                entities = [];
                _entitiesByType[modelType] = entities;
            }

            entities.Add(entity);

            // Traverse navigation properties
            var navigations = _graphMetadata.GetNavigations(modelType);

            foreach (var navigation in navigations)
            {
                var value = navigation.GetValue(entity);
                if (value == null)
                {
                    continue;
                }

                if (navigation.IsCollection)
                {
                    if (value is not IEnumerable collection)
                    {
                        continue;
                    }

                    foreach (var item in collection)
                    {
                        if (item == null)
                        {
                            continue;
                        }

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

                        stack.Push((item, depth + 1));
                    }
                }
                else
                {
                    // For reference navigations (one-to-one), set the inverse navigation
                    SetInverseNavigation(entity, value, navigation);
                    stack.Push((value, depth + 1));
                }
            }
        }
    }

    private static void SetInverseNavigation(object parentEntity, object childEntity, NavigationMetadata navigation)
    {
        // For one-to-many navigations, set the inverse navigation property
        // (e.g., if Blog.Posts is the navigation, set Post.Blog = blog)
        if (!navigation.HasInverseSetter)
        {
            return;
        }

        // Only set inverse navigations that are reference properties (not collections).
        // If the inverse is a collection, the parent should be added to the collection,
        // not assigned directly (which would cause an invalid cast).
        if (navigation.Navigation is INavigation nav && nav.Inverse?.IsCollection == true)
        {
            // Skip: inverse is a collection, not a reference property
            return;
        }

        // Check if the inverse navigation is already set
        var currentValue = navigation.GetInverseValue(childEntity);
        if (currentValue == null)
        {
            navigation.SetInverseValue(childEntity, parentEntity);
        }
    }
}
