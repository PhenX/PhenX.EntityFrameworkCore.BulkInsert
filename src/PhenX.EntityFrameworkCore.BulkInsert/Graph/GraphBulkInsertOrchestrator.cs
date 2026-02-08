using System.Reflection;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Logging;

using PhenX.EntityFrameworkCore.BulkInsert.Abstractions;
using PhenX.EntityFrameworkCore.BulkInsert.Metadata;
using PhenX.EntityFrameworkCore.BulkInsert.Options;

namespace PhenX.EntityFrameworkCore.BulkInsert.Graph;

/// <summary>
/// Orchestrates bulk insertion of entity graphs with FK propagation.
/// </summary>
internal sealed class GraphBulkInsertOrchestrator
{

    private readonly DbContext _context;
    private readonly MetadataProvider _metadataProvider;
    private readonly ILogger<GraphBulkInsertOrchestrator>? _logger;

    public GraphBulkInsertOrchestrator(DbContext context)
    {
        _context = context;
        _metadataProvider = context.GetService<MetadataProvider>();
        _logger = context.GetService<ILogger<GraphBulkInsertOrchestrator>>();
    }

    /// <summary>
    /// Orchestrates the bulk insert of an entity graph.
    /// </summary>
    public async Task<GraphInsertResult<T>> InsertGraph<T>(
        bool sync,
        IEnumerable<T> entities,
        BulkInsertOptions options,
        IBulkInsertProvider provider,
        CancellationToken ctk) where T : class
    {
        if (!provider.SupportsOutputInsertedIds)
        {
            throw new NotSupportedException(
                $"The bulk insert provider '{provider.GetType().Name}' does not support returning generated IDs, which is required for IncludeGraph operations.");
        }

        // 1. Collect and sort entities
        var collector = new GraphEntityCollector(_context, options);
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
        var graphMetadata = new GraphMetadata(_context, options);

        // 2. Insert in dependency order (parents first)
        foreach (var entityType in collectionResult.InsertionOrder)
        {
            if (!collectionResult.EntitiesByType.TryGetValue(entityType, out var entitiesToInsert) ||
                entitiesToInsert.Count == 0)
            {
                continue;
            }

            // Propagate FK values from already-inserted parents
            PropagateParentForeignKeys(entitiesToInsert, entityType, graphMetadata);

            // Insert entities of this type
            await InsertEntitiesOfType(sync, _context, entityType, entitiesToInsert, options, provider, graphMetadata, ctk);

            totalInserted += entitiesToInsert.Count;
        }

        // 3. Insert join table records for many-to-many relationships
        if (collectionResult.JoinRecords.Count > 0)
        {
            await InsertJoinRecords(sync, _context, collectionResult.JoinRecords, options, provider, graphMetadata, ctk);
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
        GraphMetadata graphMetadata)
    {
        var efEntityType = graphMetadata.GetEntityType(entityType);
        if (efEntityType == null)
        {
            return;
        }

        var entityMetadata = graphMetadata.GetEntityMetadata(entityType);
        if (entityMetadata == null)
        {
            return;
        }

        // For each FK relationship, propagate PK values from parent entities
        foreach (var fk in efEntityType.GetForeignKeys())
        {
            var dependentNavigation = fk.DependentToPrincipal;

            if (dependentNavigation == null)
            {
                continue;
            }

            var navigationPropertyName = dependentNavigation.Name;

            foreach (var entity in entities)
            {
                // Get the parent entity via navigation property
                var parentEntity = entityMetadata.GetPropertyValue(entity, navigationPropertyName);
                if (parentEntity == null)
                {
                    continue;
                }

                var parentMetadata = graphMetadata.GetEntityMetadata(parentEntity.GetType());
                if (parentMetadata == null)
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

                    var pkValue = parentMetadata.GetPropertyValue(parentEntity, pkProp.Name);
                    entityMetadata.SetPropertyValue(entity, fkProp.Name, pkValue);
                }
            }
        }
    }

    private async Task InsertEntitiesOfType(
        bool sync,
        DbContext context,
        Type entityType,
        List<object> entities,
        BulkInsertOptions options,
        IBulkInsertProvider provider,
        GraphMetadata graphMetadata,
        CancellationToken ctk)
    {
        // Use reflection to call the generic BulkInsert method
        var method = typeof(GraphBulkInsertOrchestrator)
            .GetMethod(nameof(InsertEntitiesGenericAsync), BindingFlags.NonPublic | BindingFlags.Instance)!
            .MakeGenericMethod(entityType);

        var task = (Task)method.Invoke(this, [context, entities, options, provider, graphMetadata, ctk])!;
        await task;
    }

