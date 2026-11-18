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
All projects target `net8.0;net9.0;net10.0;net11.0`. C# 12, nullable enabled, implicit usings.
The PostgreSql and Oracle projects and the benchmarks skip `net11.0` until their dependencies support EF Core 11; build and test `net11.0` with `PhenX.EntityFrameworkCore.BulkInsert.Net11.slnf`.

## Code style
- No comments inside method bodies describing investigation or reasoning. Comments should explain *what* code does or *why* a design decision was made, not document an ongoing investigation.
- `_camelCase` for private fields, `s_` for statics, `PascalCase` for constants (`.editorconfig`)
- Treat warnings as errors in non-Debug builds (`TreatWarningsAsErrors`)
- Internals visible to test/benchmark/provider projects via `InternalsVisibleTo`

## Workflow
- For every public-facing change (new feature, API change, behavior change), update the **README.md** and any relevant **docs/** files.
- For every code change, add or update corresponding **tests** (unit/integration/benchmark).
- Before opening any pull request, run the **adversarial review** below.

## Adversarial review (required before every PR)
Review the full diff (`git diff origin/main...HEAD`) with the goal of proving the change wrong, not confirming it works.

- **Independent reviewer**: if your tooling can start a separate agent or session, give it the original task and the diff only, not your reasoning or conclusions. Otherwise, re-read the diff from scratch as if someone else wrote it.
- **Try to break it**, at minimum on these axes:
  - **Every TFM**: build and test each framework in `Directory.Build.props`. Check that `#if` branches and `Condition="'$(TargetFramework)' == '...'"` items cover all of them, including TFMs added by open PRs.
  - **Release build**: `dotnet build PhenX.EntityFrameworkCore.BulkInsert.slnx -c Release` must add no warnings, including warnings emitted by package or MSBuild targets (such as "doesn't support net8.0" TFM-support warnings), which `TreatWarningsAsErrors` does not turn into errors. If restore fails on NuGet audit errors (NU1902/NU1903) that your change did not introduce, build with `-p:NuGetAudit=false` and say so in the PR.
  - **Every provider**: SQL Server, PostgreSQL, SQLite, MySQL, Oracle (SQL dialect, quoting, schema qualification, type mapping).
  - **Consumers**: inspect the packed `.nuspec` (`dotnet pack -c Release`) for dependency changes per TFM, transitive version bumps, and public API breaks.
  - **Tests**: where feasible, new tests must fail without the change (revert or mutate it and re-run); when not (docs-only changes, some dependency changes), say why in the PR. Cover edge cases: empty input, nulls, sync and async paths, return-entities paths, on-conflict/merge options.
  - **Docs and style**: README.md and docs/ updated for public-facing changes; the code style rules above are respected.
- **Act on every finding**: fix it, or explain in the PR description why it is not a problem. Re-run affected builds and tests after fixing.
- **Report it**: add an "Adversarial review" section to the PR description listing what was checked, what was found, and what was fixed or deferred. Name any check that could not run (for example, provider tests without Docker) instead of implying it passed.

## CI
- PRs to `main` → build + test on all 4 TFMs
- Tags `v*` → build, test on all 4 TFMs, pack, push to NuGet.org
