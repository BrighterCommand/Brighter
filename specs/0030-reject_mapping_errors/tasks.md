# Tasks — Spec 0030 reject_mapping_errors

> Implementation task list derived from [requirements.md](requirements.md) and
> [ADR 0061](../../docs/adr/0061-reject_mapping_errors.md).
>
> **TDD is MANDATORY.** Each behavioural task below MUST be implemented via the `/test-first`
> command shown in the task. STOP after writing each test and wait for the user to review it in
> their IDE before implementing. Do NOT write tests manually and proceed to implementation.

This is a purely behavioural change (ADR 0061: no structural/Tidy-First step). No structural prep
tasks are warranted — per ADR §"Responsibility-Driven Design framing" the change "carries no
separate structural-change step". Tasks are ordered Proactor-first then Reactor (ADR sequencing),
with regression guards and parity/edge characterization tests after. The only non-test production
edits are the two `MessageMappingException` catch-block bodies (Tasks 1 and 2); the remaining tasks
are characterization tests expected to be green once those land (or to expose a regression if they
are not).

## Test infrastructure & conventions (READ FIRST — decided once, reused by every task)

Two **orthogonal** capture mechanisms are used throughout. They are not two ways of doing the same
thing — one captures *whether the pump routed the message to the invalid-message queue* (reject vs
acknowledge), the other captures *the span the pump emitted*. Decide and wire both up front so
every task uses them the same way (resolves review-tasks findings #1, #2, #3).

**(A) InMemoryMessageConsumer with invalidMessageTopic — the mechanism for reject-vs-ack capture.**
Use `InMemoryMessageConsumer` (from `src/Paramore.Brighter/InMemoryMessageConsumer.cs`) configured
with `invalidMessageTopic`. When `Reject(message, reason)` is called with `RejectionReason.Unacceptable`,
the consumer sets `message.Header.Topic = invalidMessageTopic` and enqueues the message to the
`InternalBus` (`InMemoryMessageConsumer.cs:228-236`). The assertion
`Assert.Single(_bus.Stream(invalidMessageKey))` proves exactly one reject happened;
`Assert.Empty(_bus.Stream(_routingKey))` proves the message was not left on the source queue.
The routing IS the signal — no custom test double is needed.

This is the standard project pattern — see
`When_an_unacceptable_message_is_recieved_async_and_there_is_an_imc.cs` (class
`AsyncMessagePumpUnacceptableMessageInvalidMessageChannelTests`, line 55 for the
`invalidMessageTopic:` constructor-arg wiring) and its sync sibling
`When_an_unacceptable_message_is_recieved_and_there_is_a_imc.cs` (class
`MessagePumpUnacceptableMessageInvalidMessageChannelTests`).

Task 1 (already implemented) shows the exact wiring:
```csharp
_channel = new ChannelAsync(
    new(ChannelName), _routingKey,
    new InMemoryMessageConsumer(_routingKey, _bus, _timeProvider,
        invalidMessageTopic: _invalidMessageKey,
        ackTimeout: TimeSpan.FromMilliseconds(1000)));
// ...
_channel.Enqueue(unmappableMessage);
_channel.Stop(_routingKey);
_messagePump.Run();
_traceProvider.ForceFlush();
Assert.Single(_bus.Stream(_invalidMessageKey));  // reject happened
Assert.Empty(_bus.Stream(_routingKey));           // not left on source
```

**Note on Task 8 (no-IMQ):** when no `invalidMessageTopic` is configured,
`Reject(Unacceptable)` returns `true` without enqueuing — indistinguishable from Acknowledge via
streams (`InMemoryMessageConsumer.cs:228-236`). For Task 8, configure a `deadLetterTopic` instead
of `invalidMessageTopic` to still get routing visibility via `_bus.Stream(deadLetterKey)`, OR
accept a weaker assertion: verify the pump does not crash/hang and the process-span has
`Status == Error` (characterization-level adequacy for that task).

**(B) Span observability wiring — for span status / span-ended / the AC-10 equality.**
The mapping mirrors (`When_a_message_fails_to_be_mapped_to_a_request(_async).cs`) originally
construct the pump with a **null `Tracer`** (e.g. `…, messageMapperRegistry, null, …`), under
which `processSpan` is null and `SetStatus`/`EndSpan` are no-ops — so those mirrors CANNOT observe
span behaviour. To assert the process span (AC-1/AC-3 "span status set to `Error` and the span
ended"; AC-7 "`finally { … EndSpan … }` runs"; AC-10 "Description equals the `processSpan.SetStatus`
string") you MUST wire a real tracer + in-memory exporter, mirroring
`tests/Paramore.Brighter.Core.Tests/Observability/MessageDispatch/When_There_Is_An_Unacceptable_Messages_Close_The_Span.cs`
(class `MessagePumpUnacceptableMessageOberservabilityTests`):
- `var tracer = new BrighterTracer(timeProvider); var instrumentationOptions = InstrumentationOptions.All;`
  passed to BOTH the `CommandProcessor` and the `Proactor`/`Reactor` constructor.
- `Sdk.CreateTracerProviderBuilder().AddSource("Paramore.Brighter").AddInMemoryExporter(exportedActivities).Build()`.
- After `Run()`: `traceProvider.ForceFlush();` then find the **process-operation** activity for the
  message and assert `Status == ActivityStatusCode.Error`, `StatusDescription` carries the mapping
  string, and the activity was exported at all (export ⇒ the span was ended via `EndSpan`).

Task 1 (already implemented) shows the exact wiring — see
`tests/Paramore.Brighter.Core.Tests/MessageDispatch/Proactor/When_a_message_fails_to_be_mapped_to_a_request_async.cs`.

## Tasks

- [x] **TEST + IMPLEMENT: Proactor rejects a mapping failure as Unacceptable instead of acknowledging it**
  - **USE COMMAND**: `/test-first Proactor routes a MessageMappingException through RejectMessage with RejectionReason.Unacceptable and continues, never reaching the fall-through acknowledge`
  - Test location: "tests/Paramore.Brighter.Core.Tests/MessageDispatch/Proactor/"
  - Test file: UPDATED the existing `When_a_message_fails_to_be_mapped_to_a_request_async.cs` (class `MessagePumpFailingMessageTranslationTestsAsync`, fact `When_A_Message_Fails_To_Be_Mapped_To_A_Request_Should_Reject`). Wired with **mechanism (A)**: `InMemoryMessageConsumer` configured with `invalidMessageTopic: _invalidMessageKey` wrapped in `ChannelAsync`; message pre-enqueued then `_channel.Stop(_routingKey)`; `_messagePump.Run()` called synchronously. Wired with **mechanism (B)**: `BrighterTracer` + in-memory exporter. Mapping failure driven through the REAL translate path via `FailingEventMessageMapperAsync`.
  - **RED gate (the assertion that MUST fail before implementing):** `Assert.Single(_bus.Stream(_invalidMessageKey))` — with no `RejectMessage`+`continue`, the pump falls through to acknowledge and the message is NOT routed to the invalid-message topic. The span-status assertions also go red (null-status / no Error) once a real tracer is wired.
  - Test verifies (post-implementation GREEN):
    - The mapping failure reaches the dedicated `catch (MessageMappingException ...)` via the REAL translate path (`FailingEventMessageMapperAsync` → bare unwrapped `MessageMappingException`) — NOT a hand-thrown exception (satisfies AC-12).
    - `Assert.Single(_bus.Stream(_invalidMessageKey))` — exactly one message routed to the invalid-message topic (Reject with `RejectionReason.Unacceptable` was called). (AC-1.)
    - `Assert.Empty(_bus.Stream(_routingKey))` — message not left on the source queue (fall-through `await Acknowledge(message)` was not reached). (AC-1, no-ack clause of AC-7.)
    - Span (mechanism B): the exported process-operation activity has `Status == ActivityStatusCode.Error`, its `StatusDescription` contains the message `Id`, and it was exported (⇒ span ended via `EndSpan`, FR-6 / AC-1 "span ended").
    - The failure is logged via `Log.FailedToMapMessage`.
  - Implementation:
    - In `src/Paramore.Brighter.ServiceActivator/Proactor.cs`, in the `catch (MessageMappingException messageMappingException)` block (verified at ~374-379, currently ending after `processSpan?.SetStatus(...)` with NO reject/continue):
      - Extract the existing inline interpolation into a shared local `description = $"MessagePump: Failed to map message {message.Id} from {Channel.Name} with {Channel.RoutingKey} on thread # {Thread.CurrentThread.ManagedThreadId}"` (preserve `Thread.CurrentThread.ManagedThreadId` verbatim — do NOT normalise, C-1).
      - Pass `description` to the existing `processSpan?.SetStatus(ActivityStatusCode.Error, description)`.
      - Retain `IncrementUnacceptableMessageCount()` and `Log.FailedToMapMessage(...)`.
      - Add `await RejectMessage(message, new MessageRejectionReason(RejectionReason.Unacceptable, description));` (helper at ~474) then `continue;` — mirroring the adjacent `InvalidMessageAction` block at ~370-372.
      - Leave the fall-through `await Acknowledge(message)` (~391) unchanged.
  - References: FR-1, FR-6, FR-7, AC-1, AC-12, ADR-0061 (route mapping via RejectMessage+continue; shared description local; retain counter); touches `Proactor.cs`, updates `When_a_message_fails_to_be_mapped_to_a_request_async.cs`.

- [x] **TEST + IMPLEMENT: Reactor rejects a mapping failure as Unacceptable instead of acknowledging it**
  - **USE COMMAND**: `/test-first Reactor routes a MessageMappingException through RejectMessage with RejectionReason.Unacceptable and continues, never reaching the fall-through acknowledge`
  - Test location: "tests/Paramore.Brighter.Core.Tests/MessageDispatch/Reactor/"
  - Test file: UPDATE the existing `When_a_message_fails_to_be_mapped_to_a_request.cs` (sync equivalent encoding the old ack contract). Re-wire it the same way as Task 1: **mechanism (A)** `InMemoryMessageConsumer` configured with `invalidMessageTopic` wrapped in a sync `Channel`; message pre-enqueued then `Channel.Stop(_routingKey)`; `_messagePump.Run()` called synchronously. **Mechanism (B)** `BrighterTracer` + in-memory exporter. Mapping failure driven via `FailingEventMessageMapper` (sync).
  - **RED gate:** `Assert.Single(_bus.Stream(_invalidMessageKey))` FAILS today (pump falls through to `AcknowledgeMessage`, message not routed to invalid-message topic). Span-status assertions also go red until the prod edit lands.
  - Test should verify (post-implementation GREEN):
    - Mapping failure reaches the dedicated catch via the REAL translate path (`FailingEventMessageMapper` → bare unwrapped `MessageMappingException`) — NOT hand-thrown (AC-12).
    - `Assert.Single(_bus.Stream(_invalidMessageKey))` — exactly one reject (Unacceptable). (AC-3.)
    - `Assert.Empty(_bus.Stream(_routingKey))` — fall-through `AcknowledgeMessage(message)` not reached. (AC-3, no-ack clause of AC-7.)
    - Span: exported process-operation activity `Status == ActivityStatusCode.Error`, `StatusDescription` contains the message `Id`, activity exported (⇒ span ended). Logged via `Log.FailedToMapMessage`. (FR-6, AC-3 "span ended".)
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - In `src/Paramore.Brighter.ServiceActivator/Reactor.cs`, in the `catch (MessageMappingException messageMappingException)` block (verified at ~339-344): apply the identical change to the Proactor task but DROP `await` — `RejectMessage(message, new MessageRejectionReason(RejectionReason.Unacceptable, description));` (helper at ~407) then `continue;`, mirroring the sync `InvalidMessageAction` block at ~335-337.
    - Shared `description` local with the verbatim mapping string (same as Proactor; preserve `Thread.CurrentThread.ManagedThreadId`).
    - Leave fall-through `AcknowledgeMessage(message)` (~356) unchanged.
  - References: FR-3, FR-6, FR-7, AC-3, AC-12, NFR-1, ADR-0061 (Reactor drops await; sequencing Proactor then Reactor); touches `Reactor.cs`, updates `When_a_message_fails_to_be_mapped_to_a_request.cs`.
  - Depends on: prior Proactor task (apply Proactor first per ADR sequencing; no file-level dependency, but keep order).

- [x] **TEST + IMPLEMENT: Rejection Description equals the process-span status string and contains the message Id (both pumps)**
  - **USE COMMAND**: `/test-first the MessageRejectionReason.Description on the mapping path is non-empty, contains the message Id, and is the same string passed to processSpan.SetStatus`
  - Test location: "tests/Paramore.Brighter.Core.Tests/MessageDispatch/Proactor/" and "tests/Paramore.Brighter.Core.Tests/MessageDispatch/Reactor/"
  - Test file: `When_a_message_fails_to_be_mapped_the_rejection_description_matches_the_span_status_async.cs` (Proactor) and `When_a_message_fails_to_be_mapped_the_rejection_description_matches_the_span_status.cs` (Reactor)
  - Wiring: this test REQUIRES both mechanisms — **mechanism (A)** `InMemoryMessageConsumer` with `invalidMessageTopic` to confirm the reject happened AND to read the rejection reason from the routed message: `_bus.Stream(invalidMessageKey).First().Header.Bag[Message.RejectionReasonHeaderName]` contains the description string (the pump writes `message.Header.Bag[Message.RejectionReasonHeaderName] = $"Message rejected reason: {reason.RejectionReason} Description: {reason.Description}"` before delegating). **Mechanism (B)** `BrighterTracer` + in-memory exporter to read the span `StatusDescription`. Construct the pump WITH `new BrighterTracer(timeProvider)` + `InstrumentationOptions.All`, mirroring `Observability/MessageDispatch/When_There_Is_An_Unacceptable_Messages_Close_The_Span.cs` (class `MessagePumpUnacceptableMessageOberservabilityTests`, lines 33-40 for exporter wiring, line 95 `ForceFlush()` then lines 109-110 for reading `activity.Status`/`StatusDescription`). A null-Tracer arrange CANNOT satisfy this test — there is no span string to compare (review-tasks finding #2).
  - Test should verify:
    - From the routed message on the invalid-message queue: `_bus.Stream(invalidMessageKey).First().Header.Bag[Message.RejectionReasonHeaderName]` is non-empty and contains the message `Id` as a substring (derives from the shared `description` local).
    - From the exporter (after `traceProvider.ForceFlush()`): the process-operation activity's `StatusDescription` for the same message EQUALS the `description` local (both derive from the single shared `description` local — this equality is the proof of the C-5 refactor). Equivalently: the bag value contains the `StatusDescription` as a substring (the bag value embeds it with prefix text).
    - The activity's `Status == ActivityStatusCode.Error` and the activity was exported (⇒ span ended — covers AC-7's "EndSpan runs" and AC-1/AC-3's "span ended").
    - Do NOT assert any literal thread-id token (environment-dependent, C-5).
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - No new prod code beyond the shared `description` local introduced in the Proactor/Reactor tasks above. This test PROVES the shared-local refactor (C-5). If the description and span string diverge, the test fails — investigate the shared-local extraction. (Before the prod edit lands, the reject never happens, so there is no rejected message on the IMQ stream to inspect — the test is RED until Tasks 1/2 are implemented.)
  - References: FR-1, FR-3, FR-6, AC-7 (EndSpan/span-ended), AC-10, C-5, ADR-0061 (shared description local); depends on both prior pump tasks.

- [ ] **TEST + IMPLEMENT: Catch-all dispatch exception still acknowledges (async regression guard)**
  - **USE COMMAND**: `/test-first an unexpected dispatch Exception in the Proactor still falls through to Acknowledge and is NOT rejected`
  - Test location: "tests/Paramore.Brighter.Core.Tests/MessageDispatch/Proactor/"
  - Test file: `When_a_dispatch_exception_is_thrown_the_catch_all_acknowledges_async.cs`
  - Test should verify:
    - A non-mapping, non-action `Exception` (e.g. `InvalidOperationException`) thrown during dispatch of message `def-456`.
    - **Mechanism (A)**: `InMemoryMessageConsumer` with `invalidMessageTopic` configured; `Assert.Empty(_bus.Stream(invalidMessageKey))` — zero rejects (message was NOT routed to the invalid-message topic). The source-queue stream is emptied normally by acknowledge. (Use `invalidMessageTopic` so that a spurious reject would be visible; empty stream means only acknowledge was called.)
    - Failure logged via `Log.FailedToDispatchMessage2`; with **mechanism (B)** `BrighterTracer` + exporter wired, the process-operation activity has `Status == ActivityStatusCode.Error` and was exported (span ended). (AC-2 full clause.)
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Existing tests to mirror: `tests/.../MessageDispatch/Proactor/When_an_event_handler_throws_unhandled_exception_Then_message_is_acked_async.cs` (class `MessagePumpEventProcessingExceptionTestsAsync`) and `When_a_command_handler_throws_unhandled_exception_Then_message_is_acked_async.cs` — both already drive the catch-all via `SpyExceptionCommandProcessor` (TestDoubles/SpyCommandProcessor.cs) and assert the Error log "MessagePump: Failed to dispatch message ...". Mirror their arrange but use `InMemoryMessageConsumer` with `invalidMessageTopic` (mechanism A) and add the BrighterTracer+exporter (mechanism B); assert zero rejects via `Assert.Empty(_bus.Stream(invalidMessageKey))` and Error status on the exported activity. NB the existing tests assert via Serilog `TestCorrelator` log capture, which you may retain for the log assertion.
  - Implementation should:
    - Characterization — expected GREEN; no prod code (the catch-all `catch (Exception e)` block at ~380-385 is LEFT UNCHANGED per FR-8). If RED, investigate: the adjacent edit accidentally altered the catch-all.
  - References: FR-8, AC-2, NFR-2, ADR-0061 (catch-all unchanged; regression guard); touches `Proactor.cs` catch-all only as a guard.

- [ ] **TEST + IMPLEMENT: Catch-all dispatch exception still acknowledges (sync regression guard)**
  - **USE COMMAND**: `/test-first an unexpected dispatch Exception in the Reactor still falls through to AcknowledgeMessage and is NOT rejected`
  - Test location: "tests/Paramore.Brighter.Core.Tests/MessageDispatch/Reactor/"
  - Test file: `When_a_dispatch_exception_is_thrown_the_catch_all_acknowledges.cs`
  - Test should verify:
    - `InvalidOperationException` thrown during dispatch of message `jkl-012`.
    - **Mechanism (A)**: `InMemoryMessageConsumer` with `invalidMessageTopic`; `Assert.Empty(_bus.Stream(invalidMessageKey))` — zero rejects. The source-queue stream is emptied normally by acknowledge.
    - Logged via `Log.FailedToDispatchMessage2`; with **mechanism (B)** BrighterTracer+exporter wired, exported process activity `Status == ActivityStatusCode.Error` and exported (span ended). (AC-4 full clause.)
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Existing tests to mirror: `tests/.../MessageDispatch/Reactor/When_an_event_handler_throws_unhandled_exception_Then_message_is_acked.cs` (class `MessagePumpEventProcessingExceptionTests`) and `When_a_command_handler_throws_unhandled_exception_Then_message_is_acked.cs` (class `MessagePumpCommandProcessingExceptionTests`) — sync equivalents of the Proactor pair above (`SpyExceptionCommandProcessor`, "Failed to dispatch message" Error log). Mirror their arrange but use `InMemoryMessageConsumer` with `invalidMessageTopic` + BrighterTracer/exporter; assert zero rejects via `Assert.Empty(_bus.Stream(invalidMessageKey))` and Error status on the exported activity.
  - Implementation should:
    - Characterization — expected GREEN; no prod code (catch-all at ~345-350 unchanged per FR-8). If RED, investigate accidental edit.
  - References: FR-8, AC-4, NFR-2, ADR-0061 (catch-all unchanged); touches `Reactor.cs` catch-all only as a guard.

- [ ] **TEST + IMPLEMENT: Unacceptable limit trips after N mapping rejections**
  - **USE COMMAND**: `/test-first with UnacceptableMessageLimit=3, three consecutive MessageMappingExceptions each reject and increment, and the pump terminates on the next iteration`
  - Test location: "tests/Paramore.Brighter.Core.Tests/MessageDispatch/Proactor/" (async) — extend coverage in Reactor too if a parallel file does not already assert reject
  - Test file: UPDATE/repurpose existing `When_a_message_fails_to_be_mapped_to_a_request_and_the_unacceptable_message_limit_is_reached_async.cs` and its sync sibling `When_a_message_fails_to_be_mapped_to_a_request_and_the_unacceptable_message_limit_is_reached.cs`
  - Test should verify:
    - `UnacceptableMessageLimit = 3`, window unset; three consecutive real-translate mapping failures each trigger a reject with `Unacceptable` and increment the count.
    - On the iteration after the third failure, `UnacceptableMessageLimitReached()` returns true and the pump terminates (4th message not dispatched).
    - **Mechanism (A)**: `InMemoryMessageConsumer` with `invalidMessageTopic`; strengthen the existing assertions to confirm each failure was REJECTED (message routed to `invalidMessageKey`) in addition to the limit-trip behaviour they already assert: `Assert.Equal(3, _bus.Stream(invalidMessageKey).Count())` (three rejects).
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Existing tests to mirror: the files to UPDATE are `tests/.../MessageDispatch/Proactor/When_a_message_fails_to_be_mapped_to_a_request_and_the_unacceptable_message_limit_is_reached_async.cs` (class `MessagePumpUnacceptableMessageLimitTestsAsync`) and the sync sibling (class `MessagePumpUnacceptableMessageLimitTests`) — they already drive 3 mapping failures via `FailingEventMessageMapper(Async)` with `UnacceptableMessageLimit = 3`, but currently only assert `Assert.Empty(_bus.Stream(...))` with a `//TODO: How do we assert channel closure?`. Borrow the missing assertion from `When_an_unacceptable_message_limit_is_reached_async.cs` (class `MessagePumpUnacceptableMessageLimitBreachedAsyncTests`) / sync `When_an_unacceptable_message_limit_is_reached.cs` (class `MessagePumpUnacceptableMessageLimitBreachedTests`): `Assert.Equal(MessagePumpStatus.MP_LIMIT_EXCEEDED, _messagePump.Status)` (enum in `src/Paramore.Brighter.ServiceActivator/MessagePump.cs`). Add `invalidMessageTopic` to the existing consumer wiring and assert the per-failure reject count.
  - Implementation should:
    - Characterization for the limit-trip — the guardrail is unchanged; the reject behaviour comes from the Proactor/Reactor tasks above. No NEW prod code in `MessagePump.cs`. If the limit no longer trips, investigate: `IncrementUnacceptableMessageCount()` was dropped from the mapping block.
  - References: FR-5, AC-5, ADR-0061 (retain IncrementUnacceptableMessageCount); depends on Proactor/Reactor tasks.

- [ ] **TEST + IMPLEMENT: Default limit of 0 never trips despite repeated mapping rejections**
  - **USE COMMAND**: `/test-first with the default UnacceptableMessageLimit=0, many consecutive MessageMappingExceptions each reject and increment but the pump never terminates due to the limit`
  - Test location: "tests/Paramore.Brighter.Core.Tests/MessageDispatch/Proactor/"
  - Test file: `When_the_unacceptable_message_limit_is_zero_mapping_failures_never_trip_the_limit_async.cs` (and Reactor sibling if not already covered)
  - Test should verify:
    - `UnacceptableMessageLimit = 0` (default); a large run (e.g. 100) of real-translate mapping failures each reject with `Unacceptable` and increment.
    - **Mechanism (A)**: `InMemoryMessageConsumer` with `invalidMessageTopic`; `Assert.Equal(100, _bus.Stream(invalidMessageKey).Count())` — all 100 failures routed to the invalid-message topic (each was rejected).
    - The pump does NOT terminate due to the limit (loop continues until externally stopped via `MT_QUIT`).
    - `Assert.NotEqual(MessagePumpStatus.MP_LIMIT_EXCEEDED, _messagePump.Status)` after the run.
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Existing tests to mirror: `tests/.../MessageDispatch/Proactor/When_an_unacceptable_message_is_recieved_async.cs` (class `AsyncMessagePumpUnacceptableMessageTests`) / sync `When_an_unacceptable_message_is_recieved.cs` (class `MessagePumpUnacceptableMessageTests`) — these run with the DEFAULT limit (`UnacceptableMessageLimit` unset = 0) and the pump only stops via an enqueued `MT_QUIT`, never via the limit. Mirror their default-limit arrange and QUIT-driven stop; swap the `MT_UNACCEPTABLE` message for a real mapping failure (`FailingEventMessageMapper(Async)`) with `invalidMessageTopic` wired (mechanism A) and assert reject count + `Status != MP_LIMIT_EXCEEDED`.
  - Implementation should:
    - Characterization — expected GREEN; no new prod code (`UnacceptableMessageLimitReached()` already returns false when limit <= 0). If RED, investigate.
  - References: FR-5, AC-6, ADR-0061; depends on Proactor/Reactor tasks.

- [ ] **TEST + IMPLEMENT: No-IMQ consumer — pump still delegates reject without transport branching**
  - **USE COMMAND**: `/test-first a consumer with no IMQ or DLQ configured still receives a Reject call (not Acknowledge) on a mapping failure`
  - Test location: "tests/Paramore.Brighter.Core.Tests/MessageDispatch/Proactor/" and "tests/Paramore.Brighter.Core.Tests/MessageDispatch/Reactor/"
  - Test file: `When_a_message_fails_to_be_mapped_with_no_imq_the_pump_still_delegates_reject_async.cs` (and sync sibling)
  - Test should verify:
    - `InMemoryMessageConsumer` configured with NO `invalidMessageTopic` and WITH a `deadLetterTopic` instead (so routing visibility is preserved: `Reject(Unacceptable)` routes to the dead-letter topic, which IS distinct from acknowledge). Alternatively, if the test framework supports it, configure `deadLetterTopic` on the consumer and assert `Assert.Single(_bus.Stream(deadLetterKey))`.
    - On a real-translate mapping failure for message `mno-345`: exactly one reject happened (message routed to the dead-letter topic); zero fall-through acknowledge (source stream empty / message not left there).
    - Assert the PUMP invoked reject (not acknowledge) — what the consumer's reject then does to the source (e.g. plain delete) is out of scope (C-3). No transport-specific branching exists in the pump.
    - **If neither `invalidMessageTopic` nor `deadLetterTopic` routing provides adequate visibility**, accept the weaker characterization: process span has `Status == ActivityStatusCode.Error` (mechanism B) and the pump does not crash or hang. Note the limitation in a comment.
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Existing tests to mirror: the no-IMQ vs IMQ pair for `MT_UNACCEPTABLE` shows the consumer-setup contrast — `tests/.../MessageDispatch/Proactor/When_an_unacceptable_message_is_recieved_async.cs` (class `AsyncMessagePumpUnacceptableMessageTests`, NO IMQ) vs `When_an_unacceptable_message_is_recieved_async_and_there_is_an_imc.cs` (class `AsyncMessagePumpUnacceptableMessageInvalidMessageChannelTests`, with `invalidMessageTopic`); sync equivalents `When_an_unacceptable_message_is_recieved.cs` and `When_an_unacceptable_message_is_recieved_and_there_is_a_imc.cs`. Mirror the no-IMQ arrange but drive the mapping failure (`FailingEventMessageMapper(Async)`) and substitute `deadLetterTopic` for routing visibility.
  - Implementation should:
    - No NEW production code — delegation already follows from the Proactor/Reactor tasks. No custom test double is needed (routing IS the signal via `deadLetterTopic`).
  - References: FR-7, NFR-2, NFR-4, AC-8, ADR-0061 (no transport branching); depends on Proactor/Reactor tasks.

- [ ] **TEST + IMPLEMENT: Async/sync parity on the mapping reject path**
  - **USE COMMAND**: `/test-first the Proactor and Reactor reject an identical MessageMappingException scenario identically — same Unacceptable reason, both retain the counter, both continue past acknowledge`
  - Test location: "tests/Paramore.Brighter.Core.Tests/MessageDispatch/Proactor/" and "tests/Paramore.Brighter.Core.Tests/MessageDispatch/Reactor/"
  - Test file: `When_the_mapping_reject_path_is_compared_across_pumps_async.cs` (and the Reactor sibling), or a shared-scenario theory if the test layout supports it
  - Test should verify (via **mechanism (A)** `InMemoryMessageConsumer` with `invalidMessageTopic`, one consumer instance per pump):
    - Same real-translate mapping scenario run against both pumps: each pump's `_bus.Stream(invalidMessageKey)` contains exactly one message (exactly one reject per pump with `RejectionReason.Unacceptable`).
    - Both pumps increment the unacceptable count; both `continue` past the bottom-of-loop acknowledge; neither pump's source stream retains the failed message (the AC-7 no-fall-through clause, asserted symmetrically via `Assert.Empty(_bus.Stream(_routingKey))`).
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - Characterization — expected GREEN once both pump edits land; no new prod code. If RED, investigate divergence between the two pump bodies (the only intended difference is `await`).
  - References: NFR-1, AC-7 (no-fall-through clause), AC-11, ADR-0061 (parity); depends on both pump tasks.

- [ ] **(OPTIONAL — NON-GATING) IMPLEMENT: Live SQS V4 end-to-end confirmation of IMQ/DLQ routing on mapping failure**
  - This is NOT a blocking task and NOT a TDD unit task. It is an optional manual/integration confirmation against real AWS SQS V4 (per AC-9, NFR-4 — the required gate is the in-memory unit tests above).
  - Confirm: a message that fails mapping appears on the configured Invalid Message Queue (or DLQ on fallback) and is removed from the source queue via the reject flow, not silently deleted.
  - Do NOT make CI block on this; do NOT add live-AWS dependencies to the unit gate.
  - References: AC-9 (optional/non-gating), FR-7, NFR-2.

## Coverage cross-reference

### Functional Requirements
- FR-1 (Proactor mapping→reject Unacceptable+continue): Task 1.
- FR-2: INTENTIONALLY WITHDRAWN (tombstone) — no task, correctly omitted.
- FR-3 (Reactor mapping→reject Unacceptable+continue): Task 2.
- FR-4: INTENTIONALLY WITHDRAWN (tombstone) — no task, correctly omitted.
- FR-5 (retain counter / guardrail): Task 6 (limit=3 trips), Task 7 (limit=0 never trips).
- FR-6 (logging+tracing+EndSpan preserved): Tasks 1, 2 (assert Error span status + span-ended via BrighterTracer+exporter), 3 (asserts the span `StatusDescription` equals the rejection `Description`). Span coverage depends on mechanism (B) tracer wiring — the mapping mirrors originally used a null tracer and have been upgraded.
- FR-7 (no transport branching — delegated): Tasks 1, 2, 8.
- FR-8 (catch-all UNCHANGED, regression-guarded): Tasks 4 (async), 5 (sync).

### Non-functional Requirements
- NFR-1 (Proactor/Reactor parity on mapping path): Task 9 (and 2).
- NFR-2 (no transport/consumer changes): Tasks 4, 5, 8.
- NFR-3 (no public API change): No standalone task — a non-additive "do-not-change" constraint enforced by all impl tasks (mapping-block body only; reuses existing `RejectMessage`/`MessageRejectionReason`/`RejectionReason`).
- NFR-4 (fake/in-memory consumer, no live AWS): `InMemoryMessageConsumer` with `invalidMessageTopic` (mechanism A, built in Task 1) is the primary reject-vs-ack capture across Tasks 1, 2, 3, 4, 5, 6, 7, 8, 9; all unit tasks use in-memory/fake consumers; Task 10 isolates live AWS as optional only.

### Acceptance Criteria
- AC-1 (Proactor mapping reject + log + span Error + span ended): Task 1 — `Assert.Single(_bus.Stream(invalidMessageKey))` (one reject, mechanism A); `Assert.Empty(_bus.Stream(_routingKey))` (no ack); BrighterTracer+exporter asserts span `Error` status + span exported (ended); `Log.FailedToMapMessage` asserted.
- AC-2 (catch-all unchanged async — regression): Task 4 (`Assert.Empty(_bus.Stream(invalidMessageKey))` — zero rejects; exporter: span Error/ended).
- AC-3 (Reactor mapping reject + log + span Error + span ended): Task 2 — same pattern as AC-1 for the sync pump.
- AC-4 (catch-all unchanged sync — regression): Task 5 (mechanism A zero-reject assertion + exporter).
- AC-5 (limit=3 trips): Task 6.
- AC-6 (limit=0 never trips): Task 7.
- AC-7 (finally/EndSpan runs, no fall-through ack): EndSpan/span-ended asserted via the exporter in Tasks 1, 2, 3 (an exported activity ⇒ `EndSpan` ran); no-fall-through-ack asserted via `Assert.Empty(_bus.Stream(_routingKey))` in Tasks 1, 2, 9.
- AC-8 (no-IMQ delegation, no transport branching): Task 8 (`deadLetterTopic` routing or span-status characterization).
- AC-9 (live SQS E2E — OPTIONAL, NON-GATING): Task 10.
- AC-10 (Description == span StatusDescription, contains Id): Task 3 — rejected message's `Header.Bag[Message.RejectionReasonHeaderName]` supplies the description string; exporter supplies the span `StatusDescription`; the test asserts equality (both derive from the single shared `description` local).
- AC-11 (async/sync parity): Task 9 (`Assert.Single(_bus.Stream(invalidMessageKey))` per pump).
- AC-12 (mapping exception via REAL translate path, not hand-thrown): Tasks 1, 2.

### ADR-0061 decisions
- Route mapping via RejectMessage+continue both pumps: Tasks 1, 2.
- Shared description local: Tasks 1, 2 (introduced), Task 3 (proves equality span↔reason).
- Retain IncrementUnacceptableMessageCount: Tasks 1, 2 (retained), Task 6 (proves guardrail still trips).
- Catch-all unchanged: Tasks 4, 5.
- No new types / no branching: Tasks 1, 2, 8. (No custom recording test double — `InMemoryMessageConsumer` with `invalidMessageTopic` is the standard project pattern, not a new production type — NFR-3 unaffected.)
- Reactor drops await: Task 2.
- Sequencing Proactor then Reactor: encoded in task order (Task 1 before Task 2) and dependency notes.

### Scope-creep / gap check
No task introduces behaviour outside the FR/NFR/AC/ADR set. No task touches `SqsMessageConsumer`,
`MessageRejectionReason`, `RejectionReason`, the catch-all body, or the catch-block ordering. The
only non-test prod edits are the two mapping-block bodies (Tasks 1, 2). NFR-3 is the sole
requirement with no dedicated task, by design (a "do not change" constraint enforced across all
impl tasks). No gaps: every FR (FR-1..FR-8, FR-2/FR-4 withdrawn), every NFR, every AC
(AC-1..AC-12, AC-9 optional), and every ADR-0061 decision maps to at least one task.
