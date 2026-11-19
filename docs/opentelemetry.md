---
title: OpenTelemetry Instrumentation
lang: en-US
---

# OpenTelemetry Instrumentation

PhenX.EntityFrameworkCore.BulkInsert has built-in support for [OpenTelemetry](https://opentelemetry.io/) tracing via the `PhenX.EntityFrameworkCore.BulkInsert.OpenTelemetry` package.

Each bulk insert operation is automatically tracked as an [Activity](https://learn.microsoft.com/en-us/dotnet/api/system.diagnostics.activity) using the `PhenX.EntityFrameworkCore.BulkInsert` activity source, enabling distributed tracing and performance monitoring in your application.

## Installation

Install the instrumentation NuGet package:

```shell
Install-Package PhenX.EntityFrameworkCore.BulkInsert.OpenTelemetry
```

The package depends on `OpenTelemetry.Api` 1.15.3 or later, which brings in a `System.Diagnostics.DiagnosticSource` version that supports each target framework:

| Target framework     | `OpenTelemetry.Api` | `System.Diagnostics.DiagnosticSource` |
|----------------------|---------------------|---------------------------------------|
| `net8.0`, `net9.0`   | 1.15.3 or later     | 10.0.0 package                        |
| `net10.0` and later  | 1.15.3 or later     | Included in .NET                      |

Your application can use any newer OpenTelemetry version. If it references `OpenTelemetry.Api` directly, use 1.15.3 or later; a lower version is reported as a package downgrade (NU1605).

On `net8.0` and `net9.0`, stay on OpenTelemetry 1.20.x: the 1.21.0 packages no longer ship assets for these frameworks and bring in .NET 11 packages, such as `System.Diagnostics.DiagnosticSource` 11.0.0, that do not support them.

## Configuration

Register the instrumentation when configuring your OpenTelemetry `TracerProvider` by calling `AddPhenXEntityFrameworkCoreBulkInsertInstrumentation()`:

```csharp
using OpenTelemetry.Trace;

builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing
        .AddAspNetCoreInstrumentation()
        .AddPhenXEntityFrameworkCoreBulkInsertInstrumentation() // <-- add this
        .AddOtlpExporter()
    );
```

## `TracerProviderBuilderExtensions`

### `AddPhenXEntityFrameworkCoreBulkInsertInstrumentation`

```csharp
public static TracerProviderBuilder AddPhenXEntityFrameworkCoreBulkInsertInstrumentation(
    this TracerProviderBuilder builder)
```

Adds the `PhenX.EntityFrameworkCore.BulkInsert` activity source to the given `TracerProviderBuilder`.

**Parameters**

| Parameter | Type                    | Description                                                |
|-----------|-------------------------|------------------------------------------------------------|
| `builder` | `TracerProviderBuilder` | The `TracerProviderBuilder` to add the instrumentation to. |

**Returns**

The `TracerProviderBuilder` with the `PhenX.EntityFrameworkCore.BulkInsert` instrumentation registered.

## Traced Operations

The following bulk insert operations produce OpenTelemetry traces:

| Operation                              | Description                                    |
|----------------------------------------|------------------------------------------------|
| `ExecuteBulkInsert`                    | Synchronous bulk insert without entity return  |
| `ExecuteBulkInsertAsync`               | Asynchronous bulk insert without entity return |
| `ExecuteBulkInsertReturnEntities`      | Synchronous bulk insert with entity return     |
| `ExecuteBulkInsertReturnEntitiesAsync` | Asynchronous bulk insert with entity return    |

## Activity Source

The activity source name used by this library is:

```
PhenX.EntityFrameworkCore.BulkInsert
```

You can use this name to subscribe directly to the activity source if you need lower-level access:

```csharp
using System.Diagnostics;

ActivitySource.AddActivityListener(new ActivityListener
{
    ShouldListenTo = source => source.Name == "PhenX.EntityFrameworkCore.BulkInsert",
    Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
    ActivityStarted = activity => Console.WriteLine($"Started: {activity.DisplayName}"),
    ActivityStopped = activity => Console.WriteLine($"Stopped: {activity.DisplayName} ({activity.Duration})"),
});
```
