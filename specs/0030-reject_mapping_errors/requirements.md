# Requirements

> **Note**: This document captures user requirements and needs. Technical design decisions and implementation details should be documented in an Architecture Decision Record (ADR) in `docs/adr/`.

**Linked Issue**: #4160

## Problem Statement

As an operator running Brighter consumers on a transport that supports rejection routing (e.g., AWS SQS V4 configured with an Invalid Message Queue and a Dead Letter Queue), I want messages that fail message mapping to be routed to the configured rejection channels (Invalid Message Queue or Dead Letter Queue) instead of being silently deleted from the source queue, so that I can inspect, replay, or alert on those malformed messages rather than losing them.

Today, when the message pump catches a `MessageMappingException` (raised during message translation/unwrap), it logs the failure, increments the unacceptable-message counter, and then falls through to the unconditional acknowledge call at the bottom of the loop (`await Acknowledge(message)` in the async pump, `AcknowledgeMessage(message)` in the sync pump). On SQS V4 and transports with the same acknowledge semantics, acknowledging performs a straight `DeleteMessage`, so the Invalid Message Queue and Dead Letter Queue are never consulted even when configured. Because the default `UnacceptableMessageLimit` is `0` (meaning "no limit"), these failures are silently and permanently deleted forever unless an operator has explicitly set the limit.

Every other action-driven exception path in the pump (`DeferMessageAction`, `DontAckAction`, `RejectMessageAction`, `InvalidMessageAction`) already routes through `RejectMessage(...)` with an appropriate `RejectionReason`. The `MessageMappingException` path was simply omitted — a malformed message that cannot be translated should be treated as unacceptable (routed to the IMQ), not silently deleted. This change closes that single gap.

**The catch-all `Exception` path is deliberately NOT changed.** Brighter's handler-failure policy is that a general application exception raised during dispatch is logged and the message acknowledged; the framework does not reinterpret an arbitrary exception as a rejection. Routing to a DLQ/IMQ, deferral, or rejection is driven only by explicit means — the handler throwing an action exception (`DeferMessageAction`, `RejectMessageAction`, etc.) or using an attribute/policy — not by the pump second-guessing an unhandled exception. See the [handler failure policy](https://brightercommand.gitbook.io/paramore-brighter-documentation/using-an-external-bus/handlerfailure). The catch-all therefore keeps its current behaviour (log, increment, fall through to acknowledge), and this requirement explicitly preserves it (FR-8).

### Definitions

- **IMQ** — Invalid Message Queue: the transport-configured destination for messages the consumer deems malformed/unacceptable.
- **DLQ** — Dead Letter Queue: the transport-configured destination for messages that could not be delivered/processed.
- **Pump** — the message pump: the loop that receives a message, translates it, and dispatches it.
- **Proactor** — the asynchronous message pump (`src/Paramore.Brighter.ServiceActivator/Proactor.cs`).
- **Reactor** — the synchronous message pump (`src/Paramore.Brighter.ServiceActivator/Reactor.cs`).
- **`RejectMessage`** — the pump helper (`Task<bool> RejectMessage(Message, MessageRejectionReason)` in Proactor; `bool RejectMessage(Message, MessageRejectionReason)` in Reactor) that delegates to the consumer's reject implementation, which on supporting transports routes to IMQ/DLQ and then deletes the source message.
- **`MessageMappingException`** — the exception raised when translating a transport message into a Brighter request (the unwrap pipeline) fails.

## Proposed Solution

When the pump fails to map a message (`MessageMappingException`), it must reject the message through the same `RejectMessage` flow already used by the other action-driven exception paths, supplying `RejectionReason.Unacceptable`, rather than allowing the message to fall through to the unconditional acknowledge (delete). The pump's only responsibility is to invoke `RejectMessage` with the correct reason; the consumer/transport decides what rejection actually means (route to IMQ/DLQ then delete the source, plain delete, etc.). This makes failed-mapping messages observable on the configured rejection channels on transports that support them, while leaving the behaviour of transports without such channels unchanged.

The catch-all `Exception` (dispatch failure) path is left unchanged per Brighter's handler-failure policy (see Problem Statement and FR-8): general application exceptions are logged and acknowledged, with DLQ/defer/reject driven only by explicit action exceptions or attributes.

The unacceptable-message counter must continue to be incremented on the mapping path so the existing `UnacceptableMessageLimit` / `UnacceptableMessageLimitWindow` guardrail still terminates the pump on repeated failures.

## Requirements

### Functional Requirements

