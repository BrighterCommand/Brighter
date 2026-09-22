# Bugfix: Rollback/RollbackAsync on the EF Core relational transaction providers never reach EF's transaction — sync `Rollback()` silently no-ops on all four

**Linked Issue**: none (folded into PR #4401 at maintainer's request; free-text description, no GitHub issue filed)
**Status**: Verified

## Symptom

**Observed** (all `file:line` below re-verified against the current working tree on branch `fix/4399-efcore-commitasync-fire-and-forget`, i.e. *after* the CommitAsync fix):

1. **Synchronous `Rollback()` is a silent no-op on all four relational EF Core providers** (MsSql, PostgreSql, MySql, Sqlite EntityFrameworkCore). None of the four overrides `Rollback()`, so the inherited `RelationalDbTransactionProvider.Rollback()` runs. Its guard `if (HasOpenTransaction)` is satisfied by the *overridden* `HasOpenTransaction` (which reads `_context.Database.CurrentTransaction != null`), but its body dereferences the *base* `Transaction` field — which these providers never populate. The result is a `NullReferenceException` thrown and immediately discarded by `try { Transaction!.Rollback(); } catch(Exception) { /*ignore*/ }` at `src/Paramore.Brighter/RelationalDbTransactionProvider.cs:130`. Nothing is logged; no ILogger is present in this class at all. The EF `IDbContextTransaction` is never told to roll back.
2. **`RollbackAsync()` is the same silent no-op on PostgreSql and Sqlite EF Core** — neither overrides it, so the identical swallow at `RelationalDbTransactionProvider.cs:142` applies.
3. **`RollbackAsync()` on MsSql and MySql EF Core is *not* a silent no-op on live infrastructure — the informal description is partially incorrect here** (see Root-Cause Hypothesis, "Corrections"). Both override it and reach a real rollback by way of `GetTransaction()`, which returns EF's *unwrapped* `DbTransaction`. They are still defective, but differently: they bypass `IDbContextTransaction`, so EF's own transaction bookkeeping is never cleared, and every failure inside them (bad cast, non-relational transaction, genuine rollback failure) is swallowed by the same `catch (Exception) { /* Ignore*/}`.

**Expected**: `Rollback()` / `RollbackAsync()` on these providers roll back the transaction EF actually owns (`_context.Database.CurrentTransaction`), leaving EF's state consistent afterwards — i.e. the same shape as `MongoDbEntityFrameworkTransactionProvider.Rollback()` / `RollbackAsync()` (`src/Paramore.Brighter.MongoDb.EntityFramework/MongoDbEntityFrameworkTransactionProvider.cs:84-101`) and as the four providers' already-correct `Commit()` / `CommitAsync()`.

**Reproduction (design, not yet run)**: same no-infrastructure technique as the four tests added earlier in this session and as the Mongo EF tests — fake a `DbContext`, point `Database.CurrentTransaction` at a fake `IDbContextTransaction`, call `provider.Rollback()` / `await provider.RollbackAsync(CancellationToken.None)`, then assert `IDbContextTransaction.Rollback()` / `RollbackAsync(...)` `MustHaveHappenedOnceExactly()`. Predicted to fail for all four on the sync path, and for all four on the async path (PostgreSql/Sqlite because the base swallows an NRE; MsSql/MySql because on a fake the `GetTransaction()` → `GetDbTransaction()` unwrap cannot yield a real `SqlTransaction`/`MySqlTransaction` and whatever it throws is swallowed). Exact pattern: `tests/Paramore.Brighter.MongoDb.Tests/EntityFramework/MongoDbEntityFrameworkTransactionProviderAsyncTest.cs:51-68`.

**Downstream impact (verified call sites)**:
- `samples/WebAPI/WebAPI_EFCore/GreetingsApp/Handlers/AddGreetingHandlerAsync.cs` — `GetTransactionAsync` at :28, `CommitAsync` at :49, `RollbackAsync` in the `catch` at :54, `Close()` in the `finally` at :59.
- `samples/WebAPI/WebAPI_EFCore/SalutationApp/Handlers/GreetingMadeHandlerAsync.cs` — `GetTransactionAsync` at :37, `CommitAsync` at :51, `RollbackAsync` in the `catch` at :57, `Close()` in the `finally`.
- After the failed rollback, `Close()` is *also* a no-op: base `Close()` (`RelationalDbTransactionProvider.cs:20-30`) skips the dispose branch because `HasOpenTransaction` is still `true`, and skips `Connection?.Close()` because `IsSharedConnection` is `true` in all four. None of the four overrides `Close()`. `Dispose(bool)` (`:158-171`) disposes the base `Connection`/`Transaction` fields, both of which are permanently `null` for these providers. So the *only* thing that ever ends the transaction is `DbContext` disposal at the end of the DI scope, which disposes EF's `CurrentTransaction` and thus implicitly rolls the ADO.NET transaction back. Consequences: the uncommitted write is not silently committed, but (a) the rollback is deferred well past the handler and is invisible, (b) `HasOpenTransaction` stays `true` for the rest of the scope, so a retry/second message handled on the same scoped `DbContext` re-uses the dead transaction via `GetTransactionAsync` (it returns the existing `CurrentTransaction`) instead of starting a fresh one, and (c) for MsSql/MySql the raw `DbTransaction` is rolled back while EF still believes a transaction is live, so a subsequent `Commit()`/`SaveChangesAsync` on that context operates on an aborted transaction.

## Suspected Location

Base class — `src/Paramore.Brighter/RelationalDbTransactionProvider.cs`:
- `:15` — `protected DbTransaction? Transaction { get; set; }` (the field the EF providers never populate).
- `:113-116` — `[MemberNotNullWhen(true, nameof(Transaction))]` (line `:114`, guarded by `#if !NETSTANDARD`) on `public virtual bool HasOpenTransaction => Transaction != null;`.
- `:126-133` — `Rollback()`; the swallow-all is on one line at **`:130`**: `try { Transaction!.Rollback(); } catch(Exception) { /*ignore*/ }`.
- `:138-147` — `RollbackAsync(CancellationToken)`; identical swallow at **`:142`** (and note it calls the *synchronous* `Rollback()` on the transaction, then returns `Task.CompletedTask`).
- `:20-30` `Close()`, `:35-42` `Commit()`, `:48-57` `CommitAsync()`, `:84-92` `GetTransaction()`, `:99-108` `GetTransactionAsync()`, `:158-171` `Dispose(bool)` — all read and traced.

The four EF Core providers (current state, post-CommitAsync-fix):
- `src/Paramore.Brighter.MsSql.EntityFrameworkCore/MsSqlEntityFrameworkCoreTransactionProvider.cs` — `GetTransaction()` `:80-91`, returns `currentTransaction!.GetDbTransaction()` at `:90` with **no assignment to `Transaction`**; `RollbackAsync` override `:96-103`, swallow at `:100` (`try { await ((SqlTransaction)GetTransaction()).RollbackAsync(cancellationToken); } catch (Exception) { /* Ignore*/}`), dead `Transaction = null;` at `:101`; **no `Rollback()` override**; `HasOpenTransaction => _context.Database.CurrentTransaction != null` at `:106`; correct `Commit()` `:26-32` and `CommitAsync()` `:38-45` for contrast.
- `src/Paramore.Brighter.PostgreSql.EntityFrameworkCore/PostgreSqlEntityFrameworkTransactionProvider.cs` — `GetTransaction()` `:77-86`, returns `currentTransaction.GetDbTransaction()` at `:85`, no assignment; `HasOpenTransaction` at `:88`; **neither `Rollback()` nor `RollbackAsync()` is overridden** (file ends at `:92`).
- `src/Paramore.Brighter.MySql.EntityFrameworkCore/MySqlEntityFrameworkTransactionProvider.cs` — `GetTransaction()` `:79-88`, returns at `:87`, no assignment; `RollbackAsync` override `:93-100`, swallow at `:97` (cast to `MySqlTransaction`), dead `Transaction = null;` at `:98`; **no `Rollback()` override**; `HasOpenTransaction` at `:103`.
- `src/Paramore.Brighter.Sqlite.EntityFrameworkCore/SqliteEntityFrameworkTransactionProvider.cs` — `GetTransaction()` `:78-83`, returns at `:82`, no assignment; `HasOpenTransaction` at `:88`; **neither `Rollback()` nor `RollbackAsync()` is overridden** (file ends at `:95`).

None of the four overrides `GetTransactionAsync()`, so the base (`:99-108`) simply wraps the overridden synchronous `GetTransaction()` — confirming `Transaction` is never assigned on either path.

Reference implementations (verified correct, for shape):
- `src/Paramore.Brighter.MongoDb.EntityFramework/MongoDbEntityFrameworkTransactionProvider.cs:84-87` (`Rollback()` → `_context.Database.CurrentTransaction?.Rollback();`) and `:94-101` (`async Task RollbackAsync` → local, `is not null` guard, `await currentTransaction.RollbackAsync(cancellationToken)` at `:99`). Neither swallows exceptions. As suspected, this class implements `IAmARelationalDbConnectionProvider, IAmABoxTransactionProvider<IClientSessionHandle>` directly (`:40`) and does **not** derive from `RelationalDbTransactionProvider`, so it has no base `Transaction` field and does not share this defect class at all. Its `Close()` (`:55-58`) also disposes `CurrentTransaction`, unlike the four.
- `src/Paramore.Brighter/IAmABoxTransactionProvider.cs:39` / `:44` — the contract being broken: `void Rollback();` and `Task RollbackAsync(CancellationToken cancellationToken = default);`.

Non-EF providers — **all correctly populate the base `Transaction` field, which is exactly why inherited rollback works for them**:
- `src/Paramore.Brighter.MsSql/MsSqlTransactionProvider.cs:54-60` and `:67-73` — both `GetTransaction()`/`GetTransactionAsync()` assign `Transaction`. Overrides neither `CommitAsync` nor `Rollback`/`RollbackAsync` — fully inherits the base.
- `src/Paramore.Brighter.PostgreSql/PostgreSqlTransactionProvider.cs:93-99`, `:106-112` — assign `Transaction`; overrides `CommitAsync` (`:50-58`) but not rollback.
- `src/Paramore.Brighter.Sqlite/SqliteTransactionProvider.cs:104-110`, `:117-129` — assign `Transaction`; overrides `CommitAsync` but not rollback.
- `src/Paramore.Brighter.MySql/MySqlTransactionProvider.cs:94-100`, `:107-113` — assign `Transaction`; `CommitAsync` at `:57-64` (this session's reconciliation); no rollback override.
- `src/Paramore.Brighter.Spanner/SpannerUnitOfWork.cs:22` — fifth subclass, also **not** affected: `GetTransactionAsync` assigns `Transaction`, it does not override `GetTransaction` (so the base assigns it), and its `RollbackAsync` (`:85`) awaits `((SpannerTransaction)Transaction!).RollbackAsync(...)` and **propagates** rather than swallowing. A useful precedent for the swallow question below.

So the defect is precisely the intersection: *derives from `RelationalDbTransactionProvider`* **and** *overrides `GetTransaction()` to return EF's transaction without storing it* **and** *overrides `HasOpenTransaction` to read EF's state* — which is exactly and only the four relational EF Core providers.

## Root-Cause Hypothesis

**Hypothesis (falsifiable)**: the four relational EF Core providers inherit rollback behaviour that is written against a state field they never populate. Their `GetTransaction()` returns `IDbContextTransaction.GetDbTransaction()` without assigning the base `Transaction` property, while their `HasOpenTransaction` override reports EF's `CurrentTransaction` state. This decouples the guard from the data: the base `Rollback()`/`RollbackAsync()` enter their bodies (guard true) and then dereference `Transaction` (null), producing a `NullReferenceException` that the surrounding `catch(Exception) { /*ignore*/ }` (`RelationalDbTransactionProvider.cs:130` and `:142`) discards without logging. Prediction: a test asserting that the faked `IDbContextTransaction.Rollback()`/`RollbackAsync()` was invoked will fail for all four providers and no exception will escape (falsified if the fake's rollback is invoked, or if an exception escapes).

**Corrections to the informal PR-review description, both material:**
1. *"MsSql/MySql … RollbackAsync … silently no-ops"* — **refuted for live infrastructure.** Their overrides call `((SqlTransaction)GetTransaction()).RollbackAsync(...)` / `((MySqlTransaction)GetTransaction()).RollbackAsync(...)`, and `GetTransaction()` returns the *real* unwrapped `DbTransaction`, so a genuine rollback does occur against the database. They never touch the null `Transaction` field (the `Transaction = null;` at `MsSqlEntityFrameworkCoreTransactionProvider.cs:101` / `MySqlEntityFrameworkTransactionProvider.cs:98` is a dead assignment — good evidence the authors believed the base field was live). Their defects are (a) bypassing `IDbContextTransaction`, so EF's `CurrentTransaction` is never cleared and EF's connection-level transaction bookkeeping is left stale, (b) a provider-coupled hard cast, and (c) a swallow-all catch that hides genuine rollback failures — and, on a faked/non-relational transaction, hides the `GetDbTransaction()` unwrap failure too (expected to be `InvalidOperationException` from EF's `IInfrastructure<DbTransaction>` unwrap, or an NRE on a null result — **to be pinned down in confirm**).
2. *"This is currently unreachable in practice because CommitAsync never throws"* (scope note 3 of `bugfixes/0023-efcore-commitasync-fire-and-forget/bugfix.md:103`) — **overstated.** The `catch` in both EF samples wraps the whole body, so `SingleAsync` / `DepositPostAsync` / `SaveChangesAsync` failures already reached `RollbackAsync` before the CommitAsync fix. What the CommitAsync fix changes is that *commit* failures now also reach it — i.e. the fix widens an already-live exposure and makes the most important rollback trigger (a failed commit) hit the broken path for the first time. The two defects are genuinely distinct in cause (fire-and-forget task vs. unpopulated state field) and in code location (`CommitAsync` vs. `Rollback`/`RollbackAsync`), and fixing one does not fix the other.

**Candidate fix shape — UNVERIFIED — to be proven or refuted in /bugfix:confirm**: in each of the four files, override both methods to mirror the already-correct `Commit`/`CommitAsync` shape in the same file (read `_context.Database.CurrentTransaction` into a local once, guard non-null, act on EF's transaction object directly rather than on the inherited `Transaction` field), i.e.:

```csharp
/// <summary>
/// Rolls back a transaction
/// </summary>
public override void Rollback()
{
    var currentTransaction = _context.Database.CurrentTransaction;
    if (currentTransaction is not null)
    {
        currentTransaction.Rollback();
    }
}

/// <summary>
/// Rolls back a transaction
/// </summary>
public override async Task RollbackAsync(CancellationToken cancellationToken = default)
{
    var currentTransaction = _context.Database.CurrentTransaction;
    if (currentTransaction is not null)
    {
        await currentTransaction.RollbackAsync(cancellationToken);
    }
}
```

replacing (not merely supplementing) the existing MsSql/MySql `RollbackAsync` overrides, and adding both to PostgreSql/Sqlite. This removes both hard casts (`SqlTransaction`, `MySqlTransaction`) and their `MySqlConnector`/`Microsoft.Data.SqlClient` coupling in the rollback path, and drops the dead `Transaction = null;` lines.

Points to prove or refute in confirm:
- **API surface**: `IDbContextTransaction` exposes synchronous `Rollback()` and `RollbackAsync(CancellationToken = default)`. In-repo evidence that both compile against the referenced EF Core: `MongoDbEntityFrameworkTransactionProvider.cs:86` and `:99`. Also verify the base signatures being overridden match exactly — base `Rollback()` is `public virtual void` (`:126`) and base `RollbackAsync` is `public virtual Task RollbackAsync(CancellationToken cancellationToken = default)` (`:138`), i.e. the async override *does* carry a default for the token (unlike `CommitAsync`, whose base override at `:48` deliberately has none). Getting that wrong is a build break under `TreatWarningsAsErrors`.
- **Swallow or propagate? (the real design decision)** — recommendation: **propagate**, i.e. do *not* carry the base's `catch (Exception) { /* Ignore */ }` into the new overrides. Reasoning: the base's swallow exists so that a best-effort rollback on an already-broken transaction cannot throw a second exception over the original failure. That reasoning is about *rollback failures*, but with the null field it is instead hiding a *programming* error, which is the whole substance of this bug — and once you call rollback on the real `CurrentTransaction`, the "no transaction to roll back" case is handled explicitly by the null guard, so the remaining swallowed exceptions are exactly the genuine rollback failures an operator needs to see. Propagating also matches every already-correct sibling: `MongoDbEntityFrameworkTransactionProvider` (`:84-101`), `SpannerUnitOfWork.cs:85`, and the `CommitAsync` shape just fixed in these same four files. Counter-argument to weigh at the confirm gate: both EF samples call `RollbackAsync` from inside a `catch`, so a throwing rollback would mask the original exception there (`AddGreetingHandlerAsync.cs:54`, `GreetingMadeHandlerAsync.cs:57` — the latter then `throw;`s the original). This is a behaviour choice, so it needs the confirm-gate decision and, if propagation is chosen, an explicit "may now throw" note in the PR body. **Scope recommendation: do not modify the base class** — leaving `RelationalDbTransactionProvider.Rollback`/`RollbackAsync` untouched keeps the blast radius to the four EF files and to providers that are demonstrably broken today; the base's sync-`Rollback()`-inside-`RollbackAsync()` sync-over-async smell (`:142`) is a separate, pre-existing issue.
- **EF state after rollback**: confirm that calling `IDbContextTransaction.Rollback()`/`RollbackAsync()` clears `_context.Database.CurrentTransaction` (so `HasOpenTransaction` correctly flips to `false` and `Close()` then behaves) — the Mongo tests assume exactly this (`MongoDbEntityFrameworkTransactionProviderAsyncTest.cs:188-212`, `:240-262`). If it does not, `Close()`/reuse-within-scope needs a scope note of its own.
- **`[MemberNotNullWhen(true, nameof(Transaction))]` contract**: independently verified as **violated at runtime by all four** EF providers — each overrides `HasOpenTransaction` to return `true` from EF state while the base `Transaction` stays `null`. There is **no compile-time signal today**: the attribute is active for these builds (all four target `net8.0`/`net9.0`/`net10.0`, never `netstandard`; `Nullable` is `enable`; `TreatWarningsAsErrors=true` and its `NoWarn` list contains only XML-doc warnings), yet the repo builds — because Roslyn does not re-verify an inherited `MemberNotNull(When)` postcondition on an overriding property accessor, and none of the four dereferences `Transaction` under the guard. Worth noting as a latent trap for any future subclass, but the candidate fix does not depend on it; confirm should simply record that a clean build produces no diagnostic.
- **Test wiring is already in place** (verified): `tests/Paramore.Brighter.Extensions.Tests/Paramore.Brighter.Extensions.Tests.csproj` has `FakeItEasy` and `ProjectReference`s to MsSql EF Core, PostgreSql EF Core and Sqlite EF Core unconditionally, plus MySql EF Core under a `'$(TargetFramework)' == 'net9.0'` `ItemGroup` — so a MySql rollback test needs the same `#if NET9_0` guard as `When_mysql_ef_commit_async_fails_should_propagate_exception.cs`. **No existing test anywhere exercises `Rollback`/`RollbackAsync` on any of the four**: a repo-wide grep for `Rollback` under `tests/` returns 65 files, none of which reference these four classes. So a red repro needs no live database and no new project — mirror `When_postgresql_ef_commit_async_fails_should_propagate_exception.cs` and the Mongo async test's rollback cases, covering sync `Rollback()` and async `RollbackAsync()` per provider plus the "null `CurrentTransaction` does not throw" guard case.

## Confirmed Root Cause

**CONFIRMED in full** — two distinct defects behind one symptom, split by method and provider:

**(A) Sync `Rollback()` on all four + async `RollbackAsync()` on PostgreSql/Sqlite — silent NRE swallow.** `RelationalDbTransactionProvider.Transaction` (`src/Paramore.Brighter/RelationalDbTransactionProvider.cs:15`) is `protected`, so only these classes or the base can write it. The only writers are base `GetTransaction()` (overridden away in all four), base `Close()`/`Dispose()` (write `null`), and the MsSql/MySql `RollbackAsync` overrides (write `null`). **`Transaction` is invariably `null` on all four EF Core providers.** Their `HasOpenTransaction` override reads EF's own state instead, decoupling the guard from the data — the inherited `Rollback()`/`RollbackAsync()` guard passes, then dereferences the permanently-null field, throwing `NullReferenceException`, silently swallowed by `catch(Exception) { /*ignore*/ }` (`RelationalDbTransactionProvider.cs:130` / `:142`).

**(B) Async `RollbackAsync()` on MsSql/MySql — real rollback, but bypasses EF and swallows failures.** Both unwrap and roll back the raw ADO.NET `DbTransaction` directly, which *does* roll back the database on live infra, but never goes through `IDbContextTransaction`, so EF's own bookkeeping (`_context.Database.CurrentTransaction`) is never cleared, and any genuine failure is eaten by `catch (Exception) { /* Ignore*/}`. The trailing `Transaction = null;` in both is provably dead code per (A).

**Why the compiler never caught it**: `[MemberNotNullWhen(true, nameof(Transaction))]` on the base `HasOpenTransaction` (`:113-116`) is not re-declared by the four overrides, so the compiler trusts the base's (false, for these types) annotation when analysing the base's own method bodies — the null-forgiving `Transaction!` at `:130`/`:142` is a compile-time-only suppression with zero IL effect.

## Evidence

- [x] **Code-trace (complete, all triage citations independently re-verified)**
- [x] **Red repro feasible with zero live infrastructure**, for all four providers and both methods:
  - **Sync `Rollback()`, all four**: fake `CurrentTransaction`, call `provider.Rollback()`; today the fake's `Rollback()` is never invoked (NRE swallowed) — `A.CallTo(() => fake.Rollback()).MustHaveHappened()` fails, `Record.Exception(() => provider.Rollback())` is `null`.
  - **Async `RollbackAsync()`, PostgreSql + Sqlite**: identical, via the inherited base method.
  - **Async `RollbackAsync()`, MsSql + MySql**: also red today, for a reason not anticipated at triage — `GetTransaction()`'s call to `GetDbTransaction()` throws `InvalidOperationException` on a plain FakeItEasy fake (which doesn't implement EF's internal `IInfrastructure<DbTransaction>`), and that too is swallowed by the same `catch`. No real `SqlTransaction`/`MySqlTransaction` needed to prove red.
- **`IDbContextTransaction.Rollback()`/`RollbackAsync(CancellationToken)` both exist** on EF Core 8.0.22 (the pinned version) — confirmed via the shipped package XML docs, and independently proven callable in this repo by `MongoDbEntityFrameworkTransactionProvider.cs:86,99`, which already compile.
- **`_context.Database.CurrentTransaction` genuinely clears to `null` after a successful rollback** — verified empirically by decompiling the shipped `Microsoft.EntityFrameworkCore` assembly (`ilspycmd`): `RelationalTransaction.Rollback()`/`RollbackAsync()` call `_dbTransaction.Rollback()`/`RollbackAsync()` inside a `try`, and only on success call `ClearTransaction()` → `Connection.UseTransaction(null)` → `CurrentTransaction = null` (and also closes the connection, an EF-bookkeeping side effect the current MsSql/MySql raw-`DbTransaction` path skips entirely — the concrete cost of defect (B)). **On a failing rollback, the `catch` rethrows *before* `ClearTransaction()`**, so `CurrentTransaction`/`HasOpenTransaction` correctly stay "open" — exactly what a caller needs to see.
- **Test wiring confirmed verbatim**: `tests/Paramore.Brighter.Extensions.Tests/Paramore.Brighter.Extensions.Tests.csproj` already has FakeItEasy + the MsSql/PostgreSql/Sqlite EF Core references unconditionally, plus MySql EF Core under `'$(TargetFramework)' == 'net9.0'`.
- **No existing test anywhere exercises `Rollback`/`RollbackAsync` on any of the four** EF Core provider classes.

## Suggested-Fix Assessment

**CONFIRMED** — the candidate fix (override both `Rollback()` and `RollbackAsync()` per file, read `_context.Database.CurrentTransaction` into a local once, guard `is not null`, act on it directly) compiles as written: the `RollbackAsync(CancellationToken cancellationToken = default)` signature matches the base exactly (unlike `CommitAsync`, the base **does** default this parameter — three other in-repo overrides already use this exact form and build). Two refinements folded in: prefer the single-read local-variable form over `if (HasOpenTransaction)` (matches the already-fixed `CommitAsync` shape, doesn't rely on the mendacious `[MemberNotNullWhen]`), and do **not** re-add `Transaction = null;` (proven dead).

## Design Decision: Propagate vs Swallow

**Recommendation: PROPAGATE** (do not carry over the base's `catch (Exception) { /*ignore*/ }`).

This repo has one *documented, ADR-backed* no-throw rollback contract (`Paramore.Brighter.BoxProvisioning.MsSql/MsSqlProvisioningUnitOfWork.cs:84-94`, citing ADR 0058 §B.3) — but it is scoped to `IAmAProvisioningUnitOfWork`, a different interface, and its resolution pattern puts the protective `try` at the **caller** (`SqlBoxMigrationRunner` catches the rollback failure, logs it, rethrows the *original*), not inside the provider. `IAmABoxTransactionProvider`'s own contract (`IAmABoxTransactionProvider.cs:36-44`) makes no such promise, and two of its own implementations already propagate (`MongoDbEntityFrameworkTransactionProvider`, `SpannerUnitOfWork`) — the base's swallow is inconsistent with its own siblings, not a considered choice.

Checked against both real call sites (only two exist in the whole repo — Brighter's own core, CommandProcessor/outbox/pump, never calls `Rollback`/`RollbackAsync` on this interface):
- `GreetingMadeHandlerAsync.cs:53-62` logs the original exception (`:55`) *before* calling `RollbackAsync` (`:57`), then `throw;`s the original (`:61`) — propagating changes which exception the caller ultimately sees, but the original is not lost (already logged), and an exception escapes either way.
- `AddGreetingHandlerAsync.cs:51-56` today discards the original exception entirely (no log, no rethrow) and returns as if successful — propagating strictly adds signal that doesn't exist today.

Net: a failed rollback means data may be uncommitted and locks may still be held — silence is the worse failure mode. Where a caller needs the original exception preserved, the repo's own established pattern is for the *caller* to wrap the rollback call, not for every provider to lie about success.

## Scope Notes

**Must be covered by the fix:**
1. **Both** `Rollback()` and `RollbackAsync()` in **all four** files — nothing in `src/` overrides the synchronous `Rollback()` today (grep-confirmed); PostgreSql/Sqlite need both methods added, MsSql/MySql need `Rollback()` added and their existing `RollbackAsync` replaced.
2. Drop the dead `Transaction = null;` in the MsSql/MySql `RollbackAsync` overrides.
3. Drop the `(SqlTransaction)`/`(MySqlTransaction)` hard casts (also a latent `InvalidCastException`/`InvalidOperationException` risk for any non-matching or non-relational `IDbContextTransaction`).

**Confirmed NOT affected** (re-verified independently): the four non-EF relational providers (`MsSql`/`PostgreSql`/`Sqlite`/`MySql` `TransactionProvider`, non-EF) correctly populate the base `Transaction` field and don't override `HasOpenTransaction`; `SpannerUnitOfWork` already propagates correctly; `RelationalDbConnectionProvider`'s no-op is intentional/documented; `InMemoryTransactionProvider` uses its own field. **No Brighter-core caller** (CommandProcessor/outbox/pump) ever calls `Rollback`/`RollbackAsync` on `IAmABoxTransactionProvider` — the only affected call sites in the whole repo are the two EF Core sample handlers analysed above.

**Adjacent pre-existing defects found while tracing — explicitly OUT OF SCOPE, flagged only, not fixed here:**
4. Base `CommitAsync`/`RollbackAsync` are sync-over-async (call the synchronous ADO.NET method, return `Task.CompletedTask`) — moot for these four (both overridden) but live for any other derived type that isn't.
5. Base `GetTransactionAsync` (`:103-106`) calls `tcs.SetCanceled()` then falls through to `tcs.SetResult(...)` on the same `TaskCompletionSource` — `InvalidOperationException` on a pre-cancelled token (missing `return`).
6. Base `Close()` never disposes the EF `IDbContextTransaction` for these four providers (benign in practice — `RelationalConnection.Dispose` handles it when the scoped `DbContext` is disposed).
7. The sync `Commit()` override in all four files still uses the two-read `if (HasOpenTransaction) { ...CurrentTransaction?.Commit(); }` shape rather than the single-read local used by the fixed `CommitAsync` — harmless, but adding the better-shaped `Rollback()` next to it will look visibly inconsistent. Worth a `/tidy-first` follow-up, not part of this bugfix.

## Regression Test
8 RED tests written and confirmed failing for the right reason (`Assert.Throws()`/`Assert.ThrowsAsync()` — "No exception was thrown") on net9.0 (8/8) and net10.0 (6/6, MySql EF Core excluded via `#if NET9_0`). Each fakes `DbContext.Database.CurrentTransaction` / `IDbContextTransaction` and makes the fake's `Rollback()`/`RollbackAsync()` throw, then asserts the provider's `Rollback()`/`RollbackAsync()` propagates that exception — proving both that the real transaction is (going to be) told to roll back and that failures are surfaced (the confirmed "propagate" design decision), in one assertion per method.

- `tests/Paramore.Brighter.Extensions.Tests/When_mssql_ef_rollback_fails_should_propagate_exception.cs` — `MsSqlEntityFrameworkCoreTransactionProviderRollbackTests`
- `tests/Paramore.Brighter.Extensions.Tests/When_mssql_ef_rollback_async_fails_should_propagate_exception.cs` — `MsSqlEntityFrameworkCoreTransactionProviderRollbackAsyncTests`
- `tests/Paramore.Brighter.Extensions.Tests/When_postgresql_ef_rollback_fails_should_propagate_exception.cs` — `PostgreSqlEntityFrameworkTransactionProviderRollbackTests`
- `tests/Paramore.Brighter.Extensions.Tests/When_postgresql_ef_rollback_async_fails_should_propagate_exception.cs` — `PostgreSqlEntityFrameworkTransactionProviderRollbackAsyncTests`
- `tests/Paramore.Brighter.Extensions.Tests/When_sqlite_ef_rollback_fails_should_propagate_exception.cs` — `SqliteEntityFrameworkTransactionProviderRollbackTests`
- `tests/Paramore.Brighter.Extensions.Tests/When_sqlite_ef_rollback_async_fails_should_propagate_exception.cs` — `SqliteEntityFrameworkTransactionProviderRollbackAsyncTests`
- `tests/Paramore.Brighter.Extensions.Tests/When_mysql_ef_rollback_fails_should_propagate_exception.cs` — `MySqlEntityFrameworkTransactionProviderRollbackTests` (`#if NET9_0`)
- `tests/Paramore.Brighter.Extensions.Tests/When_mysql_ef_rollback_async_fails_should_propagate_exception.cs` — `MySqlEntityFrameworkTransactionProviderRollbackAsyncTests` (`#if NET9_0`)

## Fix
Overrode `Rollback()` and `RollbackAsync(CancellationToken cancellationToken = default)` in all four EF Core providers, mirroring the already-fixed `Commit`/`CommitAsync` shape in the same files (single read of `_context.Database.CurrentTransaction` into a local, `is not null` guard, act directly on EF's `IDbContextTransaction` — no swallow, exceptions propagate per the confirmed design decision):

- `src/Paramore.Brighter.MsSql.EntityFrameworkCore/MsSqlEntityFrameworkCoreTransactionProvider.cs` — added `Rollback()`; replaced the `RollbackAsync` override (dropped the `(SqlTransaction)` hard cast, the swallow-all catch, and the dead `Transaction = null;`); removed the now-unused `using Microsoft.Data.SqlClient;`.
- `src/Paramore.Brighter.MySql.EntityFrameworkCore/MySqlEntityFrameworkTransactionProvider.cs` — same shape; removed the now-unused `using MySqlConnector;`.
- `src/Paramore.Brighter.PostgreSql.EntityFrameworkCore/PostgreSqlEntityFrameworkTransactionProvider.cs` — added both `Rollback()` and `RollbackAsync()` (neither existed before).
- `src/Paramore.Brighter.Sqlite.EntityFrameworkCore/SqliteEntityFrameworkTransactionProvider.cs` — added both `Rollback()` and `RollbackAsync()` (neither existed before).

No changes to the base `RelationalDbTransactionProvider` or to any non-EF provider — confirmed unaffected (Scope Notes).

**Verification**: all 8 new regression tests pass on net9.0 (8/8) and net10.0 (6/6 — MySql EF Core correctly excluded). All four EF Core provider projects build clean, 0 warnings, 0 errors.
