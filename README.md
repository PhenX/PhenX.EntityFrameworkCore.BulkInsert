# PhenX.EntityFrameworkCore.BulkInsert

A high-performance, provider-agnostic bulk insert extension for Entity Framework Core 8+. Supports SQL Server, PostgreSQL, SQLite, MySQL and Oracle.

Its main purpose is to provide a fast way to perform simple bulk inserts in Entity Framework Core applications.

## Why this library?

- **Performance**: It is designed to be fast and memory efficient, making it suitable for high-performance applications.
- **Provider-agnostic**: It works with multiple database providers (SQL Server, PostgreSQL, SQLite and MySQL), allowing you to use it in different environments without changing your code.
- **Simplicity**: The API is simple and easy to use, making it accessible for developers of all skill levels.

For now, it does not support navigation properties, complex types, owned types, shadow properties, or inheritance,
but they are in [the roadmap](#roadmap).

## Packages

| Package Name                                      | Description    | NuGet Link                                                                                                                                                                     |
|---------------------------------------------------|----------------|--------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| `PhenX.EntityFrameworkCore.BulkInsert.SqlServer`  | For SQL Server | [![NuGet](https://img.shields.io/nuget/v/PhenX.EntityFrameworkCore.BulkInsert.SqlServer.svg)](https://www.nuget.org/packages/PhenX.EntityFrameworkCore.BulkInsert.SqlServer)   |
| `PhenX.EntityFrameworkCore.BulkInsert.PostgreSql` | For PostgreSQL | [![NuGet](https://img.shields.io/nuget/v/PhenX.EntityFrameworkCore.BulkInsert.PostgreSql.svg)](https://www.nuget.org/packages/PhenX.EntityFrameworkCore.BulkInsert.PostgreSql) |
| `PhenX.EntityFrameworkCore.BulkInsert.Sqlite`     | For SQLite     | [![NuGet](https://img.shields.io/nuget/v/PhenX.EntityFrameworkCore.BulkInsert.Sqlite.svg)](https://www.nuget.org/packages/PhenX.EntityFrameworkCore.BulkInsert.Sqlite)         |
| `PhenX.EntityFrameworkCore.BulkInsert.MySql`      | For MySql      | [![NuGet](https://img.shields.io/nuget/v/PhenX.EntityFrameworkCore.BulkInsert.Sqlite.svg)](https://www.nuget.org/packages/PhenX.EntityFrameworkCore.BulkInsert.MySql)          |
| `PhenX.EntityFrameworkCore.BulkInsert.Oracle`     | For Oracle      | [![NuGet](https://img.shields.io/nuget/v/PhenX.EntityFrameworkCore.BulkInsert.Oracle.svg)](https://www.nuget.org/packages/PhenX.EntityFrameworkCore.BulkInsert.Oracle)         |
| `PhenX.EntityFrameworkCore.BulkInsert`            | Common library | [![NuGet](https://img.shields.io/nuget/v/PhenX.EntityFrameworkCore.BulkInsert.svg)](https://www.nuget.org/packages/PhenX.EntityFrameworkCore.BulkInsert)                       |

## Installation

Install the NuGet package for your database provider:

```shell
# For SQL Server
Install-Package PhenX.EntityFrameworkCore.BulkInsert.SqlServer

# For PostgreSQL
Install-Package PhenX.EntityFrameworkCore.BulkInsert.PostgreSql

# For SQLite
Install-Package PhenX.EntityFrameworkCore.BulkInsert.Sqlite

# For MySql
Install-Package PhenX.EntityFrameworkCore.BulkInsert.MySql

# For Oracle
Install-Package PhenX.EntityFrameworkCore.BulkInsert.Oracle
```

## Usage

Register the bulk insert provider in your `DbContextOptions`:

```csharp
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

### Very basic usage

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

### Insert with navigation properties (Graph Insert)

Insert entities with their related navigation properties:

```csharp
var blogs = new List<Blog>
{
    new Blog
    {
        Name = "Blog 1",
        Posts = new List<Post>
        {
            new Post { Title = "Post 1" },
            new Post { Title = "Post 2" }
        }
    }
};

await dbContext.ExecuteBulkInsertAsync(blogs, o => o.IncludeGraph = true);
```

> ℹ️ This feature is not available for Oracle and MySQL providers due to limitations in retrieving generated IDs.

See [Graph Insert documentation](https://phenx.github.io/PhenX.EntityFrameworkCore.BulkInsert/graph-insert.html) for details.

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

## Roadmap

- [x] [Add support for navigation properties](https://github.com/PhenX/PhenX.EntityFrameworkCore.BulkInsert/issues/2)
- [x] [Add support for complex types](https://github.com/PhenX/PhenX.EntityFrameworkCore.BulkInsert/issues/3)
- [x] Add support for owned types
- [ ] Add support for shadow properties
- [ ] Add support for TPT (Table Per Type) inheritance
- [ ] Add support for TPC (Table Per Concrete Type) inheritance
- [ ] Add support for TPH (Table Per Hierarchy) inheritance

## Benchmarks

Benchmark projects are available in the [`tests/PhenX.EntityFrameworkCore.BulkInsert.Benchmark`](tests/PhenX.EntityFrameworkCore.BulkInsert.Benchmark/LibComparator.cs) directory.
Run them to compare performance with raw bulk insert methods and other libraries (https://github.com/borisdj/EFCore.BulkExtensions
and https://entityframework-extensions.net/bulk-extensions), using optimized configuration (local Docker is required).

Legend :
 * `PhenX_EntityFrameworkCore_BulkInsert`: this library
 * `RawInsert`: naive implementation without any library, using the native provider API (SqlBulkCopy for SQL Server, BeginBinaryImport for PostgreSQL, raw inserts for SQLite)
 * `Z_EntityFramework_Extensions_EFCore`: https://entityframework-extensions.net/bulk-extensions
 * `EFCore_BulkExtensions`: https://github.com/borisdj/EFCore.BulkExtensions
 * `Linq2Db`: https://github.com/linq2db/linq2db

PostgreSQL results with 500 000 rows :

![bench-postgresql.png](https://raw.githubusercontent.com/PhenX/PhenX.EntityFrameworkCore.BulkInsert/refs/heads/main/images/bench-postgresql.png)

SQLite results with 500 000 rows :

![bench-sqlite.png](https://raw.githubusercontent.com/PhenX/PhenX.EntityFrameworkCore.BulkInsert/refs/heads/main/images/bench-sqlite.png)

MySQL results with 500 000 rows :

![bench-mysql.png](https://raw.githubusercontent.com/PhenX/PhenX.EntityFrameworkCore.BulkInsert/refs/heads/main/images/bench-mysql.png)

Where are the SQL Server and Oracle benchmarks? [You can run them yourself](https://www.google.com/search?q=dewitt+clause).

## Contributing

Contributions are welcome! Please open issues or submit pull requests for bug fixes, features, or documentation improvements.

## License

MIT License. See [LICENSE](LICENSE) for details.
