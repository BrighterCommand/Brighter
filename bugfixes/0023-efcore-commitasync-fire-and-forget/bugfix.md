# Bugfix: Fire-and-forget CommitAsync in EF Core transaction providers swallows commit failures

**Linked Issue**: #4399
**Status**: Verified

## Symptom
**Observed**: `await provider.CommitAsync(cancellationToken)` on any of the four relational EF Core transaction providers always completes successfully, even when the underlying database commit fails. The provider calls `_context.Database.CurrentTransaction?.CommitAsync(cancellationToken)` without awaiting, storing, or returning the resulting `Task`, then unconditionally returns `Task.CompletedTask`. A commit failure therefore surfaces (if at all) as an unobserved `TaskException` on the finalizer thread rather than at the `await` call site. The commit is also not guaranteed to have *finished* when the caller resumes.

**Expected**: `CommitAsync` completes only when the underlying `DbTransaction`/`IDbContextTransaction` commit has completed, and faults with the commit's exception when it fails — matching the behaviour of the synchronous `Commit()` overrides and of `MongoDbEntityFrameworkTransactionProvider.CommitAsync`.

**Reproduction (design, not yet run)**: the shape used by the existing Mongo EF tests is directly reusable — fake a `DbContext` whose `Database.CurrentTransaction` returns a fake `IDbContextTransaction` whose `CommitAsync` returns a faulted task (`.ThrowsAsync(...)`), then assert that `await provider.CommitAsync(CancellationToken.None)` throws. Today it does not. See `tests/Paramore.Brighter.MongoDb.Tests/EntityFramework/MongoDbEntityFrameworkTransactionProviderAsyncTest.cs:148` (`When_Committing_Async_With_Cancelled_Token_Should_Propagate`) for the exact pattern.

**Downstream impact**: the outbox flow in `samples/WebAPI/WebAPI_EFCore/GreetingsApp/Handlers/AddGreetingHandlerAsync.cs` commits at line 49, rolls back in the `catch` at line 54, and publishes at line 64. Because the commit never throws, the rollback branch never runs and `ClearOutboxAsync` can publish messages for a write that was never committed.

## Suspected Location
All four `file:line` references below were read and verified against the current working tree.

- `src/Paramore.Brighter.MsSql.EntityFrameworkCore/MsSqlEntityFrameworkCoreTransactionProvider.cs:38-46` — `public override Task CommitAsync(CancellationToken)`; un-awaited call at line 42, `return Task.CompletedTask;` at line 45.
- `src/Paramore.Brighter.PostgreSql.EntityFrameworkCore/PostgreSqlEntityFrameworkTransactionProvider.cs:43-51` — un-awaited call at line 47, `return Task.CompletedTask;` at line 50.
- `src/Paramore.Brighter.MySql.EntityFrameworkCore/MySqlEntityFrameworkTransactionProvider.cs:43-51` — un-awaited call at line 47, `return Task.CompletedTask;` at line 50.
- `src/Paramore.Brighter.Sqlite.EntityFrameworkCore/SqliteEntityFrameworkTransactionProvider.cs:42-50` — un-awaited call at line 46, `return Task.CompletedTask;` at line 49.

Contract and comparison points (verified):

- `src/Paramore.Brighter/RelationalDbTransactionProvider.cs:48-57` — base `public virtual Task CommitAsync(CancellationToken cancellationToken)`. It is **virtual** (so all four overrides are legitimate), and the base body calls the **synchronous** `Transaction!.Commit()`, nulls `Transaction`, and returns `Task.CompletedTask`. Note the base signature has **no default value** for `cancellationToken`, while the interface declares one.
- `src/Paramore.Brighter/IAmABoxTransactionProvider.cs:24` — `Task CommitAsync(CancellationToken cancellationToken = default);` on the non-generic `IAmABoxTransactionProvider`; `IAmABoxTransactionProvider<T>` (line 50) only adds `GetTransaction`/`GetTransactionAsync`. `src/Paramore.Brighter/IAmATransactionConnectionProvider.cs:5` is `IAmARelationalDbConnectionProvider, IAmABoxTransactionProvider<DbTransaction>`. So the required signature is exactly `Task CommitAsync(CancellationToken)` — changing the override to `async Task` preserves the contract; the return type must not become `ValueTask` or gain/lose parameters.
- `src/Paramore.Brighter.MongoDb.EntityFramework/MongoDbEntityFrameworkTransactionProvider.cs:72-79` — the correct pattern confirmed: `async Task CommitAsync`, local variable, `is not null` guard, `await currentTransaction.CommitAsync(cancellationToken)` at line 77. (This class implements `IAmABoxTransactionProvider<IClientSessionHandle>` directly rather than deriving from `RelationalDbTransactionProvider`, so it has no `override` keyword.)

