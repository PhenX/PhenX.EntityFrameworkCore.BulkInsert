# Fix Plan — PR #91 (IncludeGraph support) Audit Findings

Branch under review: `copilot/add-include-graph-support` (head `9595782`, base `137d2fc`).
This plan addresses every issue from the audit, ordered so that each phase leaves the
branch in a consistent, testable state. Phase 0 is a prerequisite for everything else.

---

## Phase 0 — Rebase onto current `main`

The PR is in `mergeable_state: dirty`. `main` has moved since the PR base and includes
changes that interact directly with this feature:

- `fea55db` — Guid PKs bulk-inserted as NULL without explicit `ValueGeneratedNever()` (#105).
  Touches the exact `ValueGenerated` semantics this PR relies on (see Fix 6).
- `51cd4e1` / `58f3936` — Oracle schema-qualification and SQL builder fixes.
- `4caa10f` — empty Update expression fix for ON CONFLICT / MERGE.

**Steps**
1. `git rebase origin/main` on `copilot/add-include-graph-support`; resolve conflicts.
2. Re-run the full test suite before touching anything else, to get a clean baseline.
3. Re-verify how #105 changed `ColumnMetadata.IsGenerated` / Guid handling and note it
   for Fix 6.

---

## Phase 1 — Critical correctness fixes

### Fix 1: Deterministic generated-ID back-propagation (silent FK corruption)

**Problem.** `CopyGeneratedIds` (`Graph/GraphBulkInsertOrchestrator.cs:300-310`) pairs
`originalEntities[i]` with `insertedEntities[i]` by index, but nothing guarantees row
order:
- `SqlDialectBuilder.BuildMoveDataSql` (lines 57-135) emits
  `INSERT INTO target SELECT ... FROM temp` + `RETURNING` with **no ORDER BY**.
- SQL Server `OUTPUT` (`SqlServerDialectBuilder.cs:104,119`) is documented as unordered.
- SQLite documents `RETURNING` order as undefined; PostgreSQL gives no formal guarantee.
- The temp-table heap scan itself is unordered and can reorder under parallel plans at
  the row counts this library targets (benchmarks run 500k rows).

**Approach.** Use the `_bulk_insert_id` identity column that is *already added* to every
temp table (`BulkInsertProviderBase.AddBulkInsertIdColumn`) but currently never used:

1. Extend `BuildMoveDataSql` (base + each dialect override) with an
   `orderByBulkInsertId: bool` (or read it from a new internal option) so that:
   - the `SELECT ... FROM temp` gets `ORDER BY {BulkInsertId}`;
   - the returned rows are ordered deterministically:
     - **PostgreSQL / SQLite**: `RETURNING` cannot be ordered directly — wrap it:
       `WITH ins AS (INSERT ... SELECT ... ORDER BY _bulk_insert_id RETURNING ...)`
       is still unordered, so instead insert `_bulk_insert_id` into the RETURNING list
       is impossible (column doesn't exist on target). Use the two-statement pattern:
       keep the temp table, insert ordered, then
       `SELECT <returned columns> FROM target JOIN temp ...` is fragile — prefer the
       simplest robust variant: **return the temp-table ordinal alongside the row**.
       Concretely: `INSERT INTO target (...) SELECT ... FROM temp ORDER BY _bulk_insert_id
       RETURNING <cols>` and read results into a list, THEN verify order by comparing a
       client-known unique column when one exists; where the dialect can't guarantee
       order (SQL Server `OUTPUT`), use `OUTPUT INSERTED... INTO @table` +
       `SELECT ... FROM @table ORDER BY ordinal` or the `MERGE ... OUTPUT src._bulk_insert_id`
       trick (MERGE allows outputting source columns, unlike INSERT).
   - **SQL Server**: switch the returning path to
     `MERGE INTO target USING (SELECT ... FROM #temp) AS src ON 1=0
      WHEN NOT MATCHED THEN INSERT ... OUTPUT src._bulk_insert_id, INSERTED.<cols>;`
     ordered client-side by the outputted `_bulk_insert_id`.
   - **PostgreSQL**: for a plain `INSERT ... SELECT`, rows are produced in SELECT order
     in practice but not guaranteed — instead do
     `INSERT ... SELECT ... ORDER BY _bulk_insert_id RETURNING <cols>` *and* pair by a
     mapping table: `WITH src AS (SELECT ..., _bulk_insert_id FROM temp),
     ins AS (INSERT INTO target ... SELECT ... FROM src ORDER BY _bulk_insert_id
     RETURNING <cols>) SELECT * FROM ins` still lacks the id. The bulletproof
     provider-neutral fallback: **make the returned row carry `_bulk_insert_id`** by
     adding a nullable scratch column path, or pair by unique natural key.
2. If the per-dialect SQL becomes too invasive, implement the **fallback pairing
   strategy** instead (smaller diff, provider-neutral):
   - `BulkInsertReturnEntities` gains an internal overload that also returns the
     temp-table ordinal when the dialect supports it; where it does not, the graph
     orchestrator pairs original↔inserted rows by comparing **all non-generated
     inserted column values** (they are identical data by construction); ambiguity
     (two identical rows) falls back to positional within the ambiguous group — order
     among *identical* rows is harmless because the rows are indistinguishable, and any
     assignment yields an equally correct object graph.
   - This "match by content, positional within equal groups" strategy is provably safe
     and needs no SQL changes. Recommended as the primary implementation; the ORDER BY
     work becomes an optimization.
3. Add a stress test: insert 5–10k parents with children on each provider and assert
   every child row's FK matches the parent row that carries the parent's natural data
   (query back and join on a unique payload column, not on the in-memory objects).

**Files**: `Graph/GraphBulkInsertOrchestrator.cs`, `BulkInsertProviderBase.cs`,
`Dialect/SqlDialectBuilder.cs`, `SqlServer/SqlServerDialectBuilder.cs`, tests.

### Fix 2: Fail hard on ID-propagation count mismatch

**Problem.** `CopyGeneratedIds` (lines 282-292) logs a warning and continues when
`originalEntities.Count != insertedEntities.Count`, so children are inserted with
default FK values (0) inside the same transaction — FK violation at best, silent links
to row id 0 at worst.

**Fix.** Replace the warning with a thrown `InvalidOperationException` (message keeps
the current diagnostic detail). The surrounding transaction already rolls back via the
orchestrator's `catch`/`Close`. Keep the log call as `LogError` before throwing.
Add a unit-style test by faking a provider that returns fewer rows.

**Files**: `Graph/GraphBulkInsertOrchestrator.cs`.

### Fix 3: Bidirectional many-to-many — orientation-normalized dedup + per-record FKs

**Problem.** Join records are collected from both sides of a skip navigation
(`Graph/GraphEntityCollector.cs:117-127`), so `(student, course)` and
`(course, student)` both survive dedup (`EntityPairEqualityComparer` doesn't normalize
orientation), and `InsertJoinRecords` (`GraphBulkInsertOrchestrator.cs:338-340`) reads
`fk`/`inverseFk` from `records[0].Navigation` **for the whole group**, transposing FK
columns for records collected via the inverse navigation.

**Fix.**
1. Resolve `fk`/`inverseFk` **per record** from `record.Navigation`, not from
   `records[0]` (delete the group-level `navigation`/`fk`/`inverseFk` variables).
2. Normalize dedup: key the seen-set on the *join entity type + unordered pair
   orientation*. Simplest robust rule: canonicalize each record so `LeftEntity` is
   always the declaring side of a canonical navigation. Implementation: for a skip
   navigation pair, pick the navigation whose declaring entity type name (or a stable
   `IsOnDependent`-style tiebreak — use `INavigationBase` model order:
   `skipNav.Inverse` vs `skipNav`, choose the one where
   `string.CompareOrdinal(declaring.Name, target.Name) <= 0`) is canonical; when a
   record was collected via the inverse, swap Left/Right and substitute the canonical
   navigation before dedup.
3. Alternative (equally valid, less code): during **collection**, only record join
   entries for the canonical direction of each skip-navigation pair and skip the
   inverse; both sides' entities are still traversed.
4. Tests: populate BOTH sides (`student.Courses` and `course.Students` referencing each
   other) and assert exactly one join row per logical pair with correctly oriented
   columns. Requires an explicit join entity type in the test model (see Fix 4).

**Files**: `Graph/GraphEntityCollector.cs`, `Graph/GraphBulkInsertOrchestrator.cs`,
`Graph/EntityPairEqualityComparer.cs` (may become obsolete or keyed differently), tests.

### Fix 4: Dictionary-based (implicit) join tables — support them, and fix the count

**Problem.** EF Core's *default* many-to-many mapping uses `Dictionary<string, object>`
shared-type join entities; `InsertJoinEntities`
(`GraphBulkInsertOrchestrator.cs:446-453`) skips them with only a log warning — silent
relationship data loss on the most common mapping. Worse, the skipped entities are still
counted into `TotalInsertedCount` (line 428 increments before knowing the insert was
skipped). And the PR's own tests use exactly this mapping, so the join-insert path has
zero coverage.

**Fix.**
1. **Support shared-type join entities** instead of skipping. The bulk-insert core
   requires a typed `IEnumerable<T>`; for `Dictionary<string, object>` join entities:
   - Build `TableMetadata` from the join `IEntityType` (already done at line 455-462 —
     note it must use the *join entity type from the model by name*
     (`context.Model.FindEntityType(joinEntityTypeName)`), not
     `FindEntityType(clrType)`, because multiple shared-type entities share the CLR
     type — pass the `IEntityType` captured in `NavigationMetadata.JoinEntityType`
     through `JoinRecord` instead of just the CLR `Type`).
   - Call `InsertJoinEntitiesGeneric<Dictionary<string, object>>` — the provider writes
     column values via `ColumnMetadata` getters; verify those getters read
     indexer/shadow values for shared-type entities. If `ColumnMetadata` accessors
     can't read dictionary entries, add a dictionary-aware getter path in
     `ColumnMetadata`/`TableMetadata` keyed off `entityType.HasSharedClrType`.
   - Shadow columns on the join entity (e.g. the test's `EnrolledAt`) that have no
     value: insert database/CLR defaults; document that extra join-payload columns get
     default values.
2. If full support turns out to be too large for this PR, **minimum acceptable
   fallback**: throw `NotSupportedException` (not a silent warning) when a traversed
   many-to-many uses a shared-type join entity, and fix `TotalInsertedCount`.
3. **Fix the count regardless**: make `InsertJoinEntities` return the number actually
   inserted; `InsertJoinRecords` adds that return value instead of `joinEntities.Count`.
4. Tests: assert join rows exist (query the join table with `FromSqlRaw` or a keyless
   entity) for both the Dictionary mapping and a new explicit join-entity mapping.
   Delete/replace the current assertion comment "even if join table wasn't populated".

**Files**: `Graph/GraphBulkInsertOrchestrator.cs`, `Graph/JoinRecord.cs`,
`Metadata/NavigationMetadata.cs`, possibly `Metadata/TableMetadata.cs` /
`Metadata/ColumnMetadata.cs`, test model + `GraphTestsBase.cs`.

---

## Phase 2 — High-severity fixes

### Fix 5: Already-persisted entities are re-inserted

**Problem.** The collector inserts everything reachable; a child referencing an
existing (already persisted) parent re-inserts it → PK violation with client keys, or a
**silent duplicate row with a fresh identity** with generated keys.

**Fix (two layers).**
1. Add an opt-in filter to `BulkInsertOptions`:
   `Func<object, bool>? GraphEntityFilter { get; set; }` — return `false` to treat an
   entity as existing (skip insert, but still use its PK values for FK propagation).
   In `CollectEntity`, when the filter excludes an entity: do not add it to
   `_entitiesByType`, do not create join records where it appears... — correction:
   *do* still create join records only if the option
   `InsertJoinRowsForExistingEntities` (default false) is set; keep traversal of its
   navigations off by default.
2. Cheap default heuristic (documented, no tracking dependency): if an entity's
   database-generated PK already has a non-default value, treat it as existing and skip
   it (FK propagation still reads its PK). Gate behind
   `bool SkipEntitiesWithExistingKeys { get; set; } = true`? — decide default with the
   maintainer; safest is `false` + loud documentation, since changing insert semantics
   implicitly is surprising. Plan: implement the option with default `false`, document
   the duplication hazard in `docs/graph-insert.md` limitations, and add tests for both
   settings.

**Files**: `Options/BulkInsertOptions.cs`, `Graph/GraphEntityCollector.cs`,
`Graph/GraphBulkInsertOrchestrator.cs`, `docs/graph-insert.md`, tests.

### Fix 6: Stop conflating `ValueGenerated != Never` with "database-generated"

**Problem.** The provider gate (`GraphBulkInsertOrchestrator.cs:62-66`) and the
`hasIdentity` check (line 244 via `ColumnMetadata.IsGenerated`, defined as
`ValueGenerated != Never`) classify client-generated `ValueGeneratedOnAdd` keys (Guid
value generators) as database-generated. Consequences: needless
`NotSupportedException` on MySQL/Oracle, and the slow temp-table/RETURNING path for
entities whose keys are actually set client-side.

**Fix.**
1. Introduce a precise predicate, e.g. in a shared helper:
   `static bool IsDatabaseGenerated(IProperty p) =>
      p.ValueGenerated.HasFlag(ValueGenerated.OnAdd) &&
      p.GetValueGeneratorFactory() == null &&
      (p.GetDefaultValueSql() != null || p.GetComputedColumnSql() != null ||
       p.GetValueGenerationStrategy-like provider check ...)`
   — in practice, EF exposes this best as: database-generated ⇔
   `ValueGenerated != Never && property.TryGetDefaultValue(out _) == false &&
   GetValueGeneratorFactory() == null`. Reconcile with what #105 did on `main` for
   Guid PKs (`ColumnMetadata` in current `main`) and reuse that logic — do not invent a
   second definition; **extract whatever #105 settled on into one shared helper** used
   by both `ColumnMetadata` and the graph code.
2. Use the helper in: `hasAnyDatabaseGeneratedKeys`, `SaveOriginalPrimaryKeyValues`
   (lines 513-515), and the per-type `hasIdentity` decision.
3. Tests: Guid-PK entity with `ValueGeneratedOnAdd` (client generator) inserts through
   the graph path on all five providers, including MySQL/Oracle test stubs (at minimum
   verify no `NotSupportedException` is thrown by the gate; full MySQL/Oracle
   integration coverage if containers are available in CI).

**Files**: `Metadata/ColumnMetadata.cs` (or new shared helper),
`Graph/GraphBulkInsertOrchestrator.cs`, tests.

### Fix 7: Turn silent/late failures into upfront, descriptive exceptions

Three documented limitations currently fail silently or with opaque DB errors:

1. **Shadow FKs** (`GraphBulkInsertOrchestrator.cs:198-203`): while building
   `GraphMetadata`, detect any traversed relationship whose FK contains a shadow
   property *and* whose dependent type is present in the collected set; throw
   `NotSupportedException("Relationship X.Y uses a shadow foreign key; add a CLR FK
   property or exclude the navigation.")` instead of skipping.
2. **Type-level FK cycles**: in `GraphMetadata.TopologicalSort`
   (`Metadata/GraphMetadata.cs:118-121`), the cycle branch currently "skips". Track the
   cycle path; if any type on the cycle has entities to insert AND the FK is required
   (`fk.IsRequired`), throw with the cycle description. Optional-FK cycles can proceed
   (insert with NULL then a second UPDATE pass is out of scope — document).
3. **Multi-level self-referencing graphs**: after `PropagateParentForeignKeys`, if any
   entity of type T references a parent of the *same type* that is also in the batch
   and has a database-generated, not-yet-populated key → throw a descriptive
   `NotSupportedException` (docs already declare this unsupported; make the runtime say
   it too). Detection: in `CollectEntity` or during propagation, flag
   `parentEntity.GetType() == entityType && parent is in _visited`.

**Files**: `Metadata/GraphMetadata.cs`, `Graph/GraphBulkInsertOrchestrator.cs`,
`Graph/GraphEntityCollector.cs`, tests for each exception.

---

## Phase 3 — Medium fixes

### Fix 8: `originalPkValues` must use reference equality
`GraphBulkInsertOrchestrator.cs:79` — construct with
`new Dictionary<object, Dictionary<string, object?>>(ReferenceEqualityComparer.Instance)`.
Entities overriding `Equals`/`GetHashCode` currently collide, and PK mutation changes
the hash. One-line fix + test with an `Equals`-overriding entity.

### Fix 9: Iterative graph traversal (StackOverflow)
Rewrite `GraphEntityCollector.CollectEntity` recursion with an explicit
`Stack<(object entity, int depth)>`. Preserve the exact visit semantics: depth check
before visited-add; join-record creation and inverse-navigation setting happen when the
*parent* processes the child, so push `(child, depth+1)` after that bookkeeping. Add a
test with a 100k-deep `Category` parent chain (guarded/`[Trait]`-tagged as slow) or at
least 10k to prove no stack growth.

### Fix 10: `IncludeGraph` on unsupported entry points must throw
`ExecuteBulkInsertReturnEntitiesCoreAsync` and `ExecuteBulkInsertReturnEnumerableAsync`
(`Extensions/PublicExtensions.cs`, `PublicExtensions.DbSet.cs:101-115`) silently ignore
`options.IncludeGraph`. Add: if `options.IncludeGraph` →
`throw new NotSupportedException("IncludeGraph is not supported with the
ReturnEntities/ReturnEnumerable APIs; use ExecuteBulkInsert(Async).")`.
(Implementing graph+ReturnEntities is possible — the orchestrator already produces
`RootEntities` — but keep it a follow-up; wire `GraphInsertResult` in then.)

### Fix 11: Honest `TotalInsertedCount` (partially covered by Fix 4.3)
- Count join rows from the actual insert result (Fix 4.3).
- For entity tables, keep the input count (inserts either succeed fully or throw inside
  the transaction after Fix 2), but recount *after* each successful provider call.

### Fix 12: Lazy-loading proxy support
In `GraphEntityCollector.CollectEntity` and `GraphMetadata.GetEntityType`, resolve the
CLR type via the model when the direct lookup misses: walk `type.BaseType` until a
model type is found (Castle proxies subclass the entity). Centralize in
`GraphMetadata.GetEntityType`. Group entities under the *resolved* model CLR type so
proxies and POCOs of the same entity share one bulk insert. Test with a manually
subclassed entity (no need to pull in the proxies package).

### Fix 13: `GraphInsertResult.RootEntities` correctness
Root lookup by `typeof(T)` misses TPH-derived roots and proxy types. Since the
collector knows the actual root instances (they're the iteration input), capture them
directly: record each root passed to `Collect` in a `List<object>` and expose it on
`GraphCollectionResult`; `InsertGraph` returns those (cast to `T`). Removes the
type-keyed lookup entirely.

---

## Phase 4 — Performance

### Fix 14: Cache graph metadata (biggest win)
`GraphMetadata` is built **twice per call** (`GraphBulkInsertOrchestrator.cs:59` and
inside `GraphEntityCollector`'s ctor), scanning the whole model and compiling
expression accessors every time.
1. Pass the orchestrator's instance into `GraphEntityCollector` (constructor
   parameter) — removes the duplicate immediately.
2. Cache per model in `MetadataProvider` (which already caches `TableMetadata`):
   `GetGraphMetadata(DbContext)` memoized on `context.Model`. **Caveat**: current
   `GraphMetadata` bakes in `BulkInsertOptions` (Include/ExcludeNavigations filtering,
   `GraphMetadata.cs:165-178`). Split it: cache the option-independent part (entity
   types, all navigations, compiled accessors, `EntityMetadata` instances); apply
   include/exclude filtering at read time (`GetNavigations(type, options)` filters the
   cached list — it's a tiny LINQ over a short list, or pre-index by name).
3. `EntityMetadata` accessor caches then live for the provider lifetime — compiled
   delegates amortize across calls.

### Fix 15: No-tracking materialization for returned entities
`BulkInsertProviderBase.CopyFromTempTableAsync` line 222:
`context.Set<TResult>().FromSqlRaw(query).AsAsyncEnumerable()` is a tracking query —
graph inserts of large sets bloat the change tracker with throwaway copies. Change to
`.AsNoTracking()`. This changes behavior of the *public* ReturnEntities API too —
verify no existing test relies on tracking; if the maintainer wants to preserve the old
public behavior, add an internal flag so only the graph path uses no-tracking.

### Fix 16: Benchmark hygiene
Replace the `#if BENCHMARK_INCLUDE_GRAPH` conditional compilation in
`LibComparator.cs` with a separate benchmark class (`GraphInsertBenchmark`) that always
compiles, so graph performance is measured in default CI runs and the flat benchmarks
stay untouched. Also remove the stray blank-line diff noise in `LibComparator.cs`.

---

## Phase 5 — Tests & docs sweep

1. **New tests** (per fixes above): ordering stress test (Fix 1), count-mismatch throw
   (Fix 2), bidirectional M2M (Fix 3), join-table row assertions incl. Dictionary
   mapping (Fix 4), existing-entity filter (Fix 5), Guid client-generated keys across
   providers (Fix 6), shadow-FK / cycle / self-ref exceptions (Fix 7), Equals-override
   entity (Fix 8), deep-chain traversal (Fix 9), ReturnEntities+IncludeGraph throws
   (Fix 10), proxy-subclass entity (Fix 12), TPH root entities (Fix 13).
2. **Add MySQL/Oracle graph test classes** (client-generated-key scenarios only) —
   currently only PostgreSql/SqlServer/Sqlite have `GraphTests*` classes.
3. **docs/graph-insert.md** updates:
   - Remove or rewrite the many-to-many limitation once Fix 4 lands.
   - Document `GraphEntityFilter` / existing-entities semantics (Fix 5) and the
     duplication hazard.
   - Document that entities are mutated (FK properties and inverse navigations are set
     on the user's objects) — currently undocumented.
   - Document join-payload columns get defaults (Fix 4).
   - State that `IncludeGraph` throws on ReturnEntities APIs (Fix 10).
4. Fix the `TotalInsertedCount` description anywhere it appears.

---

## Suggested execution order & sizing

| Order | Fix | Size | Risk |
|-------|-----|------|------|
| 0 | Rebase on main | S–M | conflicts w/ #105 |
| 1 | Fix 2 (throw on mismatch) | XS | none |
| 2 | Fix 8 (ref-equality dict) | XS | none |
| 3 | Fix 11 (count) + Fix 10 (throw on ReturnEntities) | XS | none |
| 4 | Fix 3 (M2M orientation) | S | low |
| 5 | Fix 6 (generated-key predicate) | S | needs #105 reconciliation |
| 6 | Fix 14 (metadata caching) | M | low |
| 7 | Fix 1 (ordered ID propagation) | M–L | per-dialect SQL |
| 8 | Fix 4 (shared-type join entities) | M–L | core metadata touch |
| 9 | Fix 7 (upfront exceptions) | M | low |
| 10 | Fix 5 (existing entities) | M | API design w/ maintainer |
| 11 | Fix 9, 12, 13, 15, 16 | S each | low |
| 12 | Phase 5 tests & docs | M | — |

Fixes 1 and 4 are the two large items; everything else is mechanical. If the PR needs
to land incrementally, the minimum merge bar is: 0, 1–5, 7 (Fix 1), and the Fix 4
fallback (throw instead of silent skip) — that eliminates all silent-corruption paths;
full shared-type join support and the remaining items can follow up.
