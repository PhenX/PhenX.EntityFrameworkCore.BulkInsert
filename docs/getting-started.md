---
title: Getting started
lang: en-US
---

# Installation

Install the NuGet package for your database provider:

<table>
  <thead>
    <tr>
      <th>Nuget install</th>
      <th>NuGet Link</th>
    </tr>
  </thead>
  <tbody>
    <tr>
      <td>

```shell
Install-Package PhenX.EntityFrameworkCore.BulkInsert.PostgreSql
```
</td>
      <td><a href="https://www.nuget.org/packages/PhenX.EntityFrameworkCore.BulkInsert.SqlServer"><img src="https://img.shields.io/nuget/v/PhenX.EntityFrameworkCore.BulkInsert.SqlServer.svg" alt="NuGet"></a></td>
    </tr>
    <tr>
      <td>

```shell
Install-Package PhenX.EntityFrameworkCore.BulkInsert.Sqlite
```
</td>
      <td><a href="https://www.nuget.org/packages/PhenX.EntityFrameworkCore.BulkInsert.PostgreSql"><img src="https://img.shields.io/nuget/v/PhenX.EntityFrameworkCore.BulkInsert.PostgreSql.svg" alt="NuGet"></a></td>
    </tr>
    <tr>
      <td>

```shell
Install-Package PhenX.EntityFrameworkCore.BulkInsert.Sqlite
```
</td>
      <td><a href="https://www.nuget.org/packages/PhenX.EntityFrameworkCore.BulkInsert.Sqlite"><img src="https://img.shields.io/nuget/v/PhenX.EntityFrameworkCore.BulkInsert.Sqlite.svg" alt="NuGet"></a></td>
    </tr>
    <tr>
      <td>

```shell
Install-Package PhenX.EntityFrameworkCore.BulkInsert.MySql
```
</td>
      <td><a href="https://www.nuget.org/packages/PhenX.EntityFrameworkCore.BulkInsert.MySql"><img src="https://img.shields.io/nuget/v/PhenX.EntityFrameworkCore.BulkInsert.MySql.svg" alt="NuGet"></a></td>
    </tr>
    <tr>
      <td>

```shell
Install-Package PhenX.EntityFrameworkCore.BulkInsert.Oracle
```
</td>
      <td><a href="https://www.nuget.org/packages/PhenX.EntityFrameworkCore.BulkInsert.Oracle"><img src="https://img.shields.io/nuget/v/PhenX.EntityFrameworkCore.BulkInsert.Oracle.svg" alt="NuGet"></a></td>
    </tr>
  </tbody>
</table>

# Usage

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

Then insert data:

```csharp{2,5}
// Asynchronously
await dbContext.ExecuteBulkInsertAsync(entities);

// Or synchronously
dbContext.ExecuteBulkInsert(entities);
```
