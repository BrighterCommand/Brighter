# Bugfix: OutboxProducerMediator sync resilience branch returns the delegate instead of invoking it

**Linked Issue**: #4344
**Status**: Verified

## Symptom
When a `RequestContext` carries a non-null `ResilienceContext`, the synchronous outbox path runs the resilience pipeline but never invokes the work delegate. The call returns normally, `ExecuteWithResiliencePipeline` returns `true`, and nothing is logged — a silent no-op.

Observed (hypothetically, since nothing sets the property today — see Root-Cause Hypothesis):
- `commandProcessor.Post(request, requestContext)` with `requestContext.ResilienceContext` set completes successfully, but no message reaches the broker.
- Worse than the issue title states: the same helper is used for outbox *writes*, so the deposit also no-ops. `AddToOutbox` (`src/Paramore.Brighter/OutboxProducerMediator.cs:321-327`) and `EndBatchAddToOutbox` (`:631-637`) both guard on the boolean result and throw `ChannelFailureException` when it is `false`; because the helper returns `true` unconditionally (`:1395`) after a non-throwing no-op, that guard never fires. The message is neither stored nor sent, and the caller sees success.
- `MarkDispatched` (`:1072-1075`) is routed through the same helper, so the dispatched marker would also silently not be written — leaving messages the sweeper would re-send.

Expected: the delegate is invoked inside the pipeline, exactly as in the `else` branch (`:1392`) and in the async twin (`:1417-1419`).

Reproduction (not yet run): construct the fixture used by `tests/Paramore.Brighter.Core.Tests/CommandProcessors/Post/When_Posting_A_Message_To_The_Command_Processor.cs`, set `requestContext.ResilienceContext = ResilienceContextPool.Shared.Get()`, `Post`, then assert the `InternalBus` stream is non-empty.

## Suspected Location
Primary:
- `src/Paramore.Brighter/OutboxProducerMediator.cs:1388` — `resiliencePipeline.Execute(_ => action, requestContext.ResilienceContext);` inside `ExecuteWithResiliencePipeline(Action, RequestContext?)` declared at `:1380`.
- Correct sibling for comparison: same file `:1392` (`Execute(action)`), and the async twin `ExecuteWithResiliencePipelineAsync` at `:1405`, whose context branch at `:1417-1419` does invoke the delegate.
- `:1395` — `return true;` is reached whenever no exception escapes, so a no-op is reported as success.

Callers of the **sync** helper (the blast radius), all in the same file:
- `:321-327` — `AddToOutbox` (outbox write; reached from `CommandProcessor.DepositPost` → `CallAddToOutbox`, `src/Paramore.Brighter/CommandProcessor.cs:838,1259-1268`).
- `:344-347` — `CallViaExternalBus` (RPC send; reached from `CommandProcessor.Call`, `src/Paramore.Brighter/CommandProcessor.cs:1460`).
- `:631-637` — `EndBatchAddToOutbox` (bulk deposit; `src/Paramore.Brighter/CommandProcessor.cs:918,1290-1295`).
- `:976-978` — `MarkDispatched` inside the publish-confirmation callback in `ConfigurePublisherCallbackMaybe`.
- `:1060-1062`, `:1066-1069`, `:1072-1075` — `Dispatch`: producer `Send` (confirmation and non-confirmation variants) and `MarkDispatched`. `Dispatch` is called from `ClearOutbox` at `:398`, which is the sync clear reached by `CommandProcessor.Post` (`src/Paramore.Brighter/CommandProcessor.cs:680`) and `CommandProcessor.ClearOutbox` (`src/Paramore.Brighter/CommandProcessor.cs:1350-1357`).

Supporting declarations:
- `src/Paramore.Brighter/RequestContext.cs:107` — `public ResilienceContext? ResilienceContext { get; set; }` (publicly settable from outside the assembly).
- `src/Paramore.Brighter/IRequestContext.cs:97` — interface exposes it **get-only**, so the setter is only reachable via the concrete `RequestContext`.
- `src/Paramore.Brighter/RequestContext.cs:142-153` — `CreateCopy()` copies `Span`, `Policies`, `ResiliencePipeline`, `FeatureSwitches`, `OriginatingMessage`, `InstrumentationOptions` but **not** `ResilienceContext`. Related, separate suspected defect; it also means the copy-based paths (`OutboxProducerMediator.cs:877`, `:974`; `PipelineBuilder.cs:189,234`) can never enter the broken branch.