**FR-1: Route `MessageMappingException` through `RejectMessage` with `Unacceptable` in the async pump (Proactor).**
When the Proactor's `catch (MessageMappingException ...)` block executes, after logging and incrementing the unacceptable-message count it MUST call `await RejectMessage(message, new MessageRejectionReason(RejectionReason.Unacceptable, <mapping-description>))` and then `continue` the loop, so control does not reach the unconditional `await Acknowledge(message)`. The `<mapping-description>` is the message defined in C-5 (mapping variant).
- Example: A subscription is missing an unwrap transformer registration, so translating message `Id = "abc-123"` throws `MessageMappingException`. Expected: `RejectMessage` is invoked once for message `abc-123` with reason `Unacceptable` and a non-empty `Description` containing `abc-123` (per C-5); `Acknowledge("abc-123")` is NOT invoked by the fall-through path; the loop proceeds to the next iteration.

**FR-2: ~~Route the catch-all `Exception` through `RejectMessage` with `DeliveryError` in the async pump (Proactor).~~ REMOVED.**
This requirement is withdrawn. The catch-all `Exception` path is left unchanged per Brighter's handler-failure policy (see FR-8 and the Out of Scope list). General application exceptions are logged and acknowledged; the pump does not reinterpret an unhandled exception as a rejection. (ID retained as a tombstone to preserve traceability against the approved baseline and the review history.)

**FR-3: Route `MessageMappingException` through `RejectMessage` with `Unacceptable` in the sync pump (Reactor).**
When the Reactor's `catch (MessageMappingException ...)` block executes, after logging and incrementing the unacceptable-message count it MUST call `RejectMessage(message, new MessageRejectionReason(RejectionReason.Unacceptable, <mapping-description>))` and then `continue` the loop, so control does not reach the unconditional `AcknowledgeMessage(message)`. The `<mapping-description>` is the message defined in C-5 (mapping variant).
- Example: Same missing-transformer scenario as FR-1 but on a synchronous consumer for message `Id = "ghi-789"`. Expected: `RejectMessage` is invoked once for message `ghi-789` with reason `Unacceptable` and a non-empty `Description` containing `ghi-789` (per C-5); `AcknowledgeMessage("ghi-789")` is NOT invoked by the fall-through path; the loop proceeds to the next iteration.

**FR-4: ~~Route the catch-all `Exception` through `RejectMessage` with `DeliveryError` in the sync pump (Reactor).~~ REMOVED.**
This requirement is withdrawn. The catch-all `Exception` path is left unchanged per Brighter's handler-failure policy (see FR-8 and the Out of Scope list). (ID retained as a tombstone to preserve traceability against the approved baseline and the review history.)

**FR-5: Preserve the unacceptable-message guardrail on both changed (mapping) paths.**
On each of the two changed paths (FR-1 and FR-3), the existing `IncrementUnacceptableMessageCount()` call MUST be retained. The guardrail semantics are unchanged: `UnacceptableMessageLimit <= 0` means no limit (the default of `0` never trips); when `UnacceptableMessageLimit` is set to a positive value `N`, after `N` accumulated unacceptable messages (within `UnacceptableMessageLimitWindow` when set) the pump terminates as it does today.
- Example: `UnacceptableMessageLimit = 3`, window unset. Three consecutive `MessageMappingException`s for messages `m1`, `m2`, `m3` each call `RejectMessage(Unacceptable)` and `IncrementUnacceptableMessageCount()`; on the next loop iteration `UnacceptableMessageLimitReached()` returns true and the pump stops.
- Example: `UnacceptableMessageLimit = 0` (default). 100 consecutive mapping failures each reject and increment, but the pump never terminates due to the limit.

**FR-6: Existing logging and tracing on the changed (mapping) paths are preserved.**
On each changed path the existing `Log.FailedToMapMessage(...)` log call and the existing `processSpan?.SetStatus(ActivityStatusCode.Error, ...)` call MUST still execute, and the `finally { Tracer?.EndSpan(processSpan); }` block (the **process** span — distinct from the separate receive-span `finally` higher in the loop) MUST still run for the iteration. Throughout FR-6 and AC-1/AC-3, "the span" refers specifically to the per-message **process** span (`processSpan`), not the receive span.
- Example: On a `MessageMappingException` for message `abc-123`, `Log.FailedToMapMessage` is emitted, the **process** span status is set to `Error`, and the process span is ended via `Tracer?.EndSpan(processSpan)` exactly once for that iteration.

