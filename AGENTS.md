# AGENTS.md — PhenX.EntityFrameworkCore.BulkInsert

## Project
High-performance, provider-agnostic bulk insert for EF Core 8+. Supports SQL Server, PostgreSQL, SQLite, MySQL, Oracle.

## Commands

```shell
# Build (default TFM = net9.0)
dotnet build PhenX.EntityFrameworkCore.BulkInsert.slnx

# Build specific TFM
dotnet build . --framework net8.0

# Run tests (net9.0 by default)
dotnet test PhenX.EntityFrameworkCore.BulkInsert.slnx
dotnet test . --framework net8.0
dotnet test . --framework net10.0

# Filter by provider
dotnet test . --filter "FullyQualifiedName~Sqlite"
dotnet test . --filter "FullyQualifiedName~PostgreSql"

# Run a single test class
dotnet test . --filter "FullyQualifiedName~BasicTestsSqlite"

# Release build
dotnet build -c Release
dotnet test -c Release

# Pack
dotnet pack -c Release --output nupkgs

# Benchmarks
dotnet run --project tests/PhenX.EntityFrameworkCore.BulkInsert.Benchmark -c Release
```

## Solution structure
- **`src/PhenX.EntityFrameworkCore.BulkInsert/`** — Common library (abstract base, dialect builder, metadata, options, public API)
- **`src/PhenX.EntityFrameworkCore.BulkInsert.{SqlServer,PostgreSql,Sqlite,MySql,Oracle}/`** — Provider-specific implementation
- **`tests/PhenX.EntityFrameworkCore.BulkInsert.Tests/`** — xUnit integration tests (Testcontainers for DBs)
- **`tests/PhenX.EntityFrameworkCore.BulkInsert.Benchmark/`** — BenchmarkDotNet benchmarks

## Test conventions
- **xUnit** + **FluentAssertions** + **Xunit.Combinatorial** + **SkippableFact**
- Abstract base per domain (`BasicTestsBase<TDbContext>`) → provider-specific class (`BasicTestsSqlite`)
- Tests require **Docker** (via Testcontainers) for all DBs except SQLite (in-memory/file)
- Tests skipped automatically when Docker is unavailable or DB container can't start

## Multi-targeting
All projects target `net8.0;net9.0;net10.0`. C# 12, nullable enabled, implicit usings.

## Code style
- No comments inside method bodies describing investigation or reasoning. Comments should explain *what* code does or *why* a design decision was made, not document an ongoing investigation.
- `_camelCase` for private fields, `s_` for statics, `PascalCase` for constants (`.editorconfig`)
- Treat warnings as errors in non-Debug builds (`TreatWarningsAsErrors`)
- Internals visible to test/benchmark/provider projects via `InternalsVisibleTo`

## Workflow
- For every public-facing change (new feature, API change, behavior change), update the **README.md** and any relevant **docs/** files.
- For every code change, add or update corresponding **tests** (unit/integration/benchmark).

## CI
- PRs to `main` → build + test on all 3 TFMs
- Tags `v*` → build, test on all 3 TFMs, pack, push to NuGet.org
