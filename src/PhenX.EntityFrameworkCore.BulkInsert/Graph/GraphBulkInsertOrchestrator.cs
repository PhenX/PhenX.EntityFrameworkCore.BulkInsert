using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Reflection;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

using PhenX.EntityFrameworkCore.BulkInsert.Abstractions;
using PhenX.EntityFrameworkCore.BulkInsert.Metadata;
using PhenX.EntityFrameworkCore.BulkInsert.Options;

namespace PhenX.EntityFrameworkCore.BulkInsert.Graph;

/// <summary>
/// Result of a graph insert operation.
/// </summary>
/// <typeparam name="T">The root entity type.</typeparam>
internal sealed class GraphInsertResult<T> where T : class
{
    /// <summary>
    /// The root entities that were inserted.
    /// </summary>
    public required IReadOnlyList<T> RootEntities { get; init; }

    /// <summary>
    /// Total count of all entities inserted across all types.
    /// </summary>
    public required int TotalInsertedCount { get; init; }
}

/// <summary>
/// Orchestrates bulk insertion of entity graphs with FK propagation.
/// </summary>
internal sealed class GraphBulkInsertOrchestrator
{
    private static readonly ConcurrentDictionary<(Type, string), Action<object, object?>> PropertySetters = new();
    private static readonly ConcurrentDictionary<(Type, string), Func<object, object?>> PropertyGetters = new();

    private readonly MetadataProvider _metadataProvider;

    public GraphBulkInsertOrchestrator()
    {
        _metadataProvider = new MetadataProvider();
    }

    /// <summary>
    /// Orchestrates the bulk insert of an entity graph.
    /// </summary>
    public async Task<GraphInsertResult<T>> InsertGraphAsync<T>(
        DbContext context,
        IEnumerable<T> entities,
        BulkInsertOptions options,
        IBulkInsertProvider provider,
        CancellationToken ctk) where T : class
    {
        // 1. Collect and sort entities
        var collector = new GraphEntityCollector(context, options);
        var collectionResult = collector.Collect(entities);

        if (collectionResult.EntitiesByType.Count == 0)
        {
            return new GraphInsertResult<T>
            {
                RootEntities = [],
                TotalInsertedCount = 0,
            };
        }

        var totalInserted = 0;
        var graphMetadata = new GraphMetadata(context, options);

        // 2. Insert in dependency order (parents first)
        foreach (var entityType in collectionResult.InsertionOrder)
        {
            if (!collectionResult.EntitiesByType.TryGetValue(entityType, out var entitiesToInsert) ||
                entitiesToInsert.Count == 0)
            {
                continue;
            }

            // Propagate FK values from already-inserted parents
            PropagateParentForeignKeys(entitiesToInsert, entityType, graphMetadata, context);

            // Insert entities of this type
            await InsertEntitiesOfTypeAsync(context, entityType, entitiesToInsert, options, provider, ctk);

            totalInserted += entitiesToInsert.Count;
        }

        // 3. Insert join table records for many-to-many relationships
        if (collectionResult.JoinRecords.Count > 0)
        {
            await InsertJoinRecordsAsync(context, collectionResult.JoinRecords, options, provider, ctk);
        }

        // Return root entities
        var rootEntities = collectionResult.EntitiesByType.TryGetValue(typeof(T), out var roots)
            ? roots.Cast<T>().ToList()
            : [];

        return new GraphInsertResult<T>
        {
            RootEntities = rootEntities,
            TotalInsertedCount = totalInserted,
        };
    }

    private static void PropagateParentForeignKeys(
        List<object> entities,
        Type entityType,
        GraphMetadata graphMetadata,
        DbContext context)
    {
        var efEntityType = graphMetadata.GetEntityType(entityType);
        if (efEntityType == null)
        {
            return;
        }

        // For each FK relationship, propagate PK values from parent entities
        foreach (var fk in efEntityType.GetForeignKeys())
        {
            var principalEntityType = fk.PrincipalEntityType;
            var dependentNavigation = fk.DependentToPrincipal;

            if (dependentNavigation == null)
            {
                continue;
            }

            var navigationPropertyName = dependentNavigation.Name;

            foreach (var entity in entities)
            {
                // Get the parent entity via navigation property
                var parentEntity = GetPropertyValue(entity, navigationPropertyName);
                if (parentEntity == null)
                {
                    continue;
                }

                // Copy PK values from parent to FK properties on this entity
                var fkProperties = fk.Properties;
                var pkProperties = fk.PrincipalKey.Properties;

                for (var i = 0; i < fkProperties.Count; i++)
                {
                    var fkProp = fkProperties[i];
                    var pkProp = pkProperties[i];

                    if (fkProp.IsShadowProperty())
                    {
                        // Shadow properties are handled by EF Core's change tracker
                        // For bulk insert, we can't easily handle shadow FKs
                        continue;
                    }

                    var pkValue = GetPropertyValue(parentEntity, pkProp.Name);
                    SetPropertyValue(entity, fkProp.Name, pkValue);
                }
            }
        }
    }