**Existing-test landscape** — there are currently **no unit tests of any kind** for the four relational EF Core transaction providers:

- The only reference to any of them anywhere under `tests/` is `tests/Paramore.Brighter.Extensions.Tests/DispatcherResolutionScopedDependencyTests.cs:100`, which registers `typeof(SqliteEntityFrameworkTransactionProvider<Discography>)` as a DI transaction-provider type. It exercises DI resolution only — never `CommitAsync`.
- `Paramore.Brighter.Extensions.Tests` is the **only** test project that project-references any of the four src projects (`Paramore.Brighter.Sqlite.EntityFrameworkCore`). `Paramore.Brighter.MSSQL.Tests`, `Paramore.Brighter.MySQL.Tests`, `Paramore.Brighter.PostgresSQL.Tests` and `Paramore.Brighter.Sqlite.Tests` have **no** EF Core project/package references at all, so covering all four providers will require either new `ProjectReference` entries or a new test project.
- The closest existing template is `tests/Paramore.Brighter.MongoDb.Tests/EntityFramework/MongoDbEntityFrameworkTransactionProviderAsyncTest.cs` (plus its sync sibling `MongoDbEntityFrameworkTransactionProviderTest.cs`) — xUnit + FakeItEasy, faking `DbContext.Database.CurrentTransaction` and `IDbContextTransaction`, no live database. These require no infrastructure and are the model to copy.

## Root-Cause Hypothesis
**Hypothesis (falsifiable)**: In each of the four providers, `CommitAsync` is a non-`async` method that invokes `IDbContextTransaction.CommitAsync(...)` as a discarded expression statement and then returns an already-completed task. Consequently (a) the returned task completes before the underlying commit has necessarily completed, and (b) any exception from the underlying commit is captured into the abandoned task and never propagated to the caller — so `await provider.CommitAsync(...)` cannot observe a failed commit. This predicts that a test in which the faked `CurrentTransaction.CommitAsync` returns a faulted task will see `await provider.CommitAsync(...)` complete without throwing (falsified if it throws).

**Issue's suggested fix — UNVERIFIED — to be proven or refuted in /bugfix:confirm**:

```csharp
public override async Task CommitAsync(CancellationToken cancellationToken)
{
    if (HasOpenTransaction)
    {
        await _context.Database.CurrentTransaction?.CommitAsync(cancellationToken);
    }
}
```

applied consistently to all four providers. Restated only; not endorsed. Two caveats for /bugfix:confirm to settle:
1. All four projects set `<Nullable>enable</Nullable>` and `src/Directory.Build.props:17` sets `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>`, so `await <null-conditional>` (awaiting a possibly-null `Task`) risks a nullable warning becoming a build error, and would NRE if `CurrentTransaction` were null — the Mongo local-variable + `is not null` shape avoids both.
2. Unlike the base implementation, none of the four overrides reset any state after commit (they derive `HasOpenTransaction` from `_context.Database.CurrentTransaction`, which EF Core clears itself), so the fix should not add `Transaction = null` without justification.

