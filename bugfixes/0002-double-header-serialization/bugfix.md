# Bugfix: Message header serialized twice per message in pump observability hot path

**Linked Issue**: #4089
**Status**: Verified

## Symptom
For every *serviceable* message dispatched through the message pump, `JsonSerializer.Serialize(message.Header, JsonSerialisationOptions.Options)` runs **twice**:
1. Once when the receive span is enriched, and
2. Once when the process span is created.

Expected: in the steady-state pump path a serviceable message should incur **at most one** full-header serialization. Before the #4085 fix it was a single serialization; the split into a receive span + a process span regressed it to two.

How it manifests: a per-message hot-path cost. `MessageHeader` is reflection-serialized in full (including the `Bag` dictionary and `Baggage`), so the duplication doubles both reflection-driven CPU and allocations on the consumer thread for the default `InstrumentationOptions.All` (which sets the `Messaging` flag — `src/Paramore.Brighter/Observability/InstrumentationOptions.cs:39,43`). The same double-emission applies to the `MessageBody` (`RequestBody` flag), though strings don't pay the reflection cost.

There is no functional/correctness defect — both spans carry correct tags. It is purely a performance/allocation regression.

## Suspected Location
The duplication is the two serialization sites firing back-to-back per serviceable message:

- `src/Paramore.Brighter/Observability/BrighterTracer.cs:292` — `EnrichReceiveSpan`, inside the `if (options.HasFlag(InstrumentationOptions.Messaging))` block (begins `src/Paramore.Brighter/Observability/BrighterTracer.cs:286`): `span.AddTag(BrighterSemanticConventions.MessageHeaders, JsonSerializer.Serialize(message.Header, JsonSerialisationOptions.Options))`. The `MessageBody` (RequestBody) tag is added at `src/Paramore.Brighter/Observability/BrighterTracer.cs:296-297`.
- `src/Paramore.Brighter/Observability/BrighterTracer.cs:191` — `CreateSpan(MessagePumpSpanOperation, Message, ...)` (the process span; method begins `src/Paramore.Brighter/Observability/BrighterTracer.cs:154`), inside the `Messaging` block (begins `:184`): `tags.Add(BrighterSemanticConventions.MessageHeaders, JsonSerializer.Serialize(message.Header, JsonSerialisationOptions.Options))`. The `MessageBody` tag is added at `:196-199`.

Per-message call path that fires both (identical pattern in Reactor and Proactor):

