# Configure the DbContext

Register the bulk insert provider in your `DbContextOptions`:

```csharp{6,8,10,12,14}
services.AddDbContext<MyDbContext>(options =>
{
    options
        // .UseSqlServer(connectionString) // or UseNpgsql or UseSqlite, as appropriate

        .UseBulkInsertPostgreSql()
        // OR
        .UseBulkInsertSqlServer()
        // OR
        .UseBulkInsertSqlite()
        // OR
        .UseBulkInsertMySql()
        // OR
        .UseBulkInsertOracle()
        ;
});
```

# Insert methods

There are two groups of methods for inserting data into the database:

* `ExecuteBulkInsert` - inserts the entities as fast as possible, without returning the inserted entities. This is suitable for scenarios where you don't need to access the inserted data immediately.
* `ExecuteBulkInsertReturnEntities` - inserts the entities and returns the inserted entities. This is useful when you need to access the inserted data right after the insertion, but it's slower because it requires creating an intermediate temporary table.

Each method has an asynchronous version (`ExecuteBulkInsertAsync` and `ExecuteBulkInsertReturnEntitiesAsync`).

These methods all take the same parameters:

* `IEnumerable<T>` - the collection of entities to insert.
* `Action<BulkInsertOptions<T>>` - an optional action to configure the bulk insert options, such as batch size, timeout, etc.
* `OnConflictOptions<T>` - an optional action to configure conflict resolution options, such as ignoring conflicts or updating existing records.
* `CancellationToken` - an optional cancellation token to cancel the operation, only for the asynchronous methods.

### Basic usage

```csharp
// Asynchronously
await dbContext.ExecuteBulkInsertAsync(entities);

// Or synchronously
dbContext.ExecuteBulkInsert(entities);
```

### Bulk insert with options

```csharp
// Common options
await dbContext.ExecuteBulkInsertAsync(entities, options =>
{
    options.BatchSize = 1000; // Set the batch size for the insert operation, the default value is different for each provider
});

// Provider specific options, when available, example for SQL Server
await dbContext.ExecuteBulkInsertAsync(entities, (SqlServerBulkInsertOptions o) => // <<< here specify the SQL Server options class
{
    options.EnableStreaming = true; // Enable streaming for SQL Server
});

// Provider specific options, supporting multiple providers
await dbContext.ExecuteBulkInsertAsync(entities, o =>
{
    o.MoveRows = true;

    if (o is SqlServerBulkInsertOptions sqlServerOptions)
    {
        sqlServerOptions.EnableStreaming = true;
    }
    else if (o is MySqlBulkInsertOptions mysqlOptions)
    {
        mysqlOptions.BatchSize = 1000;
    }
});
```

### Returning inserted entities

```csharp
await dbContext.ExecuteBulkInsertReturnEntitiesAsync(entities);
```

### Conflict resolution / merge / upsert

Conflict resolution works by specifying columns that should be used to detect conflicts and the action to take when
a conflict is detected (e.g., update existing rows), using the `onConflict` parameter.

* The conflicting columns are specified with the `Match` property and must have a unique constraint in the database.
* The action to take when a conflict is detected is specified with the `Update` property. If not specified, the default action is to do nothing (i.e., skip the conflicting rows).
* You can also specify the condition for the update action using either the `Where` or the `RawWhere` property. If not specified, the update action will be applied to all conflicting rows.

```csharp
await dbContext.ExecuteBulkInsertAsync(entities, onConflict: new OnConflictOptions<TestEntity>
{
    Match = e => new
    {
        e.Name,
        // ...other columns to match on
    },

    // Optional: specify the update action, if not specified, the default action is to do nothing
    // Excluded is the row being inserted which is in conflict, and Inserted is the row already in the database.
    Update = (inserted, excluded) => new TestEntity
    {
        Price = inserted.Price // Update the Price column with the new value
    },

    // Optional: specify the condition for the update action
    // Excluded is the row being inserted which is in conflict, and Inserted is the row already in the database.
    // Using raw SQL condition
    RawWhere = (insertedTable, excludedTable) => $"{excludedTable}.some_price > {insertedTable}.some_price",

    // OR using a lambda expression
    Where = (inserted, excluded) => excluded.Price > inserted.Price,
});
```

## Options

The default values for each provider are shown in the table below.

You can override these defaults by passing an action to the `ExecuteBulkInsert` or `ExecuteBulkInsertReturnEntities` methods.

### BatchSize

* Type: `int`
* Default:
  * SQL Server: `50,000`
  * PostgreSQL: N/A (uses native bulk insert)
  * SQLite: `5` (INSERT statement with multiple values)
  * MySQL: N/A (uses native bulk insert)
  * Oracle: `50,000`

The number of rows to insert in each batch.

### CopyTimeout

* Type: `TimeSpan`
* Default: `10 minutes`

The timeout for the bulk insert operation.

### CopyGeneratedColumns
* Type: `bool`
* Default: `false`

Copy computed/generated columns

### MoveRows
* Type: `bool`
* Default: `false` (PostgreSQL only)

Move rows between tables (PostgreSQL only), only applies when returning entities.

### SRID

* Type: `int`
* Default: `4326`

Sets the ID of the Spatial Reference System used by the Geometries to be inserted.

### NotifyProgressAfter

* Type: `int`
* Default: `unset`

Notify after X rows are copied. This is useful for tracking progress in long-running operations.

### OnProgress

* Type: `Action<int>`
* Default: `unset`

Callback for progress reporting. This is called with the number of rows copied so far.

### Converters

* Type: `IEnumerable<IValueConverter>`
* Default: `[GeometryConverter]` (SQL Server and PostgreSQL only)

List of value converters for custom types, such as spatial types.

### CopyOptions

* Type: `Enum`
* Default: `Default` (SQL Server and Oracle only)

Provider-specific copy/bulk options (`SqlBulkCopyOptions` for SQL Server, `OracleBulkCopyOptions` for Oracle, etc)

### EnableStreaming

* Type: `bool`
* Default: `false` (SQL Server only)

Enable streaming bulk copy for SQL Server

### TypeProviders

* Type: `IEnumerable<ITypeProvider>`
* Default: `unset` (PostgreSQL only)

Custom PostgreSQL type providers for handling specific data types.
