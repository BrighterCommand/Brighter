---
id: 0061-reject_mapping_errors
title: "Route Mapping Failures Through RejectMessage"
status: Accepted
author:
  - "Brighter Team"
created: 2026-06-01
summary: "Routes MessageMappingException failures in the Proactor and Reactor message pumps through the existing RejectMessage path with RejectionReason.Unacceptable, so malformed messages are directed to the configured IMQ/DLQ instead of being silently deleted via fall-through acknowledge."
tags:
  - "messaging"
  - "message-pump"
  - "error-handling"
  - "message-rejection"
---

# 0061. Route Mapping Failures Through RejectMessage

Date: 2026-06-01

## Status

Accepted

## Context

The ServiceActivator message pump runs an event loop that receives a message, translates it into a Brighter request, and dispatches it to handlers. Each meaningful failure mode in the per-message body has a dedicated `catch` block, and the action-driven ones (`DeferMessageAction`, `DontAckAction`, `RejectMessageAction`, `InvalidMessageAction`) route the message through the pump's `RejectMessage(...)` helper with an appropriate `RejectionReason`, then `continue` the loop. `RejectMessage` delegates to the consumer's `Reject`/`RejectAsync`, which on supporting transports (e.g. AWS SQS V4) routes the message to a configured Invalid Message Queue (IMQ) or Dead Letter Queue (DLQ) before deleting the source.

One path breaks this pattern. The dedicated `catch (MessageMappingException ...)` block — entered when translating/unwrapping the transport message into a Brighter request fails — only logs the failure, calls `IncrementUnacceptableMessageCount()`, and sets the process span status to `Error`. It does **not** call `RejectMessage` and it does **not** `continue`. Control therefore falls through to the unconditional acknowledge at the bottom of the loop — `await Acknowledge(message)` in the Proactor (verified at `Proactor.cs:391`) and `AcknowledgeMessage(message)` in the Reactor (`Reactor.cs:356`).

On SQS V4 and transports with the same acknowledge semantics, acknowledge is a straight `DeleteMessage`. So a configured IMQ/DLQ is never consulted for a mapping failure — the malformed message is silently and permanently deleted. This is made worse by the default `UnacceptableMessageLimit` of `0`, which means "no limit" (`UnacceptableMessageLimitReached()` returns `false` when the limit is `<= 0`, verified `Proactor.cs:556` / `Reactor.cs:548`), so the guardrail that would otherwise stop the pump after repeated failures never trips unless an operator has explicitly set a positive limit.

### Why the catch-all `Exception` path is deliberately out of scope