- Reactor: `CreateReceiveSpan` at `src/Paramore.Brighter.ServiceActivator/Reactor.cs:117` (no header serialization there — see `BrighterTracer.cs:231-259`), then `EnrichReceiveSpan` at `src/Paramore.Brighter.ServiceActivator/Reactor.cs:119` (serialization #1), then for serviceable messages `CreateSpan(MessagePumpSpanOperation.Process, ...)` at `src/Paramore.Brighter.ServiceActivator/Reactor.cs:185` (serialization #2).
- Proactor: `CreateReceiveSpan` at `src/Paramore.Brighter.ServiceActivator/Proactor.cs:158`, `EnrichReceiveSpan` at `src/Paramore.Brighter.ServiceActivator/Proactor.cs:160`, `CreateSpan(...Process...)` at `src/Paramore.Brighter.ServiceActivator/Proactor.cs:226`.

MT_UNACCEPTABLE handling (distinguishes serviceable vs unparseable):
- Reactor: `src/Paramore.Brighter.ServiceActivator/Reactor.cs:160-169` — on `MT_UNACCEPTABLE` it sets the receive-span error status and `continue`s, so it `continue`s **before** `CreateSpan(Process)` at `:185`. Hence the unacceptable path serializes the header only **once** (in `EnrichReceiveSpan`); that receive span is the *only* span carrying `MessageHeaders`/`MessageBody` for a rejected message.
- Proactor: mirror at `src/Paramore.Brighter.ServiceActivator/Proactor.cs:201` (+ the `MT_NONE` empty-queue `continue` at `Reactor.cs:153` / `Proactor.cs:194`).

Regression origin: commit `5cb3410c1` "fix: receive span covers broker call; add process span (#4085) (#4091)" (2026-05-13) introduced `EnrichReceiveSpan` and the receive/process span split (confirmed via `git log -S "EnrichReceiveSpan"`). Before that, the message pump emitted a single consumer span and therefore a single header serialization.

## Root-Cause Hypothesis
**Hypothesis (falsifiable):** Commit `5cb3410c1` (#4085) split the single consumer span into a broker-latency *receive* span and a dispatch *process* span. Both spans independently carry the full `MessageHeaders` tag, and each computes it by calling `JsonSerializer.Serialize(message.Header, JsonSerialisationOptions.Options)` (`BrighterTracer.cs:292` for receive, `BrighterTracer.cs:191` for process). Because both `EnrichReceiveSpan` (`Reactor.cs:119` / `Proactor.cs:160`) and `CreateSpan(Process)` (`Reactor.cs:185` / `Proactor.cs:226`) execute for every serviceable message under the default `Messaging` flag, the full reflection-driven header serialization runs exactly twice per serviceable message — where it previously ran once. The `MessageBody` (RequestBody) tag has the same double-emission shape but is a cheaper string copy.

This hypothesis is falsifiable: it predicts that for a serviceable message with `Messaging` enabled, `JsonSerializer.Serialize(message.Header, ...)` is invoked exactly twice (provable by a serialization counter / benchmark allocation profile), and that for an `MT_UNACCEPTABLE` message it is invoked exactly once. If either count differs, the hypothesis is wrong.

The issue proposes several remediation approaches — **all UNVERIFIED — to be proven or refuted in /bugfix:confirm**:
1. **Cache the serialized header string for one message lifetime** and reuse it across both spans. UNVERIFIED — to be proven or refuted in /bugfix:confirm.
2. **Lazily store the serialized header on `Message`/`MessageHeader`** (memoize on first serialization). UNVERIFIED — to be proven or refuted in /bugfix:confirm.
3. **Skip the `MessageHeaders` tag on the receive span entirely.** UNVERIFIED — to be proven or refuted in /bugfix:confirm (note: would violate the acceptance criterion that MT_UNACCEPTABLE receive spans still carry MessageHeaders, since rejected messages have no process span).
4. **Conditional enrichment by `MessageType`** — only serialize headers onto the receive span when the message is `MT_UNACCEPTABLE` (issue author's lean), letting the process span carry them for serviceable messages. UNVERIFIED — to be proven or refuted in /bugfix:confirm. Open questions to resolve there: `EnrichReceiveSpan` currently does not receive/inspect `MessageType` to branch on it (it unconditionally serializes at `BrighterTracer.cs:292`), and the receive-span tags would then differ by message type — confirm no observability test asserts `MessageHeaders` on the receive span for serviceable messages (e.g. `tests/Paramore.Brighter.Core.Tests/Observability/MessageDispatch/When_A_Message_Is_Dispatched_It_Should_Begin_A_Span.cs` and `When_There_Is_An_Unacceptable_Messages_Close_The_Span.cs`).

## Confirmed Root Cause
**CONFIRMED.** The receive span and the process span each independently call `JsonSerializer.Serialize(message.Header, JsonSerialisationOptions.Options)`. Both run under the default `InstrumentationOptions.Messaging` flag, and both fire on the serviceable-message path of the Reactor and Proactor pumps. Before #4085 there was a single consumer span and thus a single serialization; commit `5cb3410c1` split it into receive + process, doubling the work. Full header is serialized exactly **2× per serviceable message**, **1× for MT_UNACCEPTABLE**.

## Evidence
Code-trace (documented; read-only confirm step, no red repro written):

1. **Two serialization sites confirmed at the exact hypothesized lines:**
   - `BrighterTracer.cs:191` — in `CreateSpan(MessagePumpSpanOperation, Message, ...)`, inside `if (options.HasFlag(InstrumentationOptions.Messaging))` (opened :184): `tags.Add(BrighterSemanticConventions.MessageHeaders, JsonSerializer.Serialize(message.Header, JsonSerialisationOptions.Options))`. MessageBody (RequestBody) at :196-198.
   - `BrighterTracer.cs:292` — in `EnrichReceiveSpan(Activity?, Message, ...)`, inside `if (options.HasFlag(InstrumentationOptions.Messaging))` (opened :286): `span.AddTag(BrighterSemanticConventions.MessageHeaders, JsonSerializer.Serialize(message.Header, JsonSerialisationOptions.Options))`. MessageBody at :296-297.
   - Both serialize the same `message.Header` with the same options. Line numbers exact.

2. **Both fire per serviceable message (Reactor):** `EnrichReceiveSpan` at `Reactor.cs:119` runs for every received non-null message (right after `Channel.Receive`, before any MessageType branch). MT_NONE continue `:156`, MT_UNACCEPTABLE continue `:168`, MT_QUIT break `:177` — all inside the `try` ending `finally { Tracer?.EndSpan(receiveSpan); }` (:180-183). `CreateSpan(Process)` at `Reactor.cs:185` runs only after, i.e. only for serviceable messages ⇒ 2 serializations.

3. **Proactor identical in shape:** `EnrichReceiveSpan` `Proactor.cs:160`; MT_NONE continue `:197`, MT_UNACCEPTABLE continue `:209`, MT_QUIT break `:218`, `finally` `:221-224`; `CreateSpan(Process)` `Proactor.cs:226`. Same 2× serviceable / 1× MT_UNACCEPTABLE.

4. **MT_UNACCEPTABLE serializes exactly once:** `continue`s at `Reactor.cs:168` / `Proactor.cs:209` before `CreateSpan(Process)`; its only header serialization is the receive-span one. This makes "drop the receive-span header tag" lossy for rejected messages.

## Scope Notes
- **Caller scope is fully closed.** The `CreateSpan(MessagePumpSpanOperation, Message, ...)` overload (`BrighterTracer.cs:154`) is called **only** from `Reactor.cs:185` and `Proactor.cs:226`. `EnrichReceiveSpan`/`CreateReceiveSpan` are called **only** from the two pumps (`Reactor.cs:117,119`; `Proactor.cs:158,160`). `CommandProcessor.cs` uses the *different* generic `CreateSpan<TRequest>` overload (serializes a request, not `message.Header`) — not part of this defect.
- **Test that constrains the fix (CRITICAL):** `tests/Paramore.Brighter.Core.Tests/Observability/MessageDispatch/When_A_Message_Is_Dispatched_It_Should_Begin_A_Span.cs:161` asserts the **receive** span (DisplayName `… receive`, MessageType MT_EVENT — a *serviceable* message) carries `MessageHeaders == JsonSerializer.Serialize(_message.Header, …)`. Any fix that removes/relocates the receive-span header tag for serviceable messages (issue fixes **#3 and #4**) breaks this. Caching (#1/#2) keeps it green.
- **Unacceptable test does NOT assert headers:** `When_There_Is_An_Unacceptable_Messages_Close_The_Span.cs` (:100-110) asserts only `MessageType == MT_UNACCEPTABLE` + Error status on the receive span — no `MessageHeaders` assertion. The MT_UNACCEPTABLE header diagnostic is real but not currently test-locked; the locking assertion is the serviceable one at :161.
- **MessageBody/RequestBody double-emission is real but cheap** (`Message.Body.Value` is a string, no reflection). Same 2×/1× shape; not worth a separate fix but should ride along with whatever caches the header.
- **Minor pre-existing waste:** `EnrichReceiveSpan` also runs for MT_NONE (empty-queue poll) and MT_QUIT, so those pay one header serialization each despite never being serviceable. Low impact (trivial headers); not the reported regression.

### Suggested-Fix Assessment
- **#1 — cache serialized string for message lifetime: CONFIRMED (best option).** No header mutation occurs between `EnrichReceiveSpan` (`:119`) and `CreateSpan(Process)` (`:185`) in either pump, so both calls produce byte-identical JSON. A lazily-computed/cached string preserves both span tags and halves cost. Breaks no test.
- **#2 — memoize on Message/MessageHeader: CONFIRMED with caveats.** Functionally equivalent to #1. Caveat: `MessageHeader` is mutable (`Baggage`, `HandledCount`), so memoization must be invalidation-safe / computed where the header is stable; thread-safety if a header is shared. PARTIAL on robustness.
- **#3 — drop receive-span header tag: WRONG.** Breaks the `:161` assertion and loses MT_UNACCEPTABLE rejection diagnostics (the only header serialization for rejected messages lives on the receive span).
- **#4 — conditional-by-MessageType (issue author's lean): PARTIAL / effectively WRONG as described.** Branch is implementable (`message.Header.MessageType` is already used at `BrighterTracer.cs:290`) and would achieve at-most-once while keeping MT_UNACCEPTABLE diagnostics — BUT it removes `MessageHeaders` from the receive span for serviceable messages, directly breaking the `:161` assertion / documented contract. Strictly loses observability data vs. #1 for no benefit.

**Recommended approach: #1 (cache the serialized header for the message lifetime).** Keeps both span tags, breaks no test, recovers the full regression.

## Regression Test
**File**: `tests/Paramore.Brighter.Core.Tests/Observability/MessageDispatch/When_A_Message_Is_Dispatched_The_Header_Is_Serialized_Once.cs`
**Test**: `MessageHeaderSerializationObservabilityTests.When_a_message_is_dispatched_the_header_is_serialized_once_for_both_spans`

Runs the real Reactor pump for one serviceable `MT_EVENT` message, locates the receive span and the process span, and asserts:
1. Both spans carry the `MessageHeaders` tag with the correct value (`Assert.Equal` vs `JsonSerializer.Serialize(_message.Header, …)`) — preserves the diagnostic contract that already locks the receive-span header at `When_A_Message_Is_Dispatched_It_Should_Begin_A_Span.cs:161`.
2. Both tag values are the **same string instance** (`Assert.Same`) — proving the header was serialized once and reused, not twice.

**RED confirmed** (net9.0 + net10.0): fails with `Assert.Same() Failure: Values are not the same instance` — the two `Assert.Equal` checks pass (equal values) but the instances differ, i.e. two independent `JsonSerializer.Serialize` calls today. `JsonSerializer.Serialize` output is never interned, so the red is reliable. Will turn green once the serialized header is computed once per message and shared across both spans (suggested fix #1 — caching).

## Fix
**Approach**: confirmed fix #1 (caching) — compute the serialized header once per `Message` lifetime and reuse it across both spans.

**Files changed**:
- `src/Paramore.Brighter/Message.cs` — added an `internal` lazy-cached property `HeaderJson` (`_headerJson ??= JsonSerializer.Serialize(Header, JsonSerialisationOptions.Options)`). `internal`, so no public API change and no observability concern leaks to consumers; only `BrighterTracer` (same assembly) reads it. Snapshot-on-first-access; documented as such.
- `src/Paramore.Brighter/Observability/BrighterTracer.cs` — both pump-path sites now use `message.HeaderJson` instead of calling `JsonSerializer.Serialize(message.Header, …)`: `CreateSpan(MessagePumpSpanOperation, …)` (process span, was :191) and `EnrichReceiveSpan` (receive span, was :292).

**Result**: a serviceable message serializes the header **once** (first access in `EnrichReceiveSpan`, reused by `CreateSpan(Process)`); MT_UNACCEPTABLE still serializes once (receive span only) and still carries headers. Both spans share the same string instance.

**Scope honored**: `MessageBody`/RequestBody left untouched (plain string read, no reflection — Scope Notes deemed it not worth changing). No defaults changed.

**Behavioral note**: when `CorrelationId` is non-empty, the process span's `MessageHeaders` tag previously included the `correlationId` that `EnrichReceiveSpan` adds to `Header.Baggage` between the two serializations; with the shared snapshot both spans now show the pre-correlationId-baggage header (the receive-span snapshot). No test asserts process-span header content; consistent with the issue's "same data" intent.

**Tests**: `When_a_message_is_dispatched_the_header_is_serialized_once_for_both_spans` GREEN; full `Observability.MessageDispatch` folder 13/13 GREEN on net9.0 + net10.0.