    private async Task InsertEntitiesOfTypeAsync(
        DbContext context,
        Type entityType,
        List<object> entities,
        BulkInsertOptions options,
        IBulkInsertProvider provider,
        CancellationToken ctk)
    {
        // Use reflection to call the generic BulkInsert method
        var method = typeof(GraphBulkInsertOrchestrator)
            .GetMethod(nameof(InsertEntitiesGenericAsync), BindingFlags.NonPublic | BindingFlags.Instance)!
            .MakeGenericMethod(entityType);

        var task = (Task)method.Invoke(this, [context, entities, options, provider, ctk])!;
        await task;
    }

    private async Task InsertEntitiesGenericAsync<TEntity>(
        DbContext context,
        List<object> entities,
        BulkInsertOptions options,
        IBulkInsertProvider provider,
        CancellationToken ctk) where TEntity : class
    {
        var typedEntities = entities.Cast<TEntity>().ToList();
        var tableInfo = _metadataProvider.GetTableInfo<TEntity>(context);

        // Check if the entity has identity columns and we need to retrieve generated IDs
        var hasIdentity = tableInfo.PrimaryKey.Any(pk => pk.IsGenerated);

        if (hasIdentity)
        {
            // Use BulkInsertReturnEntities to get back the generated IDs
            var insertedEntities = new List<TEntity>();
            await foreach (var inserted in provider.BulkInsertReturnEntities(
                               false,
                               context,
                               tableInfo,
                               typedEntities,
                               options,
                               null,
                               ctk))
            {
                insertedEntities.Add(inserted);
            }

            // Copy generated IDs back to original entities
            CopyGeneratedIds(typedEntities, insertedEntities, tableInfo);
        }
        else
        {
            // No identity columns, just insert directly
            await provider.BulkInsert(false, context, tableInfo, typedEntities, options, null, ctk);
        }
    }

    private static void CopyGeneratedIds<TEntity>(
        List<TEntity> originalEntities,
        List<TEntity> insertedEntities,
        TableMetadata tableInfo) where TEntity : class
    {
        if (originalEntities.Count != insertedEntities.Count)
        {
            // Count mismatch - this can happen if the bulk insert operation
            // doesn't preserve order. Log a warning for debugging purposes.
            // The graph insert will continue but FK propagation may be incomplete.
            System.Diagnostics.Debug.WriteLine(
                $"Warning: IncludeGraph ID propagation failed for {typeof(TEntity).Name}. " +
                $"Original count: {originalEntities.Count}, Inserted count: {insertedEntities.Count}. " +
                "Foreign key values may not be correctly propagated to dependent entities.");
            return;
        }

        var pkProps = tableInfo.PrimaryKey.Where(pk => pk.IsGenerated).ToList();
        if (pkProps.Count == 0)
        {
            return;
        }

        for (var i = 0; i < originalEntities.Count; i++)
        {
            var original = originalEntities[i];
            var inserted = insertedEntities[i];

            foreach (var pkProp in pkProps)
            {
                var value = GetPropertyValue(inserted, pkProp.PropertyName);
                SetPropertyValue(original, pkProp.PropertyName, value);
            }
        }
    }

