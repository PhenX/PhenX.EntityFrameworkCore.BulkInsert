using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

using PhenX.EntityFrameworkCore.BulkInsert.Options;

namespace PhenX.EntityFrameworkCore.BulkInsert.Metadata;

/// <summary>
/// Metadata for analyzing entity graph relationships.
/// </summary>
internal sealed class GraphMetadata
{
    private readonly Dictionary<Type, IEntityType> _entityTypes;
    private readonly Dictionary<Type, List<NavigationMetadata>> _navigationsByType;
    private readonly Dictionary<Type, EntityMetadata> _entityMetadataByType = [];
    private readonly BulkInsertOptions _options;

    public GraphMetadata(DbContext context, BulkInsertOptions options)
    {
        _options = options;

        // Filter entity types - exclude keyless entities, owned entities, and entities with null table names
        // Also handle potential duplicates (e.g., shared type entities like Dictionary<string,object> for join tables)
        _entityTypes = [];
        foreach (var entityType in context.Model.GetEntityTypes())
        {
            if (entityType.IsOwned() || 
                entityType.ClrType == null || 
                entityType.GetTableName() == null ||
                entityType.FindPrimaryKey() == null) // Keyless entities have no primary key
            {
                continue;
            }

            // For shared type entities (like many-to-many join tables), only keep the first one
            _entityTypes.TryAdd(entityType.ClrType, entityType);
        }

        _navigationsByType = [];

        foreach (var entityType in _entityTypes.Values)
        {
            var navigations = GetNavigationsForType(entityType);
            _navigationsByType[entityType.ClrType] = navigations;
        }
    }

    /// <summary>
    /// Gets the entity type for a CLR type. Lazy-loading proxies (e.g. Castle) subclass the mapped
    /// entity, so when the direct lookup misses we walk up the base types to resolve the model type,
    /// caching the result for subsequent lookups.
    /// </summary>
    public IEntityType? GetEntityType(Type clrType)
    {
        if (_entityTypes.TryGetValue(clrType, out var entityType))
        {
            return entityType;
        }

        var baseType = clrType.BaseType;
        while (baseType != null && baseType != typeof(object))
        {
            if (_entityTypes.TryGetValue(baseType, out entityType))
            {
                // Map the proxy/derived CLR type to the resolved model type for fast future lookups.
                _entityTypes[clrType] = entityType;
                return entityType;
            }

            baseType = baseType.BaseType;
        }

        return null;
    }

    /// <summary>
    /// Gets the navigations for a CLR type.
    /// </summary>
    public IReadOnlyList<NavigationMetadata> GetNavigations(Type clrType)
    {
        return _navigationsByType.TryGetValue(clrType, out var navigations)
            ? navigations
            : [];
    }

    /// <summary>
    /// Gets or creates the entity metadata with optimized property accessors for a CLR type.
    /// </summary>
    public EntityMetadata? GetEntityMetadata(Type clrType)
    {
        if (_entityMetadataByType.TryGetValue(clrType, out var metadata))
        {
            return metadata;
        }

        var entityType = GetEntityType(clrType);
        if (entityType == null)
        {
            return null;
        }

        metadata = new EntityMetadata(entityType);
        _entityMetadataByType[clrType] = metadata;
        return metadata;
    }

    /// <summary>
    /// Determines the topological insertion order for a set of types based on FK dependencies.
    /// Parents are inserted before children to satisfy FK constraints.
    /// </summary>
    public IReadOnlyList<Type> GetInsertionOrder(IEnumerable<Type> typesToInsert)
    {
        var types = typesToInsert.ToHashSet();
        var result = new List<Type>();
        var visited = new HashSet<Type>();
        var visiting = new HashSet<Type>();

        foreach (var type in types)
        {
            TopologicalSort(type, types, visited, visiting, result);
        }

        return result;
    }

    private void TopologicalSort(
        Type type,
        HashSet<Type> validTypes,
        HashSet<Type> visited,
        HashSet<Type> visiting,
        List<Type> result)
    {
        if (visited.Contains(type))
        {
            return;
        }

        if (!visiting.Add(type))
        {
            // Reached via a back edge from a caller that already checked the FK requiredness below.
            return;
        }

        // Get dependencies (types that this type references via FKs)
        var navigations = GetNavigations(type);
        foreach (var nav in navigations.Where(n => n.IsDependentToPrincipal && validTypes.Contains(n.TargetType) && n.TargetType != type))
        {
            if (visiting.Contains(nav.TargetType))
            {
                // Back edge => dependency cycle between entity types. If the foreign key is required,
                // there is no valid insertion order (each side needs the other inserted first), so fail
                // loudly. Optional-FK cycles can proceed (the FK is simply left NULL for now).
                if (nav.ForeignKey?.IsRequired == true)
                {
                    throw new NotSupportedException(
                        $"IncludeGraph detected a required foreign-key dependency cycle between entity types " +
                        $"'{type.Name}' and '{nav.TargetType.Name}'. There is no valid insertion order; break the " +
                        $"cycle by making one of the foreign keys optional or by excluding a navigation.");
                }

                continue;
            }

            TopologicalSort(nav.TargetType, validTypes, visited, visiting, result);
        }

        visiting.Remove(type);
        visited.Add(type);
        result.Add(type);
    }

    private List<NavigationMetadata> GetNavigationsForType(IEntityType entityType)
    {
        var navigations = new List<NavigationMetadata>();

        // Get regular navigations
        foreach (var navigation in entityType.GetNavigations())
        {
            if (!ShouldIncludeNavigation(navigation.Name))
            {
                continue;
            }

            navigations.Add(new NavigationMetadata(navigation));
        }

        // Get skip navigations (many-to-many)
        foreach (var skipNavigation in entityType.GetSkipNavigations())
        {
            if (!ShouldIncludeNavigation(skipNavigation.Name))
            {
                continue;
            }

            navigations.Add(new NavigationMetadata(skipNavigation));
        }

        return navigations;
    }

    private bool ShouldIncludeNavigation(string name)
    {
        if (_options.ExcludeNavigations?.Contains(name) == true)
        {
            return false;
        }

        if (_options.IncludeNavigations?.Count > 0)
        {
            return _options.IncludeNavigations.Contains(name);
        }

        return true;
    }
}
