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

## Limitations

- **Shadow foreign keys**: Currently not supported. Add a CLR property for foreign keys.
- **Circular references**: Handled gracefully by tracking visited entities, but may result in incomplete graphs.
- **Owned entities**: Owned entity types are not included in graph traversal and are not inserted when using `IncludeGraph = true`.
- **Self-referencing hierarchies**: Multi-level self-referencing hierarchies (e.g., Category → Children) require multiple insert operations. Root entities can be inserted, but nested children with FK references to other entities of the same type within the same batch are not supported.
- **Many-to-many join tables**: Entities on both sides of many-to-many relationships are traversed and inserted. However, automatic join table population only works with explicit join entity types (not `Dictionary<string, object>` shared-type entities).
- **OnConflict/Upsert**: Not currently supported with `IncludeGraph = true`.