**FR-7: Behaviour for transports without IMQ/DLQ is delegated, not special-cased.**
The pump MUST NOT branch on transport type or on whether an IMQ/DLQ is configured. It calls `RejectMessage` with `Unacceptable` (per FR-1 and FR-3), and the consumer's reject implementation determines the outcome. On a consumer whose reject implementation has no IMQ/DLQ configured (or does a plain delete), invoking `RejectMessage` still results in correct behaviour as defined by that consumer.
- Example: An in-memory or RabbitMQ consumer with no IMQ/DLQ configured receives a `MessageMappingException`; the pump calls `RejectMessage(Unacceptable)`; the consumer performs whatever its reject does (e.g., delete/nack); no transport-specific code is added to the pump.

**FR-8: The catch-all `Exception` (dispatch failure) path is left unchanged.**
The body of the catch-all `catch (Exception e)` block in both pumps MUST NOT be modified by this change. It continues to log via `Log.FailedToDispatchMessage2(...)`, set the process span status to `Error`, increment the unacceptable-message count, and fall through to the unconditional acknowledge (`await Acknowledge(message)` / `AcknowledgeMessage(message)`). This reflects Brighter's handler-failure policy: a general application exception is logged and the message acknowledged; routing to a DLQ/IMQ, deferral, or rejection is the handler author's explicit choice (an action exception such as `DeferMessageAction`/`RejectMessageAction`, or an attribute/policy) — the pump does not reinterpret an arbitrary unhandled exception as a rejection. See the [handler failure policy](https://brightercommand.gitbook.io/paramore-brighter-documentation/using-an-external-bus/handlerfailure).
- Example: Dispatch of message `Id = "def-456"` throws an unexpected `InvalidOperationException`. Expected: the catch-all logs, sets span error, increments the count, and `Acknowledge("def-456")` IS invoked via the fall-through path; `RejectMessage` is NOT invoked for `def-456`. (Regression guard — verifies this change did not accidentally alter the catch-all; see AC-2 (async) / AC-4 (sync).)

### Non-functional Requirements

- **NFR-1 (Parity):** The async (Proactor) and sync (Reactor) implementations MUST be behaviourally equivalent for the mapping path (FR-1 / FR-3) — same `RejectionReason` (`Unacceptable`), same retention of the counter increment, same `continue` behaviour. (The catch-all path is unchanged in both and so remains at whatever parity it has today — see A-1.)
- **NFR-2 (No transport changes):** No changes are required or permitted in `SqsMessageConsumer` (its `RejectAsync`/`Reject` already implements the IMQ→DLQ→delete routing) or in any other transport consumer. The change is confined to the pump.
- **NFR-3 (No public API change):** No public type signatures, subscription options, or configuration surface are added or changed. `RejectMessage`, `MessageRejectionReason`, and `RejectionReason` already exist and are reused as-is.
- **NFR-4 (Testability):** The change must be verifiable with unit tests using a test/fake consumer that records whether `Reject` or `Acknowledge` was invoked and with what reason, without requiring live AWS infrastructure.

### Constraints and Assumptions

- **C-1:** The exact ordering of the catch blocks relative to one another (`ConfigurationException`, `DeferMessageAction`, `DontAckAction`, `RejectMessageAction`, `InvalidMessageAction`, then `MessageMappingException`, then catch-all `Exception`) is unchanged. Only the body of the `MessageMappingException` block changes; the catch-all `Exception` block is left unchanged (FR-8).
- **C-2:** `RejectionReason.Unacceptable` is the reason that, on supporting transports, routes to the IMQ with fallback to the DLQ. The mapping path uses `Unacceptable` (malformed message — the consumer could not even translate it). `RejectionReason.DeliveryError` (DLQ-only) is NOT used by this change, because the catch-all dispatch-failure path is left unchanged per the handler-failure policy (FR-8); it is described here only for context.
- **C-3:** "Route via `RejectMessage` instead of plain Acknowledge" is the intent — the source message may still ultimately be deleted as part of the consumer's reject semantics. The requirement is NOT "stop deleting the source"; it is "stop deleting via the fall-through Acknowledge and instead delete (if at all) via the reject flow after IMQ/DLQ routing."
- **C-4:** `RejectMessage` returns a `bool`/`Task<bool>`. Mirroring the existing `RejectMessageAction`/`InvalidMessageAction` blocks, the two changed (mapping) paths unconditionally `continue` after calling `RejectMessage` (the return value is not used to decide whether to fall through to Acknowledge).
- **C-5 (rejection `Description` content):** The `Description` passed into `MessageRejectionReason` MUST be a non-empty string that includes the message's `Id`, and MUST be the same description string used for the existing `processSpan?.SetStatus(...)` call on the mapping path — extracted to a shared local so the rejection reason and the span status carry identical text (in the current source it is an inline `$"..."` interpolation, so the implementer constructs it once and reuses it). The relevant variant is:
  - **Mapping variant** (FR-1, FR-3): `$"MessagePump: Failed to map message {message.Id} from {Channel.Name} with {Channel.RoutingKey} on thread # {Thread.CurrentThread.ManagedThreadId}"`.
  Preserve the existing `Thread.CurrentThread.ManagedThreadId` thread-id expression the mapping `SetStatus` call already uses — do not normalise it, as that would be a change outside the `MessageMappingException` block body (see C-1). (The catch-all path's own `SetStatus` string — the "Failed to dispatch …" interpolation using `Environment.CurrentManagedThreadId` — is untouched, since the catch-all body is unchanged per FR-8.)
  Rationale: `RejectMessage` persists `reason.Description` into the message header bag (and, on SQS V4, into rejection metadata), making it operator-visible on the IMQ/DLQ — the observability the feature exists to provide. The exact thread-id token value is not asserted (it is environment-dependent); the asserted contract is "non-empty and contains the message `Id`" (see AC-10).
- **C-6 (throwing `RejectMessage` is out of scope):** This change assumes the consumer's reject implementation does not throw back to the pump. If `RejectMessage` (i.e. the underlying consumer `Reject`/`RejectAsync`) throws, the exception propagates out of the catch block and the pump loop, exactly as an unguarded statement would today on the other reject paths (`RejectMessageAction`, `InvalidMessageAction`, etc.). Adding a try/guard around `RejectMessage` on the mapping path — or changing the existing reject paths' lack of one — is explicitly NOT part of this change; the changed path mirrors the existing reject blocks' unguarded shape (see C-1, C-4). A throwing reject surfacing as a pump-loop exception is therefore accepted, consistent behaviour, not a regression introduced here.
- **A-1 (Assumption):** Pre-existing inconsistencies between Proactor and Reactor that are outside the changed `MessageMappingException` block (for example, the Reactor's `ConfigurationException` block calling `IncrementUnacceptableMessageCount()` while the Proactor's does not) are NOT in scope for this change and are left as-is.
- **A-2 (Assumption):** Mapping failures reach the dedicated `catch (MessageMappingException ...)` block (and so take the `Unacceptable` reject path, rather than falling through to the catch-all's log-and-acknowledge) because `TranslateMessage` is invoked via a direct `await`, which surfaces the **bare, unwrapped** `MessageMappingException` it throws — not because of catch-block ordering alone. This matters: the per-message body also has an inner `catch (AggregateException)` that classifies `InnerExceptions` against the action exceptions (`ConfigurationException`, `DeferMessageAction`, `DontAckAction`, `RejectMessageAction`, `InvalidMessageAction`) but has **no** `MessageMappingException` arm. A `MessageMappingException` arriving *wrapped* in an `AggregateException` would therefore fall through that handler unmatched and exit to the unconditional acknowledge — the exact silent-delete this change removes for malformed messages. (It would also miss the new reject, landing instead in the catch-all's acknowledge, never reaching the IMQ.) Consequently this change MUST NOT introduce any wrapping of the mapping exception, and the mapping path MUST be exercised through the real `TranslateMessage` (not a hand-thrown bare exception) so a future refactor that wraps it is caught by tests (see AC-12).

### Out of Scope

- The root cause of the originally observed mapping failure (assembly load timing in auto-registration) — tracked separately in #4159.
- Any change to `SqsMessageConsumer` or other transport consumers' reject/delete logic.
- Adding, renaming, or changing `RejectionReason` enum values or `MessageRejectionReason`.
- Changing the `UnacceptableMessageLimit` / `UnacceptableMessageLimitWindow` defaults or guardrail logic.
- Reconciling pre-existing Proactor/Reactor differences outside the affected `MessageMappingException` catch block (see A-1).
- Changing the ordering or set of catch blocks in the pump (see C-1).
- Guarding `RejectMessage` against a throwing consumer reject implementation on the changed path (or on any existing reject path) — see C-6.
- **Routing the catch-all `Exception` (dispatch failure) path through `RejectMessage`.** This was considered and explicitly dropped. Per Brighter's [handler-failure policy](https://brightercommand.gitbook.io/paramore-brighter-documentation/using-an-external-bus/handlerfailure), a general application exception is logged and the message acknowledged; DLQ/defer/reject behaviour is the handler author's explicit choice (an action exception or attribute/policy), not something the pump infers from an arbitrary unhandled exception. The catch-all body is therefore left unchanged (FR-8). A `MessageMappingException` is different — it is a malformed-message signal raised by the framework's own translate step, with no opportunity for handler-level policy, so rejecting it as `Unacceptable` is correct (FR-1/FR-3).

## Acceptance Criteria

**AC-1 (covers FR-1, FR-6):**
Given an async (Proactor) pump with a test consumer recording reject and acknowledge calls,
When a `MessageMappingException` is thrown while processing message `abc-123`,
Then the consumer records exactly one reject for `abc-123` with `RejectionReason.Unacceptable`, records zero fall-through acknowledge calls for `abc-123`, and the failure is logged via `Log.FailedToMapMessage` with the process span status set to `Error` and the span ended.

**AC-2 (covers FR-8 — catch-all left unchanged, async):**
Given an async (Proactor) pump with a test consumer recording reject and acknowledge calls,
When an unexpected `Exception` (e.g., `InvalidOperationException`) is thrown while dispatching message `def-456`,
Then the consumer records zero rejects for `def-456`, records exactly one fall-through acknowledge for `def-456`, and the failure is logged via `Log.FailedToDispatchMessage2` with the process span status set to `Error` and the span ended. (Regression guard: the catch-all keeps its current log-and-acknowledge behaviour per Brighter's handler-failure policy; this change does NOT route it through `RejectMessage`.)

**AC-3 (covers FR-3, FR-6):**
Given a sync (Reactor) pump with a test consumer recording reject and acknowledge calls,
When a `MessageMappingException` is thrown while processing message `ghi-789`,
Then the consumer records exactly one reject for `ghi-789` with `RejectionReason.Unacceptable`, records zero fall-through acknowledge calls for `ghi-789`, and the failure is logged via `Log.FailedToMapMessage` with the process span status set to `Error` and the span ended.

**AC-4 (covers FR-8 — catch-all left unchanged, sync):**
Given a sync (Reactor) pump with a test consumer recording reject and acknowledge calls,
When an unexpected `Exception` (e.g., `InvalidOperationException`) is thrown while dispatching message `jkl-012`,
Then the consumer records zero rejects for `jkl-012`, records exactly one fall-through acknowledge for `jkl-012`, and the failure is logged via `Log.FailedToDispatchMessage2` with the process span status set to `Error` and the span ended. (Regression guard: same handler-failure-policy rationale as AC-2, for the sync pump.)

**AC-5 (covers FR-5):**
Given a pump (Proactor or Reactor) configured with `UnacceptableMessageLimit = 3` and no window,
When three consecutive messages each throw `MessageMappingException`,
Then each triggers a reject with `RejectionReason.Unacceptable` and increments the unacceptable-message count, and on the iteration after the third failure the pump terminates because `UnacceptableMessageLimitReached()` returns true. (The limit is evaluated at the top of the loop, before the next receive, so the 4th message is not dispatched.)

**AC-6 (covers FR-5):**
Given a pump (Proactor or Reactor) with the default `UnacceptableMessageLimit = 0`,
When many consecutive messages (e.g., 100) each throw `MessageMappingException`,
Then each is rejected with `RejectionReason.Unacceptable` and the count is incremented, and the pump does NOT terminate due to the limit (no limit is enforced at `0`).

**AC-7 (covers FR-1, FR-3 — no fall-through on the mapping path):**
Given either changed (mapping) path,
When the `catch (MessageMappingException ...)` block completes,
Then the `finally { ... EndSpan ... }` runs and the loop `continue`s such that the unconditional `await Acknowledge(message)` (Proactor) / `AcknowledgeMessage(message)` (Reactor) at the bottom of the loop is NOT executed for that message.

**AC-8 (covers FR-7, NFR-2):**
Given a fake consumer with no IMQ/DLQ configured that records every `Reject` and `Acknowledge` call,
When a `MessageMappingException` is processed by the pump for message `mno-345`,
Then `Reject("mno-345")` is invoked exactly once with `RejectionReason.Unacceptable` (per C-2), the fall-through `Acknowledge("mno-345")` is invoked zero times, and no transport-specific branching exists in the pump and no consumer code is changed.
(Note: what the consumer's reject *implementation* then does to the source — delete/ack/nack, e.g. SQS V4 acking the source when no rejection channels are configured — is the consumer's contract and out of scope per C-3. The assertion is solely that the **pump** invokes `RejectMessage` rather than the fall-through `Acknowledge`.)

**AC-9 (covers FR-7, NFR-2 — optional integration, NOT a required unit gate):**
Given the SQS V4 end-to-end path with an IMQ and a DLQ configured,
When a message fails mapping,
Then the message appears on the Invalid Message Queue (or the DLQ on fallback) and is removed from the source queue via the reject flow, rather than being silently deleted by the fall-through acknowledge.
(Per NFR-4, the required verification of FR-1, FR-3, FR-5–FR-8 uses a fake consumer and does NOT require live AWS. AC-9 is an optional end-to-end confirmation against real SQS V4 and is not a blocking gate for this change.)

**AC-10 (covers FR-1, FR-3 — rejection `Description` content, per C-5):**
Given either mapping path with a test consumer that captures the `MessageRejectionReason` passed to `Reject`,
When the path rejects message `Id = "<id>"`,
Then the captured `MessageRejectionReason.Description` is non-empty and contains the substring `<id>` (the message `Id`). The test captures both the `Description` and the string passed to `processSpan?.SetStatus(...)` on the same path and asserts they are equal; it does NOT assert any literal string containing the thread id (whose value is environment-dependent, per C-5).

**AC-11 (covers NFR-1 — async/sync parity on the mapping path):**
Given the Proactor (async) and Reactor (sync) pumps subjected to the same `MessageMappingException` scenario,
When the scenario is run against each pump,
Then both pumps reject with the identical `RejectionReason.Unacceptable`, both retain the `IncrementUnacceptableMessageCount()` call, both `continue` past the fall-through acknowledge, and neither pump invokes its bottom-of-loop acknowledge for the failed message.

**AC-12 (covers FR-1, FR-3, A-2 — mapping exception routed via the real translate path):**
Given a pump configured so that the actual message-translation path (`TranslateMessage` / the unwrap pipeline) raises a `MessageMappingException` — e.g. a missing unwrap transformer registration — rather than a hand-thrown bare exception injected into the test,
When a message is processed,
Then the `MessageMappingException` reaches the dedicated `catch (MessageMappingException ...)` block and is rejected with `RejectionReason.Unacceptable` (it does NOT fall through to the catch-all `Exception` block, which would log-and-acknowledge per FR-8), confirming the exception propagates unwrapped from the directly-awaited translate call. (This guards against a future refactor that wraps the mapping exception in an `AggregateException`, which would misroute it to the catch-all and re-introduce the silent-delete for malformed messages — see A-2.)

## Additional Context

- Changed source (async): `src/Paramore.Brighter.ServiceActivator/Proactor.cs` — only the `catch (MessageMappingException ...)` block at ~lines 374-379 is modified. The `catch (Exception e)` block at ~lines 380-385 and the unconditional `await Acknowledge(message)` at ~line 391 are referenced for context but NOT changed (FR-8). `RejectMessage` defined at ~line 474; `UnacceptableMessageLimitReached()` at ~line 554.
- Changed source (sync): `src/Paramore.Brighter.ServiceActivator/Reactor.cs` — only the `catch (MessageMappingException ...)` block at ~lines 339-344 is modified. The `catch (Exception e)` block at ~lines 345-350 and the unconditional `AcknowledgeMessage(message)` at ~line 356 are referenced for context but NOT changed (FR-8). `RejectMessage` defined at ~line 407; `UnacceptableMessageLimitReached()` at ~line 546.
- Reference shape already used by the `RejectMessageAction` / `InvalidMessageAction` catch blocks: `IncrementUnacceptableMessageCount(); await RejectMessage(message, new MessageRejectionReason(RejectionReason.Unacceptable, <text>)); continue;` (sync: drop `await`).
- The desired routing already exists in `SqsMessageConsumer.RejectAsync` (V4): `Unacceptable` → Invalid Message Queue, falling back to DLQ; in that case the original source message is then deleted. No change is needed there (NFR-2).
- `UnacceptableMessageLimit` (default `0`) and `UnacceptableMessageLimitWindow` live on `src/Paramore.Brighter.ServiceActivator/MessagePump.cs` (~lines 135, 141).
- Discovered while investigating #4159 (missing transformer registration causing `MessageMappingException` from the unwrap pipeline); this issue (#4160) covers the downstream symptom of failed-mapping messages being silently deleted instead of routed to IMQ/DLQ.
