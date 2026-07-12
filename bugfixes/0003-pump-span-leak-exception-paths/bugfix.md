# Bugfix: Pump observability spans (pumpSpan, processSpan) leak on exception paths in Reactor/Proactor

**Linked Issue**: #4090
**Status**: Verified

## Symptom
The message pumps start two `Activity` spans whose lifetimes are not wrapped in `try/finally`, so on certain exception paths they are started but never ended ("leaked"):

- `pumpSpan` (the `Begin` span for the whole pump run) is created at the top of the run method and ended only after the receive loop exits normally. If anything throws out of the loop, the end call is skipped.
- `processSpan` (the per-message `Process` span) is created on the line *before* its `try`, so if creation itself throws after the underlying activity has been started, the `finally` that ends it never engages.

Observed effect: leaked activities stay "active"; `Activity.Current` can be left pointing at the leaked span, polluting the parent context of subsequent spans; exporters see skewed/missing durations; `messaging.process.duration` is mis-recorded; memory is held until GC.

Reproduction (conceptual, from the code): the "message is null" branch deliberately throws (`throw new Exception(NoMessageReceivedDescription)`), which unwinds past the pumpSpan end call — leaking the pump span on every such occurrence.

## Suspected Location
Verified against current source.

**`src/Paramore.Brighter.ServiceActivator/Reactor.cs`** (`Run`):
- `Reactor.cs:97` — `pumpSpan` created (`Tracer?.CreateMessagePumpSpan(... Begin ...)`).
- `Reactor.cs:99` — `do` loop start; `Reactor.cs:361` — `} while (true);`. No `try/finally` surrounds the loop.
- `Reactor.cs:364` — `Tracer?.EndSpan(pumpSpan)`, *after* the loop, outside any `try/finally`.
- `Reactor.cs:144-150` — "message is null" branch that `throw`s (line 149) and unwinds out of `Run`.
- `Reactor.cs:185` — `processSpan` created *before* the `try` that begins at `Reactor.cs:186`.
- `Reactor.cs:354-357` — `finally { Tracer?.EndSpan(processSpan); }` (the end call that is skipped if creation on line 185 throws).
- Reference pattern (the #4085 receive span, correctly bounded): `try` at `Reactor.cs:113` with `finally { Tracer?.EndSpan(receiveSpan); }` at `Reactor.cs:180-183`. Note `receiveSpan` is initialized to `null` (line 112) *before* the `try`, and assigned *inside* it (line 117) — so the create is inside the guarded region.

**`src/Paramore.Brighter.ServiceActivator/Proactor.cs`** (`EventLoop`) — identical shape:
- `Proactor.cs:138` — `pumpSpan` created.
- `Proactor.cs:140` / `Proactor.cs:396` — `do` / `while (true)`; no surrounding `try/finally`.
- `Proactor.cs:399` — `Tracer?.EndSpan(pumpSpan)` after the loop.
- `Proactor.cs:185-191` — "message is null" branch that `throw`s (line 190).
- `Proactor.cs:226` — `processSpan` created before the `try` at `Proactor.cs:227`.
- `Proactor.cs:389-392` — `finally { Tracer?.EndSpan(processSpan); }`.
- Reference receive-span pattern: `try` at `Proactor.cs:154`, `finally` at `Proactor.cs:221-224`.

**`src/Paramore.Brighter/Observability/BrighterTracer.cs`**:
- `CreateMessagePumpSpan` — `BrighterTracer.cs:548-582`; starts the activity at `BrighterTracer.cs:575-576` and sets `Activity.Current` at `579`.
- `CreateSpan(MessagePumpSpanOperation, Message, …)` (the Process overload) — `BrighterTracer.cs:154-220`; underlying activity started via `StartConsumerActivity` at `BrighterTracer.cs:201` (which calls `ActivitySource.StartActivity`), then post-start work that can throw: `activity.TraceStateString = traceState` (`211`), `message.Header.Baggage.Add(...)` / `OpenTelemetry.Baggage.SetBaggage(...)` (`213-215`), `Activity.Current = activity` (`217`).

## Root-Cause Hypothesis
The two spans are created outside the `try/finally` that owns their end call, so any throw between create and end leaks the span. Stated falsifiably:

1. **pumpSpan**: Because `EndSpan(pumpSpan)` sits after the `do/while` loop with no `try/finally` around the loop body (Reactor.cs:97 → 364; Proactor.cs:138 → 399), any exception that escapes the loop — concretely the `throw` in the "message is null" branch (Reactor.cs:149 / Proactor.cs:190) — skips `EndSpan`. Prediction: a unit test that drives the pump to the null-message branch (or otherwise forces a throw out of the loop) and asserts the Begin activity was ended will currently fail.

2. **processSpan**: Because `CreateSpan` is invoked on the line *before* the `try` (Reactor.cs:185 vs try at 186; Proactor.cs:226 vs try at 227), if `CreateSpan` throws *after* `ActivitySource.StartActivity` has already returned a started activity, the `finally` at Reactor.cs:354-357 / Proactor.cs:389-392 never runs and the activity leaks. Prediction: a test where the post-start steps in `CreateSpan` throw (or `StartActivity` succeeds but a subsequent step in the create path throws) will leave an unended Process activity.

   Caveat to verify in /bugfix:confirm: the issue states the throwing serialization (`JsonSerializer.Serialize(message.Header, …)`) runs *after* `StartActivity`. In the current Process overload the tag collection — including `message.HeaderJson` serialization (BrighterTracer.cs:191) — is built *before* `StartConsumerActivity` (line 201), so that specific serialize-after-start window does not exist here. The real post-start throw window is lines 211-217 (`TraceStateString`, baggage add/set, `Activity.Current`). The leak mechanism holds; the exact throwing statement differs from the issue text and should be pinned down precisely.

**Suggested fix (from the issue — UNVERIFIED — to be proven or refuted in /bugfix:confirm):** wrap both spans' lifetimes in `try/finally` so `EndSpan` runs on every exit path. For `pumpSpan`, put the loop body inside a `try` whose `finally` calls `EndSpan(pumpSpan)`. For `processSpan`, move `CreateSpan` inside an outer `try` whose `finally` calls `EndSpan(processSpan)` — mirroring how the #4085 receive span is bounded (declare the variable as `null` before the `try`, assign inside it, end in `finally`).

## Confirmed Root Cause
**Verdict: CONFIRMED** (with an important scope correction on the processSpan fix).

`pumpSpan` is created at the top of the run method and ended only by a single `EndSpan` call placed **after** the `do/while` loop, with no `try/finally` around the loop. Any exception that unwinds out of the loop skips that `EndSpan`, leaking the `Begin` Activity. The most direct trigger is the message-is-null branch, which deliberately `throw`s an uncaught exception (Reactor.cs:149 / Proactor.cs:190). The throw is not caught anywhere between the loop body and method exit, so it propagates past the post-loop `EndSpan(pumpSpan)` (Reactor.cs:364 / Proactor.cs:399). Symmetric in both pumps. This is a **deterministic, real leak**.

The `processSpan` create-before-`try` placement is a genuine but **theoretical** structural defect, and — critically — **the issue's suggested fix for it does not work** (see Suggested-Fix Assessment).

## Evidence
- [x] Code-trace (red repro optional — observability code; trace is authoritative).

**pumpSpan leak (Reactor — deterministic):**
- Created `Reactor.cs:97`; `do` at `:99`; `} while(true)` at `:361`; `EndSpan(pumpSpan)` at `:364`, outside any try/finally — no `try` encloses lines 99–361.
- The inner `try` (113–179) has a `finally` (180–183) that ends **only** `receiveSpan`. The message-is-null branch (144–150) `throw`s at `:149`; after the finally ends receiveSpan, the exception keeps unwinding with no `catch` between line 149 and method exit → bypasses `EndSpan(pumpSpan)` at `:364`.
- Receive `catch (Exception)` at `Reactor.cs:137–142` does **not** rethrow/continue, so a receive failure falls through to the null-check at `:144` and the throw at `:149`. Both null-broker-return and receive-exception converge on the same leaking throw.
- Additional escape paths past `EndSpan(pumpSpan)`: handler calls (Nack/Dispose/Requeue) and `AcknowledgeMessage(message)` at `Reactor.cs:359` which is **outside** the process try/finally (186–357).

**pumpSpan leak (Proactor — symmetric):** created `:138`; `do` `:140`; `} while(true)` `:396`; `EndSpan(pumpSpan)` `:399`; throw at `:190` inside try 154–220 (finally 221–224 ends receiveSpan only); receive `catch` 178–183 does not rethrow; `await Acknowledge(message)` at `:394` outside process try/finally. `Performer.cs:62–68` runs `Run()` on a long-running Task and never catches or ends spans → escaped exception unobserved, pumpSpan stays open.

**EndSpan is null-safe:** `BrighterTracer.cs:793` `if (span is null) return;` — wrapping in finally is safe even when Tracer/span is null.

**processSpan structural defect (theoretical):** `Activity? processSpan = Tracer?.CreateSpan(...)` at `Reactor.cs:185` is created **before** the `try` at `:186` (finally 354–357). Inside `CreateSpan` (BrighterTracer.cs:154–220) the activity goes live at `StartConsumerActivity` (`:201`); statements after that which could throw: `TraceStateString` (`:211`), `Baggage.Add` (`:213–214`), `Baggage.SetBaggage` (`:215`), `Activity.Current` (`:217`). A throw there exits `CreateSpan` before `processSpan` is assigned and before the caller's `try` → live activity leaks.

**Triage caveat verified correct:** header/`HeaderJson` serialization is at `BrighterTracer.cs:191`, **before** `StartConsumerActivity` (`:201`). The issue's "JsonSerializer runs after StartActivity" framing is wrong; the only genuine post-start throw window is 211–217.

## Scope Notes
- **The processSpan window is NOT closed by mirroring the receiveSpan pattern.** Declaring `processSpan = null` before the try and assigning inside does nothing: the leak only occurs when `CreateSpan` throws *after* starting the activity, in which case the assignment never completes, `processSpan` stays `null`, and the finally's `EndSpan(null)` is a no-op while the started activity is still orphaned. In current code there is *zero gap* between a successful `CreateSpan` return (`:185`) and the `try` (`:186`), so a successfully-created processSpan **cannot leak today**. The real remedy is to make `CreateSpan` end its own activity on a post-start throw — an internal `try/finally` around `BrighterTracer.cs:211–217`.
- **Same class of bug elsewhere (low risk):** `receiveSpan` is structurally correct in the caller (null before try, assign inside, end in finally — Reactor.cs:111/117/182; Proactor.cs:152/158/223) but shares the same residual post-start window inside `CreateReceiveSpan`; its only post-start statement is `Activity.Current` (BrighterTracer.cs:255–256), so risk is negligible. `CreateMessagePumpSpan` (548–582) post-start work is only `Activity.Current` (`:578–579`) — negligible.
- **Underlying class:** any `Create*Span` doing post-`StartActivity` work that can throw can orphan an Activity regardless of caller try/finally; `CreateSpan` (211–217) is the worst offender. Centralizing post-start work under an internal try/finally in BrighterTracer would fix processSpan, receiveSpan and pumpSpan-internal risk uniformly.
- **`CreateMessagePumpExceptionSpan`** (594–632) is not invoked in Run/EventLoop — not a live leak site here.
- **Parity:** Reactor and Proactor are fully symmetric for both defects; any fix must be applied to both identically. No other pump variants found (`CreateMessagePumpSpan`/`EndSpan` referenced only from these two source files).

### Approved fix scope (Confirm gate)
1. **pumpSpan** — wrap the receive loop in `try { ... } finally { Tracer?.EndSpan(pumpSpan); }` in **both** `Reactor.Run` and `Proactor.EventLoop`. Let the exception propagate (do not swallow), preserving current pump-shutdown semantics.
2. **processSpan** — fix correctly **inside `BrighterTracer.CreateSpan`** (Process overload): wrap the post-start work (BrighterTracer.cs:211–217) in an internal `try/finally` so the activity ends itself if a post-start statement throws. Moving the caller's try boundary is explicitly **not** the fix.

Out of scope (deferred): centralizing post-start handling for `CreateReceiveSpan` / `CreateMessagePumpSpan` (negligible risk).

## Regression Test
Three failing regression tests (all RED for the right reason — the `begin`/`process` span is absent from the InMemory exporter because the throw skips the `EndSpan`/leaks the started activity):

1. **pumpSpan, Reactor** — `tests/Paramore.Brighter.Core.Tests/Observability/MessageDispatch/When_The_Reactor_Loop_Throws_Close_The_Pump_Span.cs`. Drives the pump down the message-is-null throw via a new test double `tests/Paramore.Brighter.Core.Tests/MessageDispatch/TestDoubles/NullReturningChannel.cs` (Receive returns null). Asserts the `begin` span is exported despite the throw.
2. **pumpSpan, Proactor (parity)** — `tests/Paramore.Brighter.Core.Tests/Observability/MessageDispatch/When_The_Proactor_Loop_Throws_Close_The_Pump_Span.cs`. Uses new test double `tests/Paramore.Brighter.Core.Tests/MessageDispatch/TestDoubles/NullReturningChannelAsync.cs` (ReceiveAsync returns null).
3. **processSpan (tracer-level)** — `tests/Paramore.Brighter.Core.Tests/Observability/MessageDispatch/When_Create_Span_Throws_After_Starting_Close_The_Span.cs`. A message whose `CorrelationId` value (`bad=value`) fails `Baggage` value validation makes `BrighterTracer.CreateSpan` throw at the baggage step (line 214) — *after* `StartConsumerActivity` (line 201). Asserts the Process activity is still ended/exported. Current RED: exporter is empty (`[]`) — the started activity leaked.

RED reasons verified by running each test; `Assert.Throws` confirms the exception genuinely escapes in all three.

## Fix
Minimal change scoped to the confirmed cause, applied symmetrically across both pumps plus the tracer.

1. **pumpSpan — `src/Paramore.Brighter.ServiceActivator/Reactor.cs` (`Run`)**: wrapped the receive loop (and the trailing `FinishedRunningMessageLoop` log) in `try { ... } finally { Tracer?.EndSpan(pumpSpan); }`. `EndSpan(pumpSpan)` now runs on every exit path — including the message-is-null throw — and the exception still propagates (try/finally, not catch), preserving pump-shutdown semantics.
2. **pumpSpan — `src/Paramore.Brighter.ServiceActivator/Proactor.cs` (`EventLoop`)**: identical `try/finally` wrap (parity).
3. **processSpan — `src/Paramore.Brighter/Observability/BrighterTracer.cs` (`CreateSpan`, Process overload)**: wrapped the post-start enrichment (`TraceStateString`, baggage add/set, `Activity.Current`) in `try { ... return activity; } catch (Exception ex) { activity.SetStatus(Error, ex.Message); EndSpan(activity); throw; }`. If enrichment throws after the activity has started, the activity is now ended (status Error) and `Activity.Current` restored by `EndSpan`, instead of leaking — the caller's pre-`try` `CreateSpan` call site can no longer orphan a started activity. The normal path is unchanged: the live activity is returned for the caller to end.

Out of scope (deferred, negligible risk): `CreateReceiveSpan` / `CreateMessagePumpSpan` post-start hardening.

**Tests**: all three regression tests green on net9.0 and net10.0. Related suites (`Observability`, `MessageDispatch`) — 167 passed, 0 failed, 2 skipped (unrelated DB-backed clear tests).