**Scope-adjacent observations (for /bugfix:confirm to accept or exclude; not part of the stated fix — issue #4399 only names the four EF Core providers)**:
- `src/Paramore.Brighter.MySql/MySqlTransactionProvider.cs:57-66` contains the **identical** fire-and-forget pattern (`((MySqlTransaction)Transaction!).CommitAsync(cancellationToken);` un-awaited, then `return Task.CompletedTask;`) in the **non-EF** MySql provider. The issue does not mention it. A repo-wide grep for `.CommitAsync(` shows every other provider (`Sqlite`, `PostgreSql`, `Spanner`, `PostgreSqlProvisioningUnitOfWork`) awaits or returns its commit task correctly.
- `PostgreSqlEntityFrameworkTransactionProvider` and `SqliteEntityFrameworkTransactionProvider` override neither `Rollback()` nor `RollbackAsync()`, and their `GetTransaction()` never assigns the base `Transaction` field. The inherited rollback therefore dereferences a null `Transaction` inside a swallow-all `try/catch` and silently does nothing — which would compound the impact described above once commits start throwing correctly.

## Confirmed Root Cause
**CONFIRMED.** In each of the four providers, `CommitAsync` is a **non-`async`** method whose body contains `_context.Database.CurrentTransaction?.CommitAsync(cancellationToken);` as a discarded *expression statement*, followed by `return Task.CompletedTask;` on every path. Two independent defects follow:

1. **No synchronization** — the returned `Task.CompletedTask` is already complete, so the caller's `await` resumes immediately, before the real (asynchronous) commit round-trip finishes.
2. **No exception propagation** — `RelationalTransaction.CommitAsync` is itself `async`, so every commit failure is captured into the discarded `Task` rather than thrown synchronously. That faulted task has no continuation/observer; on finalization it raises `TaskScheduler.UnobservedTaskException`, which since .NET 4.5 does not rethrow or crash the process — the failure is silently lost. No handler for that event exists anywhere in the repo.

**Why CI/build didn't catch it**: CS4014 ("this call is not awaited") only fires for a discarded awaitable **inside an `async` method**. These four methods are not `async`, so the compiler emits nothing despite `TreatWarningsAsErrors=true` (`src/Directory.Build.props:17`). No Roslyn threading analyzer (VSTHRD/CA2012/CA2007) is referenced in `Directory.Packages.props` or enabled via `.editorconfig`.

## Evidence
- [x] **Code-trace** (verified independently against current working tree):
  1. Caller contract: `src/Paramore.Brighter/IAmABoxTransactionProvider.cs:24` — `Task CommitAsync(CancellationToken cancellationToken = default);`. Real-world caller shape: `samples/WebAPI/WebAPI_EFCore/GreetingsApp/Handlers/AddGreetingHandlerAsync.cs:49` (`await provider.CommitAsync(...)` inside `try`), `:54` (rollback in `catch`), `:59` (`provider.Close()` in `finally`), `:64` (`ClearOutboxAsync` **outside** the try, i.e. always runs if `CommitAsync` didn't throw).
  2. All four providers override `HasOpenTransaction => _context.Database.CurrentTransaction != null`, so the guard is true whenever a transaction is open, and the discarded `CommitAsync(...)?.` call executes.
  3. Between the discarded call and `return Task.CompletedTask;` there is no `.Wait()`, no `GetAwaiter().GetResult()`, no re-read of state, and no continuation — the task is unreachable and unobservable from the call site.
  4. Correct contrast already in the repo: `src/Paramore.Brighter.MongoDb.EntityFramework/MongoDbEntityFrameworkTransactionProvider.cs:72-79` — `async Task CommitAsync`, single read into a local, `is not null` guard, `await currentTransaction.CommitAsync(cancellationToken)`.
- [x] **Red repro is feasible with no live database**, using a technique already proven in CI: `tests/Paramore.Brighter.MongoDb.Tests/EntityFramework/MongoDbEntityFrameworkTransactionProviderAsyncTest.cs` fakes the `DbContext` itself with FakeItEasy (`A.Fake<DbContext>()`, `A.CallTo(() => context.Database.CurrentTransaction).Returns(mockTransaction)`), because `DatabaseFacade.CurrentTransaction` is `virtual`. This works for the relational providers too because their `CommitAsync` never calls `GetConnection`/`GetConnectionAsync` (only `_context.Database.CurrentTransaction`), so no real connection or server is ever touched. Failing assertion: `A.CallTo(() => mockTransaction.CommitAsync(A<CancellationToken>.Ignored)).ThrowsAsync(new InvalidOperationException())` then `await Assert.ThrowsAsync<InvalidOperationException>(() => provider.CommitAsync(CancellationToken.None))` — **fails today** (no exception thrown); `ThrowsAsync`, not `Throws`, is required or the buggy code would false-pass.
  - **Wiring caveat**: no test project currently references the MsSql/PostgreSql/MySql EF Core provider projects. `Paramore.Brighter.MySql.EntityFrameworkCore` targets net8.0/net9.0 only (no net10.0), so a `ProjectReference` from a net10.0 test project needs `#if`/conditional handling, or that case needs a net9.0-only project.

## Suggested-Fix Assessment
**PARTIAL** — the *intent* is confirmed correct, but the literal one-liner from the issue is not safe as written:

```csharp
await _context.Database.CurrentTransaction?.CommitAsync(cancellationToken);
```

- It is legal, bindable C# (`x?.M()` on a `Task`-returning `M` yields `Task?`, and `await` targets `Task?.GetAwaiter()`).
- Under `Nullable enable` (set in all four csproj) + `TreatWarningsAsErrors=true`, Roslyn reports CS8602 (dereference of a possibly-null reference) on `await e` where `e` may be null — this becomes a **build error**, not just a warning. The `if (HasOpenTransaction)` guard does not rescue it: the base's `[MemberNotNullWhen(true, nameof(Transaction))]` narrows `Transaction`, not `_context.Database.CurrentTransaction` — flow analysis can't connect the two.
- Independently of the compiler diagnostic, it introduces a **new runtime NRE** the current (buggy) code doesn't have: `CurrentTransaction` is read twice (once by `HasOpenTransaction`, again in the body) — a TOCTOU gap — and `await (Task)null` throws `NullReferenceException`.
- Corroboration: zero occurrences of `await x?.M(...)` anywhere under `src/`; the Mongo provider deliberately uses a local + `is not null` instead.

**Recommended form** (mirrors the already-correct Mongo provider):
```csharp
public override async Task CommitAsync(CancellationToken cancellationToken)
{
    var currentTransaction = _context.Database.CurrentTransaction;
    if (currentTransaction is not null)
    {
        await currentTransaction.CommitAsync(cancellationToken);
    }
}
```
A single read into a local satisfies nullable flow analysis without `!`, and making the method `async` re-arms CS4014 for any future fire-and-forget introduced inside it. No `Transaction = null` reset is needed — state is derived live from `_context.Database.CurrentTransaction`, which EF clears itself once the awaited commit completes.

## Scope Notes
1. **`src/Paramore.Brighter.MySql/MySqlTransactionProvider.cs:57-66` (non-EF) — CONFIRMED identical defect, and strictly worse** (it also nulls `Transaction` while the commit is still in flight, dropping the only rollback handle and making the transaction eligible for disposal mid-commit). This is the *only* other occurrence in `src/` — `PostgreSqlTransactionProvider` and `SqliteTransactionProvider` (non-EF) correctly `await`; `MsSqlTransactionProvider` (non-EF) doesn't override `CommitAsync` at all (inherits the base's synchronous, non-swallowing implementation). **Decision (user, Confirm gate)**: fold into this same PR/fix — same one-line-shape correction, low risk, closes the cross-backend parity gap in this changeset rather than leaving it open as a separate issue.
   - **Testability caveat (discovered during /bugfix:test)**: unlike the EF Core providers, `CommitAsync` here hard-casts `Transaction` to the concrete `MySqlConnector.MySqlTransaction`, which reflection confirms is **sealed** with only an internal-shaped constructor requiring a real `MySqlConnection` (`ctor(MySqlConnection, IsolationLevel, ILogger)`). It cannot be faked/subclassed by FakeItEasy, so — unlike the four EF Core providers — this cannot be proven with a no-live-infra unit test. Per `.agent_instructions/testing.md` ("We accept test after when working with I/O implementations, where test-first is impractical"), **user decision: fix it and document as test-after** (code-trace only, no automated regression test added for this class in this PR).
2. **Rollback / base-`Transaction`-field gaps — CONFIRMED real, broader than initially suspected, recommend treating as OUT OF SCOPE for this fix, flagged as a follow-up.** None of the four EF providers ever populates the base `Transaction` field (their `GetTransaction()` overrides return the EF transaction without storing it). `PostgreSqlEntityFrameworkTransactionProvider` and `SqliteEntityFrameworkTransactionProvider` override neither `Rollback()` nor `RollbackAsync()`; `MsSqlEntityFrameworkCoreTransactionProvider` and `MySqlEntityFrameworkTransactionProvider` override only `RollbackAsync`, leaving **synchronous `Rollback()` broken in all four**. The inherited `Rollback`/`RollbackAsync` call `Transaction!.Rollback()` on a null `Transaction`, throwing an NRE that is swallowed by the surrounding `catch(Exception) { /*ignore*/ }` — so rollback silently does nothing today. This is currently unreachable in practice because `CommitAsync` never throws; **once this fix lands, the rollback path becomes reachable for the first time and will be found broken** (e.g. via the sample's `catch`/`finally` at `AddGreetingHandlerAsync.cs:54-59`). Must be called out explicitly in the PR body as a newly-exposed follow-up, even though it's not fixed here.
3. **Test naming/placement parity**: new regression tests should mirror `MongoDbEntityFrameworkTransactionProviderAsyncTest.cs` naming (`When_Committing_Async_..._Should_Propagate`) and must also carry over the "null transaction does not throw" case — the regression guard against the NRE the issue's literal one-liner would otherwise have introduced.

## Regression Test
Four RED tests written and confirmed failing for the right reason (`Assert.Throws() Failure: No exception was thrown`) on both net9.0 and net10.0 (net10.0 correctly excludes the net9.0-only MySql EF Core case), using FakeItEasy to fake `DbContext.Database.CurrentTransaction` / `IDbContextTransaction` — no live database required, mirroring `tests/Paramore.Brighter.MongoDb.Tests/EntityFramework/MongoDbEntityFrameworkTransactionProviderAsyncTest.cs`. User-approved 2025-09-22 (see Confirm-gate + Test-first `AskUserQuestion` approvals in session).

- `tests/Paramore.Brighter.Extensions.Tests/When_mssql_ef_commit_async_fails_should_propagate_exception.cs` — `MsSqlEntityFrameworkCoreTransactionProviderCommitAsyncTests`
- `tests/Paramore.Brighter.Extensions.Tests/When_postgresql_ef_commit_async_fails_should_propagate_exception.cs` — `PostgreSqlEntityFrameworkTransactionProviderCommitAsyncTests`
- `tests/Paramore.Brighter.Extensions.Tests/When_sqlite_ef_commit_async_fails_should_propagate_exception.cs` — `SqliteEntityFrameworkTransactionProviderCommitAsyncTests`
- `tests/Paramore.Brighter.Extensions.Tests/When_mysql_ef_commit_async_fails_should_propagate_exception.cs` — `MySqlEntityFrameworkTransactionProviderCommitAsyncTests` (wrapped in `#if NET9_0`; `Paramore.Brighter.MySql.EntityFrameworkCore` targets net8.0;net9.0 only)

Project wiring: `tests/Paramore.Brighter.Extensions.Tests/Paramore.Brighter.Extensions.Tests.csproj` gained a `FakeItEasy` package reference and `ProjectReference`s to `Paramore.Brighter.MsSql.EntityFrameworkCore` and `Paramore.Brighter.PostgreSql.EntityFrameworkCore` (unconditional, both target net8.0;net9.0;net10.0), plus a conditional (`net9.0`-only) `ProjectReference` to `Paramore.Brighter.MySql.EntityFrameworkCore`.

**MySqlTransactionProvider (non-EF) — no automated regression test added.** Confirmed untestable without live infra (sealed `MySqlTransaction`, no fakeable constructor) — user decision: fix with code-trace justification only, documented as `test after` per `.agent_instructions/testing.md`'s I/O exception.

## Fix
Minimal change in all five providers: read `CurrentTransaction`/`Transaction` into a local once, guard with `is not null`, `await` the real commit, and make the method `async` (drop the unconditional `return Task.CompletedTask;`). This matches the already-correct `MongoDbEntityFrameworkTransactionProvider` shape and avoids both the CS8602 nullable-dereference build break and the double-read NRE the issue's literal one-liner would have introduced (see Suggested-Fix Assessment).

- `src/Paramore.Brighter.MsSql.EntityFrameworkCore/MsSqlEntityFrameworkCoreTransactionProvider.cs` — `CommitAsync`
- `src/Paramore.Brighter.PostgreSql.EntityFrameworkCore/PostgreSqlEntityFrameworkTransactionProvider.cs` — `CommitAsync`
- `src/Paramore.Brighter.MySql.EntityFrameworkCore/MySqlEntityFrameworkTransactionProvider.cs` — `CommitAsync`
- `src/Paramore.Brighter.Sqlite.EntityFrameworkCore/SqliteEntityFrameworkTransactionProvider.cs` — `CommitAsync`
- `src/Paramore.Brighter.MySql/MySqlTransactionProvider.cs` (non-EF, folded into scope per Confirm-gate decision) — `CommitAsync` now awaits the underlying `MySqlTransaction.CommitAsync` before nulling `Transaction` (previously nulled it while the commit was still in flight). No automated regression test for this one — untestable without live infra (sealed `MySqlTransaction`); fixed via code-trace justification only (`test after`).

**Verification**: all four EF Core regression tests pass on net9.0 (4/4) and net10.0 (3/4 — MySql EF Core correctly excluded by its `#if NET9_0` guard). `Paramore.Brighter.MySql` (non-EF) builds clean on net8.0/net9.0/net10.0.