    private async Task InsertEntitiesGenericAsync<TEntity>(
        DbContext context,
        List<object> entities,
        BulkInsertOptions options,
        IBulkInsertProvider provider,
        GraphMetadata graphMetadata,
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
            var entityMetadata = graphMetadata.GetEntityMetadata(typeof(TEntity));
            if (entityMetadata != null)
            {
                CopyGeneratedIds(typedEntities, insertedEntities, tableInfo, entityMetadata);
            }
        }
        else
        {
            // No identity columns, just insert directly
            await provider.BulkInsert(false, context, tableInfo, typedEntities, options, null, ctk);
        }
    }

    private void CopyGeneratedIds<TEntity>(
        List<TEntity> originalEntities,
        List<TEntity> insertedEntities,
        TableMetadata tableInfo,
        EntityMetadata entityMetadata) where TEntity : class
    {
        if (originalEntities.Count != insertedEntities.Count)
        {
            // Count mismatch - this can happen if the bulk insert operation
            // doesn't preserve order. Log a warning for debugging purposes.
            // The graph insert will continue but FK propagation may be incomplete.
            _logger?.LogWarning(
                "IncludeGraph ID propagation failed for {EntityType}. Original count: {OriginalCount}, Inserted count: {InsertedCount}. Foreign key values may not be correctly propagated to dependent entities.",
                typeof(TEntity).Name, originalEntities.Count, insertedEntities.Count);

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
                var value = entityMetadata.GetPropertyValue(inserted, pkProp.PropertyName);
                entityMetadata.SetPropertyValue(original, pkProp.PropertyName, value);
            }
        }
    }

    private async Task InsertJoinRecords(
        bool sync,
        DbContext context,
        List<JoinRecord> joinRecords,
        BulkInsertOptions options,
        IBulkInsertProvider provider,
        GraphMetadata graphMetadata,
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

            // Get entity metadata for join type
            var joinEntityMetadata = graphMetadata.GetEntityMetadata(joinEntityType);

            // Create join table entries
            var joinEntities = new List<object>();

            foreach (var record in records)
            {
                // Get metadata for left and right entities
                var leftMetadata = graphMetadata.GetEntityMetadata(record.LeftEntity.GetType());
                var rightMetadata = graphMetadata.GetEntityMetadata(record.RightEntity.GetType());

                if (leftMetadata == null || rightMetadata == null)
                {
                    continue;
                }

                // Create a dictionary-based join entity
                var joinEntry = Activator.CreateInstance(joinEntityType);
                if (joinEntry == null)
                {
                    _logger?.LogWarning(
                        "IncludeGraph failed to create join entry for {EntityType}. Many-to-many relationship may be incomplete.",
                        joinEntityType.Name
                    );

                    continue;
                }

                // Check if the join entity is a dictionary (shared-type entity)
                var dictEntry = joinEntry as IDictionary<string, object>;

                // Set FK values for left entity
                for (var i = 0; i < fk.Properties.Count; i++)
                {
                    var fkProp = fk.Properties[i];
                    var pkProp = fk.PrincipalKey.Properties[i];

                    var pkValue = leftMetadata.GetPropertyValue(record.LeftEntity, pkProp.Name);
                    if (dictEntry != null)
                    {
                        dictEntry[fkProp.Name] = pkValue!;
                    }
                    else if (joinEntityMetadata != null)
                    {
                        joinEntityMetadata.SetPropertyValue(joinEntry, fkProp.Name, pkValue);
                    }
                }

                // Set FK values for right entity
                for (var i = 0; i < inverseFk.Properties.Count; i++)
                {
                    var fkProp = inverseFk.Properties[i];
                    var pkProp = inverseFk.PrincipalKey.Properties[i];

                    var pkValue = rightMetadata.GetPropertyValue(record.RightEntity, pkProp.Name);
                    if (dictEntry != null)
                    {
                        dictEntry[fkProp.Name] = pkValue!;
                    }
                    else if (joinEntityMetadata != null)
                    {
                        joinEntityMetadata.SetPropertyValue(joinEntry, fkProp.Name, pkValue);
                    }
                }

                joinEntities.Add(joinEntry);
            }

            if (joinEntities.Count > 0)
            {
                // Insert join entities
                await InsertJoinEntities(sync, context, joinEntityType, joinEntities, options, provider, ctk);
            }
        }
    }

    private async Task InsertJoinEntities(
        bool sync,
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

        var result = method.Invoke(provider, [sync, context, tableInfo, joinEntities, options, null, ctk]);
        if (result is not Task task)
        {
            throw new InvalidOperationException(
                $"The BulkInsert method for join entity type '{joinEntityType.Name}' did not return a Task as expected.");
        }
        await task;
    }
}
