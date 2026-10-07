# Bugfix: Claim-check requeue preserves retrieved payload

**Linked Issue**: [#4480](https://github.com/BrighterCommand/Brighter/issues/4480)
**Status**: Fixed (Core verified; live Service Bus verification blocked by missing configuration)

## Symptom

A claimed 300 KB message is received through Proactor and reconstructed for its handler. When the handler throws `DeferMessageAction`, Azure Service Bus requeue publishes the reconstructed body and exceeds the emulator's 256 KB limit.

With `retain: false`, the original queued message also references luggage already deleted during retrieval. With `retain: true`, luggage survives, but requeue still carries the oversized body. Expected behavior is a compact, recoverable claim on requeue.

## Suspected Location

- `src/Paramore.Brighter.ServiceActivator/Proactor.cs:242`, `:546`, `:357`, `:526`: received message flows into unwrapping and then requeue.
- `src/Paramore.Brighter.ServiceActivator/Reactor.cs:204`, `:539`, `:326`, `:517`: equivalent synchronous path.
- `src/Paramore.Brighter/UnwrapPipelineAsync.cs:87` and `src/Paramore.Brighter/UnwrapPipeline.cs:84`: transformations initially receive the original message reference.
- `src/Paramore.Brighter/Transforms/Transformers/ClaimCheckTransformer.cs:154`, `:163`, `:211`, `:220`: body replacement and immediate luggage deletion/header removal.
- `src/Paramore.Brighter.MessagingGateway.AzureServiceBus/AzureServiceBusConsumer.cs:342`: requeue sends the supplied message before acknowledging it at `:365`.
- `src/Paramore.Brighter.ServiceActivator/Proactor.cs:405` and `src/Paramore.Brighter.ServiceActivator/Reactor.cs:373`: normal acknowledgement happens after translation and dispatch.

## Root-Cause Hypothesis

**Initial hypothesis — subsequently confirmed by code trace and regression tests below.**

Both pumps appear to reuse the transport message instance for transformation and requeue. Claim retrieval replaces its body regardless of retention, so deferral republishes the full payload. With retention disabled, deletion occurs before handler outcome or acknowledgement; failed requeue or later redelivery therefore encounters a dangling claim.

The initial proposal combined isolating the mapper's working message with deferring luggage deletion until acknowledgement. Confirmation established that message isolation alone would not preserve deleted luggage.

## Confirmed Root Cause

**CONFIRMED by code trace; diagnosis approved by the user on 2026-10-02.**

Two independent defects combine: unwrapping expands the same message later passed to requeue, and `retain: false` deletes luggage before dispatch or acknowledgement. Copying alone fixes oversized requeue but leaves its claim dangling.

## Evidence

- [x] Separate adversarial confirmation pass traced both public pumps through transformation and requeue.
- Proactor translates then dispatches at `src/Paramore.Brighter.ServiceActivator/Proactor.cs:242`; its reflection call passes the original message at `:546`. Reactor does likewise at `src/Paramore.Brighter.ServiceActivator/Reactor.cs:204` and `:539`.
- `src/Paramore.Brighter/UnwrapPipelineAsync.cs:87` and `src/Paramore.Brighter/UnwrapPipeline.cs:84` preserve that reference.
- `src/Paramore.Brighter/Transforms/Transformers/ClaimCheckTransformer.cs:139` and `:202` resolve bag claims first, otherwise `DataRef`. Both paths replace the body (`:154`, `:211`), even when retained. Non-retained luggage is immediately deleted and claim headers removed (`:163`, `:220`).
- Deferral reuses that message in Proactor (`:357`, `:526`) and Reactor (`:326`, `:517`); aggregate-exception paths also requeue it.
- `src/Paramore.Brighter.MessagingGateway.AzureServiceBus/AzureServiceBusConsumer.cs:358` and `:362` send this expanded message; acknowledgement follows only at `:365`. Send failure therefore leaves the original broker claim.
- [x] Executable regression tests subsequently proved both defects through public pumps, in-memory storage and a capturing channel. The compact-body assertion failed because requeue contained retrieved luggage; the luggage-preservation assertion failed because retrieval had already deleted the entry. Detailed RED and GREEN results appear below.

### Suggested-Fix Assessment

**PARTIAL** — the two proposed directions jointly address the cause, but "delete after acknowledgement" needs a precise boundary: successful final consumption, not the transport acknowledgement that completes a requeue. Failed acknowledgement must preserve luggage.

## Scope Notes

- **Essential parity:** both pumps, both retention modes, and bag/`DataRef` claims. Preserve claim payload and luggage for deferral, failed requeue, `DontAck`, mapper failure, and rejection/DLQ. Those paths remain eligible for redelivery or replay (`Proactor.cs:357–392`; `Reactor.cs:326–360`; Azure rejection dead-letters at `AzureServiceBusConsumer.cs:314`).
- **Acknowledgement limitation:** Azure `AcknowledgeAsync` catches `ServiceBusException` at `AzureServiceBusConsumer.cs:131`; `HandleAsbException` at `:374` only logs. Normal method return currently does not prove successful settlement. Cleanup tied to it must address this or explicitly narrow its guarantee.
- **Lifetime:** translation disposes the pipeline before dispatch (`Proactor.cs:591`; `Reactor.cs:578`). Disposal releases transforms and DI scopes (`TransformPipelineAsync.cs:77–91`; `TransformPipeline.cs:72–75`). Deferred cleanup cannot blindly capture transformer dependencies already disposed.
- **Copy fidelity:** `MessageHeader.Copy()` at `MessageHeader.cs:457` resets handled count/delay and omits `DataRef` and other CloudEvents metadata. Using it unchanged would break `DataRef`-only retrieval and lose metadata.
- **Compatibility:** preserve standalone unwrap semantics, including immediate deletion when `retain: false`; existing assertions explicitly require it (`tests/Paramore.Brighter.Core.Tests/Claims/InMemory/When_a_message_unwraps_a_large_payload_async.cs:46–53`). Pump lifecycle handling must be distinguished from standalone use.
- **Adjacent, not independently required:** async unwrap currently invokes synchronous deletion (`ClaimCheckTransformer.cs:163`). This is secondary to requeue correctness.
- **Workflow:** the user approved the diagnosis before test preparation; `.confirm-approved` records that approval. The configured `opus` model is unavailable in this session; read-only triage and separate confirmation used the available inherited model.

## Regression Test

Prepared 40 xUnit cases. The user approved test execution and implementation on 2026-10-02 (the reply `ყეს`, interpreted as "yes"). Tests follow `.claude/commands/tdd/test-first.md`; no production changes preceded the approved RED runs and guard mutations.

Core tests (paths relative to `tests/Paramore.Brighter.Core.Tests/MessageDispatch/`):

| Test file | Cases | Behavior |
| --- | ---: | --- |
| `When_a_claimed_message_is_deferred_should_requeue_the_original_claim.cs` | 8 | Both pumps, both retention modes, bag and DataRef-only claims; handler receives the full payload while requeue preserves compact body, claim, transport lock, and handled count. |
| `When_a_nonretained_claim_is_deferred_should_keep_luggage_for_redelivery.cs` | 4 | Luggage exists during and after requeue, including aggregate-wrapped deferral; scoped dependencies are eventually disposed. |
| `When_a_claimed_delivery_is_acknowledged_should_apply_retention_after_acknowledgement.cs` | 4 | Luggage exists when acknowledgement begins; non-retained luggage is subsequently deleted, retained luggage survives, and scoped dependencies are disposed. |
| `When_a_deferred_claim_is_redelivered_should_retrieve_luggage_and_complete.cs` | 4 | A real second pump delivery resolves a DataRef-only claim, dispatches the original payload, and applies retention after final acknowledgement. |
| `When_a_claimed_delivery_is_not_completed_should_preserve_luggage.cs` | 10 | Nack, rejection, mapping failure, failed acknowledgement, and failed requeue preserve luggage and original envelope in both pumps. |
| `When_a_claim_is_unwrapped_by_a_pump_should_preserve_mapper_metadata.cs` | 2 | Mapper retains CloudEvents, tracing, workflow/job, transport and retry metadata, and persistence. |

Azure Service Bus tests:

- `tests/Paramore.Brighter.AzureServiceBus.Tests/MessagingGateway/When_service_bus_acknowledgement_fails_should_report_failure_to_the_caller.cs` (8 cases): synchronous/asynchronous acknowledgement propagates lock-loss and service-busy failures, including aggregate-wrapped exceptions. Setup receives through the public consumer; existing I/O substitutes inject the settlement failures without a live broker.

The core fixture uses public Reactor/Proactor, real CommandProcessor/handlers, attribute-discovered ClaimCheckTransformer, InMemoryStorageProvider, and ServiceProviderTransformerFactory with a scoped disposable storage adapter. Its finite in-memory channel captures settlement and can redeliver once; there are no sleeps or private-method access. New mapper request types are distinct, and their closed handler/pipeline generics are registered in `Initializer.cs`.

Observed RED assertions: requeued body equals the compact claim; luggage exists at requeue/acknowledgement and after incomplete delivery; Azure settlement failure reaches the caller. Core produced 28 failures and four passing guards on each of net9.0/net10.0 before implementation. All eight Service Bus tests failed on net10.0 because acknowledgement returned normally.

Two characterization guards were green initially. Both approved production mutations were exercised on net10.0, reverted, and their guards confirmed green before implementation:

1. **IgnoreRetainLuggage**: forced `_retainLuggage = false` in ClaimCheckTransformer.InitializeUnwrap. Both retained acknowledgement cases failed because luggage was absent at acknowledgement. The two non-retained cases remained red as expected.
2. **UseLossyHeaderForMapper**: replaced the working message in both UnwrapPipeline variants with one using `message.Header.Copy()`, explicitly restoring `DataRef`. Both metadata cases failed on Source: expected `https://example.test/claim-source`, actual `http://goparamore.io/`.

Existing standalone claim unwrap tests remain part of the regression suite and protect their immediate-delete contract. An initial metadata assertion used a record's `ToString()` rather than its `Value`; that test-only mistake was corrected before mutation checks and production changes. No behavior assertion was weakened.

Preparation validation: Core and AzureServiceBus test projects compiled for net10.0 with zero errors and no warnings in the added files; the projects report existing warnings elsewhere.

## Fix

- `MessageDelivery` and an internal association on `RequestContext` hold cleanup for one received delivery. Successful dispatch plus final acknowledgement triggers cleanup; disposal discards pending cleanup on all other paths. Request-context copying does not copy delivery ownership.
- Both unwrap pipelines use an independent envelope during pump delivery. Internal copies retain all header fields, bag comparer, baggage, content type and persistence. The existing public `MessageHeader.Copy()` contract remains unchanged.
- ClaimCheckTransformer registers non-retained luggage cleanup during pump delivery. Standalone unwrap still deletes immediately; asynchronous deletion uses the asynchronous storage API.
- Reactor and Proactor retain the unwrap pipeline through dispatch, settlement and cleanup only when cleanup is pending; otherwise they release it before the handler starts. Their outer finally releases the pipeline on every exit and logs release failures without reclassifying delivery.
- AzureServiceBusConsumer logs and rethrows acknowledgement failures, including aggregate-wrapped ServiceBusException, so unsuccessful settlement cannot trigger cleanup.
- RetrieveClaimAttribute documents the deletion boundary. [ADR 0079](../../docs/adr/0079-claim-check-cleanup-after-acknowledgement.md) records the cross-assembly lifecycle contract, compatibility and failure windows.

### Validation

| Check | net9.0 | net10.0 |
| --- | --- | --- |
| New Core cases after fix | 32 passed (included in full suite) | 32 passed |
| New Service Bus acknowledgement cases | 8 passed | 8 passed |
| Full Core suite | 1,538 passed, 7 skipped, 0 failed | 1,538 passed, 7 skipped, 0 failed |
| Full Service Bus suite | 393 passed, 6 skipped, 38 failed | 393 passed, 6 skipped, 38 failed |

Every Service Bus failure reports `ASB ConnectionString or Namespace not set not set` before broker interaction. No live Service Bus integration result is claimed. The bug remains Fixed rather than fully Verified until those 38 cases can run with infrastructure configured.

ServiceActivator (including core) and AzureServiceBus gateway builds pass for netstandard2.0, net8.0, net9.0 and net10.0, with zero warnings/errors. `git diff --check` passes.

Local evidence: TRX files under `/tmp/brighter-4480-results/`; logs `/tmp/brighter-4480-core-confirmed-red.log`, `/tmp/brighter-4480-ignore-retain.log`, `/tmp/brighter-4480-lossy-header.log`, `/tmp/brighter-4480-core-full.log`, `/tmp/brighter-4480-asb-full.log`, and the corresponding build logs. Temporary mutations are absent from the final changes.

### Operational limits

- Broker acknowledgement and storage deletion are not atomic. A crash or cleanup failure after acknowledgement may leave orphan luggage; cleanup failures are logged. Storage expiry or operator cleanup remains necessary.
- Other transports must also propagate acknowledgement failures to provide the same cleanup guarantee. The demonstrated Service Bus suppression is corrected here.
- Mapper and transform scopes with pending claim-check cleanup remain alive through handler dispatch and settlement. Pipelines without pending cleanup retain their original boundary before handler dispatch. Standalone pipeline lifetimes remain controlled by their callers.

### Upstream integration before publication

The branch was rebased onto upstream master `44994d4a1`, including the merged fix for #4481. The failed-requeue case now expects the pump to nack and continue; its original-envelope and luggage-preservation assertions remain intact. After integration, the full Core suite passed with 1,538 passed and seven skipped per framework. Service Bus remained at 393 passed, six skipped and 38 missing-configuration failures per framework. All 40 new cases and all eight upstream requeue-failure cases pass. Updated evidence is in `/tmp/brighter-4480-core-rebased.log`, `/tmp/brighter-4480-asb-rebased.log` and the matching TRX files.

### CI scope-lifetime regression

PR #4497's first CI build failed the existing `TransformScopeEndsBeforeHandlerPipelineBeginsTests` in Extensions.Tests on both frameworks. Keeping every unwrap pipeline alive through dispatch broke the ordinary transform-scope boundary. The existing test also failed locally on net10.0 with `Assert.True` at handler entry before the correction; no test assertions were changed.

`MessageDelivery.HasPendingCleanup` now lets both pumps retain the unwrap pipeline only when deferred claim-check deletion needs its scoped dependencies. Otherwise, the pipeline is released before dispatch and its reference cleared so final cleanup cannot release it twice. Non-retained claim cleanup still runs after successful acknowledgement; retained claims need no extended scope.

Release validation after the correction:

| Suite | net9.0 | net10.0 |
| --- | --- | --- |
| Core, including all 32 claim-check cases | 1,538 passed, 7 skipped | 1,538 passed, 7 skipped |
| Extensions, including the existing scope-lifetime regression | 645 passed | 642 passed |
| Transforms.Adaptors | 33 passed | 33 passed |

All three suites have zero failures. ServiceActivator and core build for netstandard2.0, net8.0, net9.0 and net10.0 with zero warnings or errors. The existing Service Bus verification limits above still apply.

Local evidence: `/tmp/brighter-4497-scope-red-isolated.log`, `/tmp/brighter-4497-extensions-green.log`, `/tmp/brighter-4497-core-green.log`, `/tmp/brighter-4497-transforms-green.log`, and `/tmp/brighter-4497-serviceactivator-build.log`.

### Merge with upstream transport metrics fix

Merged upstream master `7e897fdf2` into the PR branch, preserving the published commit history. The conflicts in Reactor and Proactor were at process-span creation: upstream uses the channel's messaging system, while this branch introduces delivery cleanup state beside the span. Both pumps now keep the transport-specific span and all claim-check delivery state. The Service Bus consumer auto-merge retains both its transport identity and acknowledgement exception propagation.

Release validation after the merge:

- Full Core suite: 1,546 passed, seven skipped, zero failures on each of net9.0 and net10.0, including the upstream transport metrics tests and all claim-check regressions.
- Full Extensions suite: 645 passed on net9.0 and 642 passed on net10.0, with zero failures.
- Service Bus acknowledgement regression tests: eight passed on each framework; these cases require no live broker.
- ServiceActivator and core: netstandard2.0, net8.0, net9.0 and net10.0 builds pass with zero warnings or errors.
- No unresolved merge entries or conflict markers remain; `git diff --check` passes.

Evidence: `/tmp/brighter-4497-merge-core.log`, `/tmp/brighter-4497-merge-extensions.log`, `/tmp/brighter-4497-merge-asb.log`, and `/tmp/brighter-4497-merge-build.log`. The previously documented live Service Bus limitation still applies.