    private async Task InsertJoinRecordsAsync(
        DbContext context,
        List<JoinRecord> joinRecords,
        BulkInsertOptions options,
        IBulkInsertProvider provider,
        CancellationToken ctk)
    {
        // Group join records by join entity type
        var groupedRecords = joinRecords.GroupBy(jr => jr.JoinEntityType);

        foreach (var group in groupedRecords)
        {
            var joinEntityType = group.Key;
            var records = group.ToList();

            if (records.Count == 0)
            {
                continue;
            }

            // Get the join table metadata from the first record
            var navigation = records[0].Navigation;
            var fk = navigation.ForeignKey;
            var inverseFk = navigation.InverseForeignKey;

            if (fk == null || inverseFk == null)
            {
                continue;
            }

            // Create join table entries
            var joinEntities = new List<object>();

            foreach (var record in records)
            {
                // Create a dictionary-based join entity
                var joinEntry = Activator.CreateInstance(joinEntityType);
                if (joinEntry == null)
                {
                    System.Diagnostics.Debug.WriteLine(
                        $"Warning: IncludeGraph failed to create join entry for {joinEntityType.Name}. " +
                        "Many-to-many relationship may be incomplete.");
                    continue;
                }

                // Set FK values for left entity
                for (var i = 0; i < fk.Properties.Count; i++)
                {
                    var fkProp = fk.Properties[i];
                    var pkProp = fk.PrincipalKey.Properties[i];

                    var pkValue = GetPropertyValue(record.LeftEntity, pkProp.Name);
                    SetPropertyValue(joinEntry, fkProp.Name, pkValue);
                }

                // Set FK values for right entity
                for (var i = 0; i < inverseFk.Properties.Count; i++)
                {
                    var fkProp = inverseFk.Properties[i];
                    var pkProp = inverseFk.PrincipalKey.Properties[i];

                    var pkValue = GetPropertyValue(record.RightEntity, pkProp.Name);
                    SetPropertyValue(joinEntry, fkProp.Name, pkValue);
                }

                joinEntities.Add(joinEntry);
            }

            if (joinEntities.Count > 0)
            {
                // Insert join entities
                await InsertJoinEntitiesAsync(context, joinEntityType, joinEntities, options, provider, ctk);
            }
        }
    }

    private async Task InsertJoinEntitiesAsync(
        DbContext context,
        Type joinEntityType,
        List<object> joinEntities,
        BulkInsertOptions options,
        IBulkInsertProvider provider,
        CancellationToken ctk)
    {
        var efEntityType = context.Model.FindEntityType(joinEntityType);
        if (efEntityType == null)
        {
            return;
        }

        var sqlDialect = provider.SqlDialect;
        var tableInfo = new TableMetadata(efEntityType, sqlDialect);

        // Use raw SQL insert for join entities since they're often dictionary-based
        var method = typeof(IBulkInsertProvider)
            .GetMethod(nameof(IBulkInsertProvider.BulkInsert))!
            .MakeGenericMethod(joinEntityType);

        var task = (Task)method.Invoke(provider, [false, context, tableInfo, joinEntities, options, null, ctk])!;
        await task;
    }

    private static object? GetPropertyValue(object entity, string propertyName)
    {
        var key = (entity.GetType(), propertyName);
        var getter = PropertyGetters.GetOrAdd(key, k =>
        {
            var property = k.Item1.GetProperty(k.Item2, BindingFlags.Public | BindingFlags.Instance);
            if (property == null)
            {
                return _ => null;
            }

            var param = Expression.Parameter(typeof(object), "obj");
            var cast = Expression.Convert(param, k.Item1);
            var access = Expression.Property(cast, property);
            var convertResult = Expression.Convert(access, typeof(object));

            return Expression.Lambda<Func<object, object?>>(convertResult, param).Compile();
        });

        return getter(entity);
    }

    private static void SetPropertyValue(object entity, string propertyName, object? value)
    {
        var key = (entity.GetType(), propertyName);
        var setter = PropertySetters.GetOrAdd(key, k =>
        {
            var property = k.Item1.GetProperty(k.Item2, BindingFlags.Public | BindingFlags.Instance);
            if (property == null || !property.CanWrite)
            {
                return (_, _) => { };
            }

            var param = Expression.Parameter(typeof(object), "obj");
            var valueParam = Expression.Parameter(typeof(object), "value");
            var cast = Expression.Convert(param, k.Item1);
            var access = Expression.Property(cast, property);
            var convertValue = Expression.Convert(valueParam, property.PropertyType);
            var assign = Expression.Assign(access, convertValue);

            return Expression.Lambda<Action<object, object?>>(assign, param, valueParam).Compile();
        });

        setter(entity, value);
    }
}