The per-message body also has a catch-all `catch (Exception e)` block for unexpected exceptions raised during dispatch. It is tempting to route that through `RejectMessage` too, but doing so would violate Brighter's handler-failure policy: a general application exception is logged and the message acknowledged; the framework does **not** reinterpret an arbitrary unhandled exception as a rejection. Routing to a DLQ/IMQ, deferral, or rejection is the handler author's explicit choice — throwing an action exception (`DeferMessageAction`, `RejectMessageAction`, …) or using an attribute/policy. See the [handler failure policy](https://brightercommand.gitbook.io/paramore-brighter-documentation/using-an-external-bus/handlerfailure). A `MessageMappingException` is categorically different: it is raised by the framework's own translate step *before* any handler runs, so there is no handler-level policy that could have classified it. Treating a message the framework cannot even translate as `Unacceptable` (route to IMQ) is the correct, framework-owned decision; second-guessing an arbitrary dispatch exception is not. This ADR therefore changes only the `MessageMappingException` block and leaves the catch-all unchanged.

The forces in tension:

- Operators need failed-mapping messages to be observable on the rejection channels they configured, for inspection, replay, and alerting — not silently lost.
- The pump must not become transport-aware; whether an IMQ/DLQ exists is the consumer's knowledge, not the pump's.
- The pump must not override the handler-failure policy by reclassifying general dispatch exceptions as rejections.
- The change must hold for both pumps (async Proactor, sync Reactor) and must not perturb the surrounding, intentionally-ordered catch blocks or any public API.

**Parent Requirement**: [specs/0030-reject_mapping_errors/requirements.md](../../specs/0030-reject_mapping_errors/requirements.md)

**Scope**: A single, focused behavioural change to the body of exactly one catch block in each of `Proactor.cs` and `Reactor.cs` (two code paths total): the `MessageMappingException` block. The catch-all `Exception` block, all other catch blocks, the catch-block ordering, transports/consumers, and the public API are unchanged.

## Decision

Route the mapping-failure path through the existing `RejectMessage(...)` + `continue`, mirroring the shape already used by the adjacent `RejectMessageAction` and `InvalidMessageAction` blocks:

- **Mapping failure** (`catch (MessageMappingException ...)`) → `RejectMessage(message, new MessageRejectionReason(RejectionReason.Unacceptable, description))` then `continue`.

Apply identically in both pumps; the Reactor version simply drops `await`. Retain `IncrementUnacceptableMessageCount()` on the mapping path so the `UnacceptableMessageLimit` guardrail still trips. Leave the catch-all `Exception` block exactly as it is today (log, increment, fall through to acknowledge). Make no other changes.

### Responsibility-Driven Design framing

The crux is a responsibility allocation between two roles:

- The **pump** (a coordinator) *knows* that translation failed (it caught `MessageMappingException` from its own directly-awaited translate step) and *decides* the rejection intent it should declare. Its responsibility on this path is to **decide and declare** `RejectionReason.Unacceptable`, nothing more.
- The **consumer** (a service provider) *knows* the transport topology — whether an IMQ/DLQ exists — and *does* the routing and deletion. Its responsibility is to **interpret** the declared reason and act.

The mapping path currently violates this separation by short-circuiting to acknowledge, which is the pump *doing* deletion directly instead of *declaring* intent and letting the consumer decide. This change restores the separation on that one path, bringing it into line with the action-exception paths that already get it right. Critically, it does **not** extend the pump's "deciding" responsibility to the catch-all: classifying an arbitrary dispatch exception is the *handler author's* responsibility (via action exceptions/attributes), not the pump's. The change introduces no new types and no branching. As a purely behavioural change that neither relocates nor renames code, it carries no separate structural-change step under the Tidy First convention.

### Architecture Overview

No structural change. The per-message body in each pump keeps its existing ordered catch chain (verified unchanged-by-this-decision): `AggregateException`, `ConfigurationException`, `DeferMessageAction`, `DontAckAction`, `RejectMessageAction`, `InvalidMessageAction`, `MessageMappingException`, then catch-all `Exception`, then `finally { Tracer?.EndSpan(processSpan); }`. Only the body of the `MessageMappingException` block gains a `RejectMessage(...); continue;`. The catch-all block and the bottom-of-loop acknowledge are untouched; acknowledge remains both the success path and the catch-all's (policy-driven) terminal action — it is simply no longer reached by the mapping-failure path.

### Key Components

- `src/Paramore.Brighter.ServiceActivator/Proactor.cs` — `catch (MessageMappingException messageMappingException)` (verified `~374-379`) **[changed]**; catch-all `catch (Exception e)` (`~380-385`) **[unchanged]**; fall-through `await Acknowledge(message)` (`391`); helper `Task<bool> RejectMessage(Message, MessageRejectionReason)` (`474`).
- `src/Paramore.Brighter.ServiceActivator/Reactor.cs` — `catch (MessageMappingException messageMappingException)` (`~339-344`) **[changed]**; catch-all `catch (Exception e)` (`~345-350`) **[unchanged]**; fall-through `AcknowledgeMessage(message)` (`356`); helper `bool RejectMessage(Message, MessageRejectionReason)` (`407`).
- `src/Paramore.Brighter/MessageRejectionReason.cs` — `record MessageRejectionReason(RejectionReason RejectionReason, string? Description = null)` and `enum RejectionReason { None, Unacceptable, DeliveryError }` (verified; members and the record shape confirmed). Reused as-is; only `Unacceptable` is used by this change.
- `src/Paramore.Brighter.ServiceActivator/MessagePump.cs` — `UnacceptableMessageLimit` (default `0`, `135`), `IncrementUnacceptableMessageCount()` (`176`). Reused as-is.
- `src/Paramore.Brighter.MessagingGateway.AWSSQS.V4/SqsMessageConsumer.cs` — `RejectAsync` (verified `149`): `Unacceptable` → IMQ then DLQ on fallback (`541-547`); `RefreshMetadata` writes `reason.RejectionReason` into the header bag key `rejectionReason` (`501`). **Not changed by this ADR.**

### Technology Choices

**Reason choice (per C-2).** The mapping failure uses `RejectionReason.Unacceptable` — a malformed message the consumer could not even translate, which on SQS V4 routes to the IMQ with DLQ fallback. `RejectionReason.DeliveryError` is **not** used by this change: it would be the natural reason for a dispatch failure, but the catch-all dispatch path is left unchanged per the handler-failure policy, so no path in this change emits it. Crucially, the pump does **not** branch on transport type or on whether an IMQ/DLQ is configured (FR-7). It states intent via the reason; the consumer's reject implementation decides the outcome. This is the knowing/deciding-vs-doing split made concrete in the enum value.

**Shared description local (per C-5).** The `Description` passed to `MessageRejectionReason` must equal the string passed to `processSpan?.SetStatus(ActivityStatusCode.Error, ...)` on the mapping path. In the current source it is an inline `$"..."` interpolation. The implementation extracts it to a single local and reuses it for both `SetStatus` and the rejection reason, so span status and rejection metadata carry identical, operator-visible text containing the message `Id`. The verified literal is:

- **Mapping** (`Proactor.cs:378`, `Reactor.cs:343`): `$"MessagePump: Failed to map message {message.Id} from {Channel.Name} with {Channel.RoutingKey} on thread # {Thread.CurrentThread.ManagedThreadId}"`

The existing `Thread.CurrentThread.ManagedThreadId` thread-id expression is preserved as-is and **not** normalised — normalising would be a change outside the `MessageMappingException` block body, forbidden by C-1. (The catch-all's own "Failed to dispatch …" `SetStatus` string, which uses `Environment.CurrentManagedThreadId`, is untouched because the catch-all body is unchanged.) This observability is what the feature delivers: `RejectMessage` persists the Description-bearing string into the header bag at `Message.RejectionReasonHeaderName` (= `"RejectionReason"`; verified `Proactor.cs:478`, `Reactor.cs:411`), which rides on the message to the IMQ/DLQ unmodified. On SQS V4, `RefreshMetadata` additionally writes the *enum* reason (`reason.RejectionReason.ToString()`) into a separate `rejectionReason` bag key (`SqsMessageConsumer.cs:501`) — so the operator sees both the categorical reason and the Id-bearing Description on the rejection channel.

### Implementation Approach

Conceptually, the mapping path in each pump becomes (design-level — the Reactor drops `await`):

```
// Proactor — catch (MessageMappingException messageMappingException)
var description = $"MessagePump: Failed to map message {message.Id} from {Channel.Name} with {Channel.RoutingKey} on thread # {Thread.CurrentThread.ManagedThreadId}";
Log.FailedToMapMessage(s_logger, messageMappingException, message.Id, Channel.Name, Channel.RoutingKey, Environment.CurrentManagedThreadId);
IncrementUnacceptableMessageCount();
processSpan?.SetStatus(ActivityStatusCode.Error, description);
await RejectMessage(message, new MessageRejectionReason(RejectionReason.Unacceptable, description));
continue;

// Reactor — identical body, minus `await`.

// catch (Exception e) — UNCHANGED in both pumps: log via FailedToDispatchMessage2, set span error,
// increment, then fall through to the bottom-of-loop acknowledge.
```

The existing log call, the existing `processSpan?.SetStatus(...)` call (now reading the shared local), and the existing `finally { Tracer?.EndSpan(processSpan); }` all remain (FR-6); the `finally` still runs because `continue` exits via it. The `RejectMessage` return value is ignored — the path unconditionally `continue`s, exactly as the existing `RejectMessageAction`/`InvalidMessageAction` blocks do (C-4).

**Sequencing:** apply the mapping-block change in `Proactor.cs`, then the equivalent in `Reactor.cs`. No dependency ordering between the two files. No build-graph or migration concerns — single project, no public surface change.

**Testing approach (NFR-4):** drive each pump with a fake/test consumer that records every `Reject`/`Acknowledge` call and the `MessageRejectionReason` passed. For the mapping path, assert exactly one reject with `Unacceptable`, zero fall-through acknowledges for that message, and (AC-10) that the captured `Description` is non-empty, contains the message `Id`, and equals the string passed to `processSpan?.SetStatus(...)`. A regression guard (AC-2/AC-4) drives an unexpected dispatch exception and asserts the catch-all is **unchanged**: zero rejects, exactly one fall-through acknowledge. For the mapping path specifically (AC-12), the exception must be produced by the **real** `TranslateMessage` path — e.g. a missing unwrap transformer registration — not a hand-thrown bare exception, so that any future refactor wrapping the mapping exception is caught (see Risks). Live SQS E2E (AC-9) is optional and non-gating.

## Consequences

### Positive

- Failed-mapping messages are routed through the consumer's reject flow, so on transports that support it they land on the IMQ/DLQ instead of being silently deleted — the operator can inspect, replay, and alert.
- The mapping path now coheres with the action-exception paths that already reject, removing a surprising special case from the pump loop.
- The `Description` text is shared between span status and rejection metadata, so an operator sees the same message-Id-bearing string in tracing and on the rejection queue.
- The handler-failure policy is respected: general dispatch exceptions remain a handler-author concern (action exceptions/attributes), not something the pump silently reclassifies. The change is surgical — one block per pump.
- No public API, configuration, or transport change; nothing for downstream users to migrate. The success path and the catch-all path are untouched.
- Proactor and Reactor are brought to behavioural parity on the mapping path (NFR-1).

### Negative

- A small asymmetry now exists between the two malformed-vs-failed catch blocks: the mapping block rejects while the catch-all acknowledges. This is intentional and policy-driven, but a future reader must understand *why* they differ (captured here and in FR-8) rather than assuming an oversight.
- For transports whose reject implementation performs a plain delete (no IMQ/DLQ), the mapping path's behaviour is functionally unchanged from today (delete still happens) but now flows through a slightly longer code path (`RejectMessage` → consumer `Reject`), adding a little work per failure for no routing benefit on those transports. Accepted per C-3/FR-7 — the pump declares intent; transports without channels do what they did before.
- `RejectMessage` is unguarded on the mapping path (see C-6). If a consumer's reject throws, that exception now propagates out of the loop where previously the path swallowed-then-acknowledged. This is a deliberate consistency choice with the existing reject blocks, but it is a behavioural difference in the error-handling-of-the-error-handler.

### Risks and Mitigations

- **Risk (A-2, most important): a future refactor wraps the mapping exception.** Today a `MessageMappingException` reaches its dedicated catch block because `TranslateMessage` is invoked via a **direct `await`** (verified `Proactor.cs:231`) / direct call (`Reactor.cs:190`), surfacing the **bare** exception — *not* because of catch ordering. The per-message body also has an inner `catch (AggregateException)` (verified `Proactor.cs:237-330`, `Reactor.cs:196-295`) that classifies `InnerExceptions` against the action exceptions but has **no `MessageMappingException` arm**. If a future change caused the mapping exception to arrive wrapped in an `AggregateException`, it would fall through that handler unmatched to the catch-all — which (correctly, by policy) logs-and-acknowledges. The malformed message would then be silently deleted again and never reach the IMQ, re-introducing the exact loss this change fixes.
  - **Mitigation:** this change must introduce no wrapping of the mapping exception. The mapping-path tests (AC-12) must exercise the **real** `TranslateMessage`/unwrap pipeline (e.g. missing transformer), not a hand-thrown bare exception, so a future wrapping refactor breaks the test and is caught at CI rather than in production.
- **Risk: a throwing consumer reject destabilises the loop.** Mitigated by deliberate scoping (C-6): the changed path mirrors the existing unguarded reject blocks; no guard is added here, and the existing blocks are not changed. A throwing reject propagating out of the loop is accepted, consistent behaviour, not a regression introduced by this change.
- **Risk: the catch-all is accidentally altered during implementation.** The change is adjacent to the catch-all block.
  - **Mitigation:** AC-2/AC-4 are explicit regression guards asserting the catch-all still acknowledges (not rejects) on an unexpected dispatch exception, on both pumps.
- **Risk: pre-existing Proactor/Reactor asymmetry outside the mapping block is mistaken for in-scope.** For example, the Reactor's `ConfigurationException` block calls `IncrementUnacceptableMessageCount()` (verified `Reactor.cs:300`) while the Proactor's does not (`Proactor.cs:331-339`).
  - **Mitigation:** A-1 explicitly leaves these as-is. The decision touches only the `MessageMappingException` block per pump.

## Alternatives Considered

1. **Also route the catch-all `Exception` path through `RejectMessage` (with `DeliveryError`).** Considered and rejected. It would override Brighter's [handler-failure policy](https://brightercommand.gitbook.io/paramore-brighter-documentation/using-an-external-bus/handlerfailure), under which a general application exception is logged and acknowledged and DLQ/defer/reject is the handler author's explicit choice (action exception or attribute/policy). Having the pump reclassify an arbitrary unhandled exception as a transport-level rejection takes a decision away from the handler author, would surprise existing applications that rely on the documented log-and-acknowledge behaviour, and conflates "the framework could not translate this message" (a framework-owned, malformed-message verdict) with "a handler threw" (an application concern). The mapping exception is exempt precisely because it arises in the framework's own translate step before any handler runs.

2. **Branch in the pump on whether an IMQ/DLQ is configured, and only reject when one exists (else acknowledge).** Rejected: it makes the pump transport-aware, violating the knowing/doing split (FR-7) — the pump would interrogate consumer/transport topology it has no business knowing — and duplicates routing knowledge that already lives in the consumer's `RejectAsync`.

3. **Add a new `RejectionReason` (e.g. `MappingError`) for the mapping path.** Rejected: NFR-3 forbids enum/type changes, and the existing `Unacceptable` already carries exactly the right routing semantics on SQS V4 (IMQ with DLQ fallback). Adding a value would create a new type without necessity and force every consumer's reject switch to handle it.

4. **Guard `RejectMessage` with try/catch on the mapping path so a throwing reject falls back to acknowledge.** Rejected (C-6): it would diverge from every existing reject block (which are unguarded), introduce an inconsistency, and risk re-introducing silent delete via the fallback acknowledge — the exact behaviour being removed. A throwing reject should surface, consistent with the rest of the loop.

5. **Normalise the mapping block's thread-id expression while editing (use `Environment.CurrentManagedThreadId` to match the catch-all).** Rejected: C-1 confines the change to the `MessageMappingException` block body's behaviour; the existing `SetStatus` string is reused verbatim into the shared local, and gratuitously rewriting it would be an out-of-scope structural change and could churn unrelated tests/log expectations.

6. **Do nothing; document that operators must set a positive `UnacceptableMessageLimit`.** Rejected: the limit only *stops the pump* after N failures; it never routes the lost messages anywhere. Even with a limit set, every failed message before the limit is still silently deleted. It addresses neither observability nor recovery.

## References

- Requirements: [specs/0030-reject_mapping_errors/requirements.md](../../specs/0030-reject_mapping_errors/requirements.md)
- Related ADRs: none — this is the first and only ADR for spec 0030.
- External references: issue #4160 (this symptom — failed-mapping messages silently deleted instead of routed to IMQ/DLQ); related issue #4159 (root cause under investigation — assembly load timing in auto-registration causing the `MessageMappingException`, out of scope here); [Brighter handler-failure policy](https://brightercommand.gitbook.io/paramore-brighter-documentation/using-an-external-bus/handlerfailure).
- Verified source: `src/Paramore.Brighter.ServiceActivator/Proactor.cs`, `src/Paramore.Brighter.ServiceActivator/Reactor.cs`, `src/Paramore.Brighter.ServiceActivator/MessagePump.cs`, `src/Paramore.Brighter/MessageRejectionReason.cs`, `src/Paramore.Brighter.MessagingGateway.AWSSQS.V4/SqsMessageConsumer.cs`.
