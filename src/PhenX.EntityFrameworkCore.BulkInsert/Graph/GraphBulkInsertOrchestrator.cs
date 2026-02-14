using System.Reflection;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Logging;

using PhenX.EntityFrameworkCore.BulkInsert.Abstractions;
using PhenX.EntityFrameworkCore.BulkInsert.Extensions;
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

        using var activity = Telemetry.ActivitySource.StartActivity("InsertGraph");
        activity?.AddTag("synchronous", sync);

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

        // Check if any entity types have database-generated keys - if so, provider must support returning IDs
        var hasAnyDatabaseGeneratedKeys = collectionResult.InsertionOrder.Any(entityType =>
        {
            var efEntityType = graphMetadata.GetEntityType(entityType);
            return efEntityType?.FindPrimaryKey()?.Properties.Any(p => p.ValueGenerated != Microsoft.EntityFrameworkCore.Metadata.ValueGenerated.Never) == true;
        });

        if (hasAnyDatabaseGeneratedKeys && !provider.SupportsOutputInsertedIds)
        {
            throw new NotSupportedException(
                $"The bulk insert provider '{provider.GetType().Name}' does not support returning generated IDs, " +
                $"which is required for IncludeGraph operations when entities have database-generated keys. " +
                $"Consider using client-generated keys (e.g., GUIDs with ValueGeneratedNever()).");
        }

        var connection = await _context.GetConnection(sync, ctk);

        // Track original primary key values for rollback
        var originalPkValues = new Dictionary<object, Dictionary<string, object?>>();

        try
        {
            // 2. Insert in dependency order (parents first)
            foreach (var entityType in collectionResult.InsertionOrder)
            {
                if (!collectionResult.EntitiesByType.TryGetValue(entityType, out var entitiesToInsert) ||
                    entitiesToInsert.Count == 0)
                {
                    continue;
                }

                // Save original PK values before any modifications
                if (options.RestoreOriginalPrimaryKeysOnGraphInsertFailure)
                {
                    SaveOriginalPrimaryKeyValues(entitiesToInsert, entityType, graphMetadata, originalPkValues);
                }

                // Propagate FK values from already-inserted parents
                PropagateParentForeignKeys(entitiesToInsert, entityType, graphMetadata);

                // Insert entities of this type
                await InsertEntitiesOfType(sync, _context, entityType, entitiesToInsert, options, provider,
                    graphMetadata, ctk);

                totalInserted += entitiesToInsert.Count;
            }

            // 3. Insert join table records for many-to-many relationships
            if (collectionResult.JoinRecords.Count > 0)
            {
                totalInserted += await InsertJoinRecords(sync, _context, collectionResult.JoinRecords, options,
                    provider, graphMetadata, ctk);
            }

            // Return root entities
            var rootEntities = collectionResult.EntitiesByType.TryGetValue(typeof(T), out var roots)
                ? roots.Cast<T>().ToList()
                : [];

            // Commit the transaction if we own them.
            await connection.Commit(sync, ctk);

            return new GraphInsertResult<T>
            {
                RootEntities = rootEntities,
                TotalInsertedCount = totalInserted,
            };
        }
        catch
        {
            // Restore original PK values on rollback
            if (options.RestoreOriginalPrimaryKeysOnGraphInsertFailure)
            {
                RestoreOriginalPrimaryKeyValues(originalPkValues, graphMetadata);
            }

            throw;
        }
        finally
        {
            await connection.Close(sync, ctk);
        }
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

        var task = (Task)method.Invoke(this, [sync, context, entities, options, provider, graphMetadata, ctk])!;
        await task;
    }

    private async Task InsertEntitiesGenericAsync<TEntity>(
        bool sync,
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
                               sync,
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
            await provider.BulkInsert(sync, context, tableInfo, typedEntities, options, null, ctk);
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

    private async Task<int> InsertJoinRecords(
        bool sync,
        DbContext context,
        List<JoinRecord> joinRecords,
        BulkInsertOptions options,
        IBulkInsertProvider provider,
        GraphMetadata graphMetadata,
        CancellationToken ctk)
    {
        var totalJoinRecordsInserted = 0;

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
                totalJoinRecordsInserted += joinEntities.Count;
            }
        }

        return totalJoinRecordsInserted;
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
        // Skip dictionary-based shared-type join entities as they are not supported
        // by the bulk insert infrastructure (requires typed IEnumerable<T>)
        if (typeof(IDictionary<string, object>).IsAssignableFrom(joinEntityType))
        {
            _logger?.LogWarning(
                "IncludeGraph: Skipping join table insertion for shared-type entity (Dictionary<string, object>). " +
                "Many-to-many relationships using implicit join tables are not supported. " +
                "Consider using an explicit join entity type.");
            return;
        }

        var efEntityType = context.Model.FindEntityType(joinEntityType);
        if (efEntityType == null)
        {
            return;
        }

        var sqlDialect = provider.SqlDialect;
        var tableInfo = new TableMetadata(efEntityType, sqlDialect);

        // Use reflection to call the generic BulkInsert method with correctly typed entities
        var method = typeof(GraphBulkInsertOrchestrator)
            .GetMethod(nameof(InsertJoinEntitiesGeneric), BindingFlags.NonPublic | BindingFlags.Instance)!
            .MakeGenericMethod(joinEntityType);

        var task = (Task)method.Invoke(this, [sync, context, tableInfo, joinEntities, options, provider, ctk])!;
        await task;
    }

    private static async Task InsertJoinEntitiesGeneric<TJoin>(
        bool sync,
        DbContext context,
        TableMetadata tableInfo,
        List<object> joinEntities,
        BulkInsertOptions options,
        IBulkInsertProvider provider,
        CancellationToken ctk) where TJoin : class
    {
        // Cast to correctly typed list for the provider
        var typedEntities = joinEntities.Cast<TJoin>().ToList();
        await provider.BulkInsert(sync, context, tableInfo, typedEntities, options, null, ctk);
    }

    private static void SaveOriginalPrimaryKeyValues(
        List<object> entities,
        Type entityType,
        GraphMetadata graphMetadata,
        Dictionary<object, Dictionary<string, object?>> originalPkValues)
    {
        var entityMetadata = graphMetadata.GetEntityMetadata(entityType);
        if (entityMetadata == null)
        {
            return;
        }

        var efEntityType = graphMetadata.GetEntityType(entityType);

        var pkProperties = efEntityType?.FindPrimaryKey()?.Properties;
        if (pkProperties == null || !pkProperties.Any())
        {
            return;
        }

        // Only save values for database-generated keys
        var generatedPkProps = pkProperties
            .Where(p => p.ValueGenerated != Microsoft.EntityFrameworkCore.Metadata.ValueGenerated.Never)
            .ToList();

        if (generatedPkProps.Count == 0)
        {
            return;
        }

        foreach (var entity in entities)
        {
            var pkValues = new Dictionary<string, object?>();
            foreach (var pkProp in generatedPkProps)
            {
                var value = entityMetadata.GetPropertyValue(entity, pkProp.Name);
                pkValues[pkProp.Name] = value;
            }
            originalPkValues[entity] = pkValues;
        }
    }

    private static void RestoreOriginalPrimaryKeyValues(
        Dictionary<object, Dictionary<string, object?>> originalPkValues,
        GraphMetadata graphMetadata)
    {
        foreach (var (entity, pkValues) in originalPkValues)
        {
            var entityType = entity.GetType();
            var entityMetadata = graphMetadata.GetEntityMetadata(entityType);
            if (entityMetadata == null)
            {
                continue;
            }

            foreach (var (propertyName, originalValue) in pkValues)
            {
                entityMetadata.SetPropertyValue(entity, propertyName, originalValue);
            }
        }
    }
}
