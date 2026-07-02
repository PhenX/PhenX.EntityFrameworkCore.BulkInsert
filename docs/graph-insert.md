# Graph Insert (Navigation Properties)

> ℹ️ Graph inserts that require database-generated key propagation are not supported for Oracle and MySQL providers due to limitations in retrieving generated IDs. Graph inserts using client-generated keys (e.g., GUIDs with `ValueGeneratedNever()`) are supported on all providers.

This library supports bulk inserting entire object graphs, including entities with their related navigation properties.

## Enabling Graph Insert

```csharp
await dbContext.ExecuteBulkInsertAsync(blogs, options =>
{
    options.IncludeGraph = true;
});
```

## How It Works

1. The library traverses all reachable entities via navigation properties
2. Entities are sorted in topological order (parents before children) to respect foreign key constraints
3. Each entity type is bulk inserted in dependency order
4. Generated IDs (identity columns) are propagated to foreign key properties
5. Many-to-many join tables with explicit join entity types are populated automatically (see Limitations below)

## Options

| Option | Default | Description |
|--------|---------|-------------|
| `IncludeGraph` | `false` | Enable graph traversal |
| `MaxGraphDepth` | `0` (unlimited) | Maximum depth to traverse. Use 0 for unlimited. |
| `IncludeNavigations` | `null` (all) | Specific navigation property names to include |
| `ExcludeNavigations` | `null` (none) | Navigation property names to exclude |
| `GraphEntityFilter` | `null` | Predicate to skip entities that already exist (see [Skipping already-persisted entities](#skipping-already-persisted-entities)) |
| `RestoreOriginalPrimaryKeysOnGraphInsertFailure` | `false` | Restore original PK values on the in-memory entities if the insert fails and rolls back |

> ℹ️ Graph insert **mutates the entities you pass in**: foreign-key properties are set from their parents' keys,
> inverse navigation properties are populated so the parent can be found, and database-generated primary keys are
> written back onto the original objects after insertion.

## Supported Relationship Types

- ✅ One-to-Many (e.g., Blog → Posts)
- ✅ Many-to-One (e.g., Post → Blog)
- ✅ One-to-One (e.g., Blog → BlogSettings)
- ✅ Many-to-Many with join table (e.g., Post ↔ Tags)
- ✅ Self-referencing/Hierarchies (e.g., Category → Parent/Children)

## Performance Considerations

- Graph insert is inherently slower than flat insert due to FK propagation overhead
- For entities with identity columns, the library uses `ExecuteBulkInsertReturnEntitiesAsync` internally to retrieve generated IDs
- Consider using client-generated keys (GUIDs with `ValueGeneratedNever()`) to avoid ID propagation overhead
- Use `MaxGraphDepth` to limit traversal for large/deep graphs
- Use `IncludeNavigations` or `ExcludeNavigations` to reduce the scope of insertions

## Example

### One-to-Many Relationship

```csharp
var blog = new Blog
{
    Name = "My Blog",
    Posts = new List<Post>
    {
        new Post { Title = "First Post" },
        new Post { Title = "Second Post" }
    }
};

await dbContext.ExecuteBulkInsertAsync(new[] { blog }, o => o.IncludeGraph = true);

// After insert:
// - blog.Id is populated
// - blog.Posts[0].BlogId == blog.Id
// - blog.Posts[1].BlogId == blog.Id
```

### One-to-One Relationship

```csharp
var blog = new Blog
{
    Name = "My Blog",
    Settings = new BlogSettings { EnableComments = true }
};

await dbContext.ExecuteBulkInsertAsync(new[] { blog }, o => o.IncludeGraph = true);

// After insert:
// - blog.Id is populated
// - blog.Settings.BlogId == blog.Id
```

### Selective Navigation Inclusion

```csharp
var blog = new Blog
{
    Name = "My Blog",
    Posts = new List<Post> { new Post { Title = "Post" } },
    Settings = new BlogSettings { EnableComments = true }
};

// Only insert Posts, not Settings
await dbContext.ExecuteBulkInsertAsync(new[] { blog }, o =>
{
    o.IncludeGraph = true;
    o.IncludeNavigations = new HashSet<string> { "Posts" };
});
```

### Limiting Graph Depth

```csharp
var blog = new Blog
{
    Name = "My Blog",
    Posts = new List<Post>
    {
        new Post
        {
            Title = "Post",
            Tags = new List<Tag> { new Tag { Name = "EF Core" } }  // Won't be inserted
        }
    }
};

// MaxGraphDepth = 1 means only Blog and direct children (Posts)
await dbContext.ExecuteBulkInsertAsync(new[] { blog }, o =>
{
    o.IncludeGraph = true;
    o.MaxGraphDepth = 1;
});
```

### Skipping already-persisted entities

By default every entity reachable through the graph is inserted, so a child that references an
already-persisted parent would re-insert that parent (a primary-key violation with client keys, or a
silent duplicate row with database-generated keys). Use `GraphEntityFilter` to mark such entities as
existing — they are not inserted and their navigations are not traversed, but their primary key values
are still used to propagate foreign keys to their dependents:

```csharp
await dbContext.ExecuteBulkInsertAsync(new[] { blog }, o =>
{
    o.IncludeGraph = true;
    // Treat entities that already have a key as existing (return false => skip insert).
    o.GraphEntityFilter = entity => entity is not Blog b || b.Id == 0;
});
```

## Limitations

- **Shadow foreign keys**: Not supported. If a traversed relationship needs a shadow foreign key
  written, a `NotSupportedException` is thrown — add a CLR foreign-key property or exclude the navigation.
- **Required foreign-key cycles**: If two entity types reference each other with required foreign keys,
  there is no valid insertion order and a `NotSupportedException` is thrown. Make one side optional or
  exclude a navigation to break the cycle. Optional-FK cycles are allowed.
- **Owned entities**: Owned entity types are not included in graph traversal and are not inserted when using `IncludeGraph = true`.
- **Self-referencing hierarchies**: Multi-level self-referencing hierarchies (e.g., Category → Children)
  with database-generated keys are not supported and now throw a `NotSupportedException` instead of
  producing incorrect foreign keys. Root entities (or client-generated keys) work.
- **Many-to-many join tables**: Entities on both sides of many-to-many relationships are traversed and inserted. However, automatic join table population only works with explicit join entity types (not `Dictionary<string, object>` shared-type entities); implicit shared-type join tables are skipped with a warning.
- **`ReturnEntities` / `ReturnEnumerable` APIs**: `IncludeGraph` is not supported on
  `ExecuteBulkInsertReturnEntities(Async)` / `ExecuteBulkInsertReturnEnumerableAsync` and throws a
  `NotSupportedException`. Use `ExecuteBulkInsert(Async)`.
- **OnConflict/Upsert**: Not currently supported with `IncludeGraph = true`.
