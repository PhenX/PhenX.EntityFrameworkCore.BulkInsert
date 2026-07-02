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

        // Build the graph metadata once and share it with the collector (it was previously built twice
        // per call - once here and once inside the collector - each scanning the whole model).
        var graphMetadata = new GraphMetadata(_context, options);

        // 1. Collect and sort entities
        var collector = new GraphEntityCollector(graphMetadata, options);
        var collectionResult = collector.Collect(entities);

        if (collectionResult.EntitiesByType.Count == 0)
        {
            return new GraphInsertResult<T>
            {
                RootEntities = collectionResult.RootEntities.Cast<T>().ToList(),
                TotalInsertedCount = 0,
            };
        }

        var totalInserted = 0;

        // Check if any entity types have database-generated keys - if so, provider must support returning IDs.
        // Use the precise predicate (shared with ColumnMetadata) so that client-generated keys such as
        // Guid value generators (ValueGeneratedOnAdd) are NOT treated as database-generated.
        var hasAnyDatabaseGeneratedKeys = collectionResult.InsertionOrder.Any(entityType =>
        {
            var efEntityType = graphMetadata.GetEntityType(entityType);
            return efEntityType?.FindPrimaryKey()?.Properties.Any(PropertyHelpers.IsDatabaseGenerated) == true;
        });

        if (hasAnyDatabaseGeneratedKeys && !provider.SupportsOutputInsertedIds)
        {
            throw new NotSupportedException(
                $"The bulk insert provider '{provider.GetType().Name}' does not support returning generated IDs, " +
                $"which is required for IncludeGraph operations when entities have database-generated keys. " +
                $"Consider using client-generated keys (e.g., GUIDs with ValueGeneratedNever()).");
        }

        var connection = await _context.GetConnection(sync, ctk);

        // Track original primary key values for rollback.
        // Keyed by reference so entities overriding Equals/GetHashCode (or whose PK mutates) don't collide.
        var originalPkValues = new Dictionary<object, Dictionary<string, object?>>(ReferenceEqualityComparer.Instance);

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

            // Return the actual root instances captured during collection (handles TPH-derived and
            // proxy root types that a typeof(T) lookup would miss).
            var rootEntities = collectionResult.RootEntities.Cast<T>().ToList();

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

            // A self-referencing relationship whose principal key is database-generated cannot be
            // resolved: parent and child are inserted together in this same batch, so the parent's
            // generated key is not yet known when the child's FK must be written. Detect it up front.
            var isSelfReferencing = fk.PrincipalEntityType == efEntityType;
            var hasDbGeneratedPrincipalKey = isSelfReferencing &&
                                             fk.PrincipalKey.Properties.Any(PropertyHelpers.IsDatabaseGenerated);
            HashSet<object>? batchSet = null;
            if (hasDbGeneratedPrincipalKey)
            {
                batchSet = new HashSet<object>(entities, ReferenceEqualityComparer.Instance);
            }

            foreach (var entity in entities)
            {
                // Get the parent entity via navigation property
                var parentEntity = entityMetadata.GetPropertyValue(entity, navigationPropertyName);
                if (parentEntity == null)
                {
                    continue;
                }

                if (batchSet != null && batchSet.Contains(parentEntity))
                {
                    throw new NotSupportedException(
                        $"IncludeGraph does not support multi-level self-referencing graphs with " +
                        $"database-generated keys: entity type '{entityType.Name}' references a parent of the " +
                        $"same type via '{navigationPropertyName}' that is inserted in the same batch, so its " +
                        $"generated key is not available for foreign-key propagation.");
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
                        // We have an actual parent to propagate from but the FK is a shadow property,
                        // which the bulk-insert path cannot write. Fail loudly instead of silently
                        // producing a row with a default (unset) foreign key.
                        throw new NotSupportedException(
                            $"Relationship '{entityType.Name}.{navigationPropertyName}' uses a shadow foreign key " +
                            $"('{fkProp.Name}'); add a CLR foreign-key property or exclude the navigation via " +
                            $"{nameof(BulkInsertOptions.ExcludeNavigations)}.");
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
                CopyGeneratedIds(typedEntities, insertedEntities, tableInfo, entityMetadata, options);
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
        EntityMetadata entityMetadata,
        BulkInsertOptions options) where TEntity : class
    {
        if (originalEntities.Count != insertedEntities.Count)
        {
            // A count mismatch means we cannot reliably pair inserted rows back to their originals,
            // which would leave dependent entities with default (0) foreign keys inside the same
            // transaction - silent FK corruption. Fail hard so the surrounding transaction rolls back.
            _logger?.LogError(
                "IncludeGraph ID propagation failed for {EntityType}. Original count: {OriginalCount}, Inserted count: {InsertedCount}.",
                typeof(TEntity).Name, originalEntities.Count, insertedEntities.Count);

            throw new InvalidOperationException(
                $"IncludeGraph ID propagation failed for '{typeof(TEntity).Name}': the provider returned " +
                $"{insertedEntities.Count} row(s) for {originalEntities.Count} inserted entity(ies). " +
                "Generated keys cannot be propagated to dependent entities.");
        }

        var pkProps = tableInfo.PrimaryKey.Where(pk => pk.IsGenerated).ToList();
        if (pkProps.Count == 0)
        {
            return;
        }

        // Pair each original entity with its inserted counterpart by matching the values of the
        // non-generated columns (which are identical data by construction) rather than relying on
        // the row order returned by the database, which is not guaranteed to match the input order.
        // If content matching cannot fully pair the rows (e.g. a value was coerced by the database),
        // fall back to positional pairing - never worse than the previous behaviour.
        var mapped = new TEntity[originalEntities.Count];
        if (!TryMatchByContent(originalEntities, insertedEntities, tableInfo, options, mapped))
        {
            _logger?.LogWarning(
                "IncludeGraph could not pair inserted rows by content for {EntityType}; falling back to positional pairing.",
                typeof(TEntity).Name);

            for (var i = 0; i < originalEntities.Count; i++)
            {
                mapped[i] = insertedEntities[i];
            }
        }

        for (var i = 0; i < originalEntities.Count; i++)
        {
            var original = originalEntities[i];
            var inserted = mapped[i];

            foreach (var pkProp in pkProps)
            {
                var value = entityMetadata.GetPropertyValue(inserted, pkProp.PropertyName);
                entityMetadata.SetPropertyValue(original, pkProp.PropertyName, value);
            }
        }
    }

    /// <summary>
    /// Attempts to pair original entities with inserted entities by comparing the values of all
    /// non-generated columns. Returns false (leaving <paramref name="mapped"/> untouched) if the
    /// two sets cannot be paired unambiguously by content.
    /// </summary>
    private static bool TryMatchByContent<TEntity>(
        List<TEntity> originalEntities,
        List<TEntity> insertedEntities,
        TableMetadata tableInfo,
        BulkInsertOptions options,
        TEntity[] mapped) where TEntity : class
    {
        var matchColumns = tableInfo.GetColumns().Where(c => !c.IsGenerated).ToList();
        if (matchColumns.Count == 0)
        {
            // Nothing to distinguish rows by; leave to positional pairing.
            return false;
        }

        var insertedByKey = new Dictionary<object?[], Queue<TEntity>>(RowKeyComparer.Instance);
        foreach (var inserted in insertedEntities)
        {
            var key = BuildRowKey(inserted, matchColumns, options);
            if (!insertedByKey.TryGetValue(key, out var queue))
            {
                queue = new Queue<TEntity>();
                insertedByKey[key] = queue;
            }

            queue.Enqueue(inserted);
        }

        for (var i = 0; i < originalEntities.Count; i++)
        {
            var key = BuildRowKey(originalEntities[i], matchColumns, options);
            if (!insertedByKey.TryGetValue(key, out var queue) || queue.Count == 0)
            {
                return false;
            }

            mapped[i] = queue.Dequeue();
        }

        return true;
    }

    private static object?[] BuildRowKey<TEntity>(
        TEntity entity,
        List<ColumnMetadata> matchColumns,
        BulkInsertOptions options) where TEntity : class
    {
        var key = new object?[matchColumns.Count];
        for (var i = 0; i < matchColumns.Count; i++)
        {
            key[i] = matchColumns[i].GetValue(entity, options);
        }

        return key;
    }

    private sealed class RowKeyComparer : IEqualityComparer<object?[]>
    {
        public static readonly RowKeyComparer Instance = new();

        public bool Equals(object?[]? x, object?[]? y)
        {
            if (x == null || y == null)
            {
                return ReferenceEquals(x, y);
            }

            if (x.Length != y.Length)
            {
                return false;
            }

            for (var i = 0; i < x.Length; i++)
            {
                if (!object.Equals(x[i], y[i]))
                {
                    return false;
                }
            }

            return true;
        }

        public int GetHashCode(object?[] obj)
        {
            var hash = new HashCode();
            foreach (var value in obj)
            {
                hash.Add(value);
            }

            return hash.ToHashCode();
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

            // Get entity metadata for join type
            var joinEntityMetadata = graphMetadata.GetEntityMetadata(joinEntityType);

            // Deduplicate logical join rows across BOTH navigation orientations.
            // The same pair collected via a skip navigation and its inverse produces the same set of
            // FK column values, so we key the seen-set on those (orientation-independent) values rather
            // than on the (Left, Right) reference pair - which would let (A,B) and (B,A) both survive.
            var seenKeys = new HashSet<object?[]>(RowKeyComparer.Instance);
            var joinEntities = new List<object>();

            foreach (var record in group)
            {
                // Resolve fk/inverseFk PER RECORD from its own navigation, so records collected via the
                // inverse navigation get their FK columns oriented correctly (they were previously read
                // from the first record of the group, transposing columns for inverse-collected records).
                var navigation = record.Navigation;
                var fk = navigation.ForeignKey;
                var inverseFk = navigation.InverseForeignKey;

                if (fk == null || inverseFk == null)
                {
                    continue;
                }

                // Get metadata for left and right entities
                var leftMetadata = graphMetadata.GetEntityMetadata(record.LeftEntity.GetType());
                var rightMetadata = graphMetadata.GetEntityMetadata(record.RightEntity.GetType());

                if (leftMetadata == null || rightMetadata == null)
                {
                    continue;
                }

                // Create the join entity
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

                // (columnName -> value) pairs, used both to populate the entry and to form the
                // orientation-independent dedup key below.
                var columnValues = new List<KeyValuePair<string, object?>>(fk.Properties.Count + inverseFk.Properties.Count);

                // Set FK values for left entity
                for (var i = 0; i < fk.Properties.Count; i++)
                {
                    var fkProp = fk.Properties[i];
                    var pkProp = fk.PrincipalKey.Properties[i];

                    var pkValue = leftMetadata.GetPropertyValue(record.LeftEntity, pkProp.Name);
                    SetJoinValue(joinEntry, dictEntry, joinEntityMetadata, fkProp.Name, pkValue);
                    columnValues.Add(new KeyValuePair<string, object?>(fkProp.Name, pkValue));
                }

                // Set FK values for right entity
                for (var i = 0; i < inverseFk.Properties.Count; i++)
                {
                    var fkProp = inverseFk.Properties[i];
                    var pkProp = inverseFk.PrincipalKey.Properties[i];

                    var pkValue = rightMetadata.GetPropertyValue(record.RightEntity, pkProp.Name);
                    SetJoinValue(joinEntry, dictEntry, joinEntityMetadata, fkProp.Name, pkValue);
                    columnValues.Add(new KeyValuePair<string, object?>(fkProp.Name, pkValue));
                }

                // Build an orientation-independent dedup key by ordering the (column, value) pairs by
                // column name and interleaving them into a flat array.
                var key = columnValues
                    .OrderBy(kv => kv.Key, StringComparer.Ordinal)
                    .SelectMany(kv => new[] { (object?)kv.Key, kv.Value })
                    .ToArray();

                if (!seenKeys.Add(key))
                {
                    continue;
                }

                joinEntities.Add(joinEntry);
            }

            if (joinEntities.Count > 0)
            {
                // Insert join entities (only unique ones), counting how many were actually inserted.
                totalJoinRecordsInserted +=
                    await InsertJoinEntities(sync, context, joinEntityType, joinEntities, options, provider, ctk);
            }
        }

        return totalJoinRecordsInserted;
    }

    private static void SetJoinValue(
        object joinEntry,
        IDictionary<string, object>? dictEntry,
        EntityMetadata? joinEntityMetadata,
        string columnName,
        object? value)
    {
        if (dictEntry != null)
        {
            dictEntry[columnName] = value!;
        }
        else
        {
            joinEntityMetadata?.SetPropertyValue(joinEntry, columnName, value);
        }
    }

    private async Task<int> InsertJoinEntities(
        bool sync,
        DbContext context,
        Type joinEntityType,
        List<object> joinEntities,
        BulkInsertOptions options,
        IBulkInsertProvider provider,
        CancellationToken ctk)
    {
        // Skip dictionary-based shared-type join entities as they are not supported
        // by the bulk insert infrastructure (requires typed IEnumerable<T>).
        // Return 0 so the total inserted count is not inflated by rows that were never written.
        if (typeof(IDictionary<string, object>).IsAssignableFrom(joinEntityType))
        {
            _logger?.LogWarning(
                "IncludeGraph: Skipping join table insertion for shared-type entity (Dictionary<string, object>). " +
                "Many-to-many relationships using implicit join tables are not supported. " +
                "Consider using an explicit join entity type.");
            return 0;
        }

        var efEntityType = context.Model.FindEntityType(joinEntityType);
        if (efEntityType == null)
        {
            return 0;
        }

        var sqlDialect = provider.SqlDialect;
        var tableInfo = new TableMetadata(efEntityType, sqlDialect);

        // Use reflection to call the generic BulkInsert method with correctly typed entities
        var method = typeof(GraphBulkInsertOrchestrator)
            .GetMethod(nameof(InsertJoinEntitiesGeneric), BindingFlags.NonPublic | BindingFlags.Instance)!
            .MakeGenericMethod(joinEntityType);

        var invocationResult = method.Invoke(this, [sync, context, tableInfo, joinEntities, options, provider, ctk]);
        if (invocationResult is not Task task)
        {
            throw new InvalidOperationException(
                $"Reflected call to '{nameof(InsertJoinEntitiesGeneric)}' for join entity type '{joinEntityType}' did not return a Task as expected.");
        }
        await task;

        // The bulk insert is all-or-nothing within the surrounding transaction, so on success every
        // supplied join entity was written.
        return joinEntities.Count;
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
            .Where(PropertyHelpers.IsDatabaseGenerated)
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