Provenance: the line was introduced by commit `c536f9a39` ("feat: Add support to Polly Resilience Pipeline (#3677)", 2025-08-06) — verified via `git log -L 1386,1393:src/Paramore.Brighter/OutboxProducerMediator.cs`.

## Root-Cause Hypothesis
**Hypothesis.** `_ => action` is an expression lambda whose body is the identifier `action`, not an invocation. Because a lambda body must be a *statement expression* to convert to a void-returning delegate, `_ => action` cannot convert to `Action<ResilienceContext>`, so overload resolution cannot pick `ResiliencePipeline.Execute(Action<ResilienceContext>, ResilienceContext)`. The only applicable candidate is the generic `ResiliencePipeline.Execute<TResult>(Func<ResilienceContext, TResult>, ResilienceContext)`, with `TResult` inferred as `System.Action`. The pipeline therefore executes a lambda that merely evaluates and returns the `Action` parameter; the returned `Action` is discarded and never invoked. No exception is thrown, so `ExecuteWithResiliencePipeline` returns `true` at `:1395` and every caller treats the no-op as success.

Overload set verified against the Polly version actually referenced — `Polly` 8.7.0 (`Directory.Packages.props:135`), resolving to `Polly.Core` 8.7.0. From `~/.nuget/packages/polly.core/8.7.0/lib/net8.0/Polly.Core.xml`, the relevant candidates taking `(callback, ResilienceContext)` are exactly:
- `Execute(System.Action{Polly.ResilienceContext}, Polly.ResilienceContext)`
- ``Execute`1(System.Func{Polly.ResilienceContext,``0}, Polly.ResilienceContext)``

**Reachability.** The claim in the issue checks out: a repo-wide grep for `ResilienceContext` across `src`, `tests` and `samples` finds only the two declarations above plus read-sites — there is **no assignment anywhere**, in product code, tests, or samples. The branch is dead today and becomes live the moment a library user (or Brighter itself) sets `RequestContext.ResilienceContext`, which the public setter permits. Note the asymmetry: the async twin at `:1417` is correct, so the failure would appear on the sync path only.

**Isolated or repeated?** Grepping for the `Execute(_ =>` shape finds three sites: the suspect line, and `src/Paramore.Brighter/Policies/Handlers/ResilienceExceptionPolicyHandler.cs:91` and `:96`. The latter two are `_pipeline.Execute(_ => base.Handle(request), Context.ResilienceContext)` — the body *is* an invocation, they bind to `Execute<TRequest>` and their result is returned, so they are correct. The slip appears isolated to `OutboxProducerMediator.cs:1388`.

**Existing coverage.** No test references `ResilienceContext` at all, so nothing exercises the broken branch. The sync helper is covered only incidentally via the `else` branch by the Post/Deposit/Clear tests under `tests/Paramore.Brighter.Core.Tests/CommandProcessors/`.

**Plausible test seam (for `/bugfix:test` — do not implement here).** The broken branch is reachable entirely through public API from the test assembly: construct `OutboxProducerMediator` + `CommandProcessor` as in `tests/Paramore.Brighter.Core.Tests/CommandProcessors/Post/When_Posting_A_Message_To_The_Command_Processor.cs:30-100`, set `ResilienceContext` on the concrete `RequestContext` (`Polly.ResilienceContextPool.Shared.Get()`), and pass that context to `Post`. `CommandProcessor.Post` forwards the caller's instance unchanged (`CommandProcessor.cs:680`; `InitRequestContext` at `:1531` mutates rather than replaces it), so the context survives to the mediator. No `InternalsVisibleTo` is needed. Caveat for whoever writes it: with the context set, the *deposit* no-ops before the send does, so an end-to-end "message on the `InternalBus`" assertion would fail for the earlier reason; the issue's advice — assert the delegate was invoked — is sound, and separate assertions for the write path (`AddToOutbox`) and the send path (`Dispatch`) will localise the failure better than one end-to-end assertion.

**Suggested fix from the issue — UNVERIFIED — to be proven or refuted in /bugfix:confirm:**
```csharp
resiliencePipeline.Execute(_ => action(), requestContext.ResilienceContext);
```
Reasoning to be checked, not assumed: `action()` is an invocation and therefore a statement expression, so `_ => action()` should convert to `Action<ResilienceContext>` and bind to the void overload. Open questions the confirm step must settle rather than inherit:
1. Whether that one-liner addresses the cause or only the most visible symptom — the unconditional `return true` at `:1395` means *any* future no-op in this helper is reported as success, and the missing `ResilienceContext` propagation in `RequestContext.CreateCopy()` (`RequestContext.cs:142-153`) is a second defect in the same feature that the one-liner does not touch.
2. Whether the sync branch should also honour `context.CancellationToken` the way the async branch does (`:1418` passes `context.CancellationToken` to `send`); the sync `Action` signature has no token, so the two branches remain asymmetric even after the fix.
3. Whether a Polly `ResilienceContext` supplied by a caller is safe to pass here at all (pool ownership, reuse across concurrent `Dispatch` iterations inside a single `ClearOutbox` loop) — the loop at `:1049-1083` would reuse one context per message.

## Confirmed Root Cause
**CONFIRMED.** `_ => action` binds to `Execute<TResult>(Func<ResilienceContext, TResult>, ResilienceContext)` with `TResult = System.Action`; the delegate is returned and discarded, never invoked. Proven empirically end to end against the real `Paramore.Brighter` assembly.

`src/Paramore.Brighter/OutboxProducerMediator.cs:1388`:

```csharp
resiliencePipeline.Execute(_ => action, requestContext.ResilienceContext);
```

The lambda body `action` is an identifier expression, not an invocation. It cannot convert to `Action<ResilienceContext>` (a void-returning delegate requires a statement-expression body), so overload resolution selects the generic `Execute<TResult>` with `TResult` inferred as `System.Action`. Polly runs a callback whose entire work is *evaluating a local variable*; the returned `Action` is discarded. No exception can arise, so `:1395 return true;` is reached and every caller treats the no-op as success.

The compiler emits **zero warnings** for this shape (verified: `dotnet build` on the probe with warnings enabled → `0 Warning(s)`), which is why it survived review.

## Evidence

- [x] **Polly-level probe** — scratchpad console app, Polly 8.7.0, `AddRetry` linear 3x (the exact `AddBrighterDefault` strategy from `src/Paramore.Brighter/Extensions/ResiliencePipelineRegistryExtensions.cs`). Actual output:

```
1a. Execute(_ => action, ctx)    -> invoked = False
1b. Execute(_ => action(), ctx)  -> invoked = True
2.  Shape A with throwing action -> exception seen = False
3.  Shape B with throwing action -> attempts = 4 (retry engaged), exception propagates
4.  Reuse one ctx over 3 sequential sync Execute -> invocations = 3, error = none
5.  Sync Execute on ctx last used async -> invocations = 1, error = none
6.  ReferenceEquals(returned, next Get) = True  (pool recycles the instance)
6b. Execute on a returned ctx -> invocations = 1, error = none
7.  Pre-cancelled ctx token, Execute(_ => action()) -> invocations = 0, outcome = OperationCanceledException
```

  A forced explicit binding `pipeline.Execute<Action>(_ => act, ctx)` compiles and returns `System.Action` — i.e. the generic overload is genuinely the one the compiler picks.

- [x] **Red repro, end to end** against the built `Paramore.Brighter.dll` — real `CommandProcessor`, `OutboxProducerMediator<Message, CommittableTransaction>`, `InMemoryOutbox`, `InMemoryMessageProducer`, `InternalBus`, `AddBrighterDefault()` registry; a near-clone of `tests/Paramore.Brighter.Core.Tests/CommandProcessors/Post/When_Posting_A_Message_To_The_Command_Processor.cs`. The **only** difference from the control is `new RequestContext { ResilienceContext = ResilienceContextPool.Shared.Get() }`. Actual output:

```
[control  ] Post threw = False ; messages on broker = 1 ; outbox count = 1
[bug: Post] Post threw = False ; messages on broker = 0 ; outbox count = 0
[bug: Dep ] DepositPost threw = False ; returned id = 299d9a10-... ; outbox count = 0
[bug: Clr ] after deposit: outbox count = 1, broker = 0
[bug: Clr ] ClearOutbox threw = False ; broker = 0 ; still-outstanding (undispatched) = 1
```

  The assertion that fails today is the standard one in that fixture — `Assert.True(_internalBus.Stream(new RoutingKey(Topic)).Any())` — with a `ResilienceContext` set on the `RequestContext`.

  With logging attached, the bug run for `Post` emits:

```
info: Save request: MyCommand 2cd2bce8-...
dbug: Outbox outstanding message count is: 0
fail: Message(s) with Id(s) 2cd2bce8-... not found in Outbox; dispatching found messages
```

  and for the `ClearOutbox` case:

```
info: Decoupled invocation of message: Topic:MyCommand Id:b041c389-...   <- claims a send
(no "Sent message" line; message left undispatched)
```

### Per-consequence verification — two triage claims were REFUTED

| Call site | Triage claim | Verdict |
|---|---|---|
| `AddToOutbox` `:321-327`, guard `:329-330` | `ChannelFailureException` guard defeated | **CONFIRMED.** `written == true` unconditionally; `DepositPost` returned a valid `Id` with `outbox count = 0`. The id is handed back to the caller as if durably stored. |
| `EndBatchAddToOutbox` `:631-637`, guard `:638-639` | same | **CONFIRMED by construction** — identical shape; `CommandProcessor.cs:899` (`InitRequestContext`) + `:917` pass the *same* `context` instance. Reachable via `DepositPost(IEnumerable<TRequest>, …)`. |
| `Dispatch` `:1066-1069` (send) + `:1072-1075` (`MarkDispatched`) | both no-op | **CONFIRMED.** `ClearOutbox` returned, broker empty, message still outstanding. Note the compounding: `sent` is `true` (false positive), so `MarkDispatched` is *attempted* — and it too no-ops, which is the only thing preventing the outbox being poisoned with a dispatched marker for an unsent message. Two bugs cancelling. |
| `Dispatch` `:1060-1062` (confirmation producers) | send no-ops | **CONFIRMED.** The result is discarded anyway; the message is never sent and no confirmation callback fires. |
| `CallViaExternalBus` `:344-347` | RPC send no-ops | **CONFIRMED reachable** — `CommandProcessor.cs:1460` passes the caller's raw `requestContext` (not the copied `context`). `Call<T,TResponse>` then blocks on the reply channel until timeout and returns `null`. This site already discards the boolean, so it was never protected. |
| `ConfigurePublisherCallbackMaybe` `:976-978` (`MarkDispatched`) | claimed affected | ⛔ **REFUTED.** `:974` passes `dispatchedContext`, a `CreateCopy()` — and `CreateCopy()` (`RequestContext.cs:142-153`) does **not** copy `ResilienceContext`. This site always takes the `else` branch at `:1391` and works correctly. Same for the async twin at `:877-882`. Triage over-claimed. |
| "nothing is logged" | | ⛔ **PARTIALLY REFUTED.** For `Post`, `ClearOutbox` logs `Log.OutboxMessagesNotFound` at **Error** (`:385-389`). For a bare `DepositPost`, and for `ClearOutbox` of an already-deposited message, it is genuinely silent — and the latter logs `Decoupled invocation of message`, *implying* a send. |

### Reachability — settled

**Fully reachable through the public API; `CreateCopy()` does not intervene on any affected path.**

`CommandProcessor.InitRequestContext` (`CommandProcessor.cs:1531-1542`) returns `requestContext ?? _requestContextFactory.Create()` — **the caller's own instance**, mutated in place. It never copies. Therefore:

- `Post` (`:679`) → `CallDepositPost` → `DepositPost<TRequest,TTransaction>` (`:826`) → `CallAddToOutbox` (`:1259-1268`, reflection, passes `context` through) → `mediator.AddToOutbox` — **instance preserved**.
- `Post` (`:679`) → `ClearOutbox` (`:1353`) → `mediator.ClearOutbox` → `Dispatch` — **instance preserved**.
- `DepositPost(IEnumerable<…>)` (`:899`, `:917`) → `EndBatchAddToOutbox` — **instance preserved**.
- `Call<T,TResponse>` (`:1460`) → `CallViaExternalBus` — **instance preserved** (raw `requestContext`, not even `context`).

`CreateCopy()` strips `ResilienceContext` only at `OutboxProducerMediator.cs:877`, `:974` and `PipelineBuilder.cs:189,234` — none on the broken sync outbox path.

No test-only plumbing is needed: `RequestContext.ResilienceContext` has a public setter (`RequestContext.cs:107`), and `CommandProcessor.Post` takes the concrete `RequestContext`, so the get-only `IRequestContext.ResilienceContext` (`IRequestContext.cs:97`) is not an obstacle.

### Suggested-Fix Assessment

**CONFIRMED** for the one-liner:

```csharp
resiliencePipeline.Execute(_ => action(), requestContext.ResilienceContext);
```

Necessary, sufficient and minimal for the confirmed cause. Probe lines 1b/3 show it invokes the action *and* propagates exceptions, so the retry strategy engages and the `catch` at `:1396` fires correctly.

Triage's three open questions, settled with evidence:

1. **Is the unconditional `return true` at `:1395` a second defect?** **No — not once the lambda is fixed.** `return true` is correct precisely because `Execute` propagates any exception from an *invoked* action (probe 3: `attempts = 4`, exception propagates). The `true` was only wrong because the action never ran. No defensive change needed.
2. **Should the sync branch honour `context.CancellationToken`?** **It already will, for free.** Probe 7: with a pre-cancelled `ResilienceContext`, `Execute(_ => action(), ctx)` threw `OperationCanceledException` *without invoking* the action. Polly checks the context token at the strategy boundary. The inner `Action` has no token parameter and cannot observe it mid-flight — but neither does the existing non-context branch at `:1392`, so there is no parity regression. No signature change belongs in this fix. (The resulting `OperationCanceledException` is swallowed by the `catch` at `:1396` → `return false` → `ChannelFailureException`; that matches the async twin, so leave it.)
3. **Is passing a caller-supplied `ResilienceContext` safe here?** **Yes, for Brighter's usage.** Probe 4: reusing one context across three sequential `Execute` calls works cleanly (Polly re-initialises per execution) — so the per-message `Dispatch` loop at `:1049-1083` and the Send→MarkDispatched pair at `:1066-1075` are fine. Probe 5: a context previously used for an async execution works for a subsequent sync one. Brighter correctly never calls `ResilienceContextPool.Shared.Return`, so pool ownership stays with the caller. Probe 6 shows the pool *does* recycle instances, so a caller who returns the context while Brighter still holds it would corrupt an unrelated operation — caller misuse, not a Brighter defect.

## Scope Notes

**In this fix — one line, nothing else:**
- `src/Paramore.Brighter/OutboxProducerMediator.cs:1388`.

**Separate issues — do NOT widen this fix:**

1. **`RequestContext.CreateCopy()` silently drops `ResilienceContext`** — `src/Paramore.Brighter/RequestContext.cs:142-153`. It copies `Policies`, `ResiliencePipeline`, `FeatureSwitches`, `OriginatingMessage`, `InstrumentationOptions`, `Span` — but not `ResilienceContext`. Observable inconsistency at `src/Paramore.Brighter/PipelineBuilder.cs:189` and `:234`: `Publish` to *one* observer passes the original context (so `ResilienceExceptionPolicyHandler.cs:91,96` sees the caller's `ResilienceContext`), while `Publish` to *two or more* observers silently loses it and takes a different path. This may be **deliberate** — one Polly `ResilienceContext` shared across parallel handler pipelines is not safe — so it needs a design decision, not a reflex copy. Either way the current state is undocumented and untested.
2. **Async call sites discard the helper's `ResilienceContext`-derived cancellation token.** `ExecuteWithResiliencePipelineAsync` (`:1418`) carefully forwards `context.CancellationToken` into `send`, but five of seven call sites throw the parameter away and close over the outer `cancellationToken`: `:659-661` (`EndBatchAddToOutboxAsync`), `:1132-1134` and `:1145-1150` (`BulkDispatchAsync`), `:1202-1204` and `:1213-1218` (`DispatchAsync`). Only `:278-284` (`AddToOutboxAsync`) uses `ct` correctly. Not a no-op, but it defeats the async context branch's purpose. Same feature (`c536f9a39`), same class of slip.
3. **`CallViaExternalBus` (`:344-347`) discards the boolean result entirely.** Even with a working pipeline, a send failure is logged and swallowed, and `CommandProcessor.Call` then blocks until timeout. Pre-existing and orthogonal, but adjacent.
4. **A doc-comment on `RequestContext.ResilienceContext` (`RequestContext.cs:102-107`)** stating Brighter does not take ownership of a pooled context. Not a code change.
5. **No analyzer/warning coverage for the `_ => delegate` shape** — the compiler is silent. Worth an `.editorconfig`/analyzer rule, or at minimum a note in `CLAUDE.md`.

**Verified NOT affected:** `ResilienceExceptionPolicyHandler.cs:91,96` and `ResilienceExceptionPolicyHandlerAsync.cs:95,102` — their lambda bodies are invocations. A repo-wide grep for `ResilienceContext` found no other `Execute(_ => …)` occurrence.

## Regression Test
`tests/Paramore.Brighter.Core.Tests/CommandProcessors/Post/When_Posting_A_Message_With_A_Resilience_Context_Should_Still_Send_The_Message.cs`

`CommandProcessorPostWithResilienceContextTests.When_Posting_A_Message_With_A_Resilience_Context_Should_Still_Send_The_Message`

A trimmed clone of `When_Posting_A_Message_To_The_Command_Processor`, differing in exactly one line —
`ResilienceContext = ResilienceContextPool.Shared.Get()` on the `RequestContext`. Two assertions:
the `InternalBus` stream pins the **send**, `_outbox.Get` pins the **write**. Both are needed: per the
confirmed diagnosis the write no-ops *first*, so the outbox assertion guards against a fix that
repairs only the visible half. Asserting the delegate's observable effect (not "did the call throw")
is the point — a no-throw assertion passes against the broken code.

**RED observed** on `net9.0` and `net10.0`:
`Assert.True() Failure / Expected: True / Actual: False` at the broker assertion — nothing sent.

**Control run (right-reason proof):** commenting out *only* the `ResilienceContext` line in the same
fixture gives `Passed! Failed: 0, Passed: 1` on both TFMs. The single differing line is what flips it,
so the failure is the defect and not a fixture defect. Test restored to RED afterwards.

## Fix
`src/Paramore.Brighter/OutboxProducerMediator.cs:1388` — invoke the delegate instead of returning it.

```diff
-                    resiliencePipeline.Execute(_ => action, requestContext.ResilienceContext);
+                    resiliencePipeline.Execute(_ => action(), requestContext.ResilienceContext);
```

One line, nothing else. `action()` is an invocation and therefore a statement expression, so the
lambda converts to `Action<ResilienceContext>` and binds to the void `Execute` overload; the work
now runs inside the pipeline and any exception propagates to the `catch` at `:1396`, so the
`return true` at `:1395` becomes truthful.

Deliberately **not** changed, per the Confirm findings:
- `return true` at `:1395` — correct once the action actually runs and can throw.
- The sync branch's cancellation handling — Polly already honours `context.CancellationToken` at the
  strategy boundary (probe 7: a pre-cancelled context threw with `invocations = 0`).
- The five items in *Scope Notes* — separate issues, not this fix.

**GREEN**: the regression test passes on `net9.0` and `net10.0`.
