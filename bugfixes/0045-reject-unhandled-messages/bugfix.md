# Bugfix: Reject received messages without a handler

**Linked Issue**: [#4500](https://github.com/BrighterCommand/Brighter/issues/4500)
**Status**: Verified

## Symptom

A received event whose mapped request type has no handler is acknowledged after an Information log reporting zero pipelines. A received command with no handler raises an exception, but is also acknowledged. Neither reaches the configured rejection destination.

The issue was reproduced with Proactor and dynamic CloudEvents type routing on the Azure Service Bus emulator. Startup pipeline validation is optional and cannot enumerate the request types returned by the routing callback.

## Suspected Location

- `src/Paramore.Brighter/SubscriberRegistry.cs:89`: runtime handler selection.
- `src/Paramore.Brighter/CommandProcessor.cs:489` and `:608`: event pipeline counts.
- `src/Paramore.Brighter/CommandProcessor.cs:1545`: command pipeline validation.
- `src/Paramore.Brighter.ServiceActivator/Reactor.cs:363` and `:374`: catch-all and acknowledgement.
- `src/Paramore.Brighter.ServiceActivator/Proactor.cs:395` and `:406`: catch-all and acknowledgement.

## Root-Cause Hypothesis

An empty event pipeline is treated as successful publication. An empty command pipeline raises an ordinary ArgumentException, which enters the pumps' general exception acknowledgement path. Neither represents missing handlers as an unacceptable received message.

The maintainer recommends rejecting both cases as Unacceptable, following ADR 0061. This suggestion was initially unverified; the code trace below confirms the requested behavior.

## Confirmed Root Cause

Both pumps reuse ordinary command processor dispatch semantics without distinguishing a received message with no selected handler from local publication or an application-handler exception. Event publication allows zero pipelines. Command dispatch throws ArgumentException for zero pipelines, which follows the general acknowledgement policy.

The missing behavior belongs to framework dispatch classification, not the Azure Service Bus transport or message mapping. Rejection as Unacceptable addresses the cause; logging alone would retain message loss.

## Evidence

- Proactor resolves the runtime type at `src/Paramore.Brighter.ServiceActivator/Proactor.cs:559`, maps it at `:574`, and dispatches through SendAsync or PublishAsync at `:119` and `:125`. Reactor dispatches through Send or Publish at `src/Paramore.Brighter.ServiceActivator/Reactor.cs:394`.
- `src/Paramore.Brighter/SubscriberRegistry.cs:91` returns an empty selection for an unknown request type. Registered routing delegates at `:95` can also return no handlers. `src/Paramore.Brighter/PipelineBuilder.cs:194` and `:254` build pipelines from that selection.
- Event publication counts pipelines at `src/Paramore.Brighter/CommandProcessor.cs:489` and `:608`, but neither path requires a nonempty collection. Proactor acknowledges at `src/Paramore.Brighter.ServiceActivator/Proactor.cs:406`; Reactor acknowledges at `src/Paramore.Brighter.ServiceActivator/Reactor.cs:374`.
- Command dispatch checks the count at `src/Paramore.Brighter/CommandProcessor.cs:323` and `:404`. Zero pipelines throw ArgumentException at `:1553`, entering the generic catch blocks at `src/Paramore.Brighter.ServiceActivator/Reactor.cs:363` and `src/Paramore.Brighter.ServiceActivator/Proactor.cs:395` before acknowledgement.
- Existing InvalidMessageAction handling at `src/Paramore.Brighter.ServiceActivator/Reactor.cs:347` and `src/Paramore.Brighter.ServiceActivator/Proactor.cs:379` rejects as Unacceptable, increments the unacceptable count, and skips acknowledgement.
- Startup validation checks fixed RequestType registration metadata at `src/Paramore.Brighter.ServiceActivator/Validation/ConsumerValidationRules.cs:102`.

The code trace is backed by the approved regression tests: on the unchanged implementation, all twelve rejection cases fail because the rejection topic is empty. All six compatibility cases pass.

## Scope Notes

- Cover events and commands through both Reactor and Proactor, including dynamic request-type routing.
- Use actual runtime handler selection, including selectors returning no handlers. Declared handler metadata alone is insufficient.
- Preserve in-process event publication with zero subscribers, explicitly supported by `src/Paramore.Brighter/CommandProcessor.cs:457` and `:567`.
- Preserve nested Send/Publish behavior when a handler reuses its request context. OriginatingMessage is set by the pump (`src/Paramore.Brighter.ServiceActivator/Proactor.cs:487`) and copied with the context (`src/Paramore.Brighter/RequestContext.cs:190`); its presence alone does not identify the initial receiving dispatch.
- Preserve the catch-all policy for general application-handler failures and existing multiple-command-handler validation.
- Reuse the consumer's unacceptable-message policy. The configured transport determines the actual invalid/dead-letter destination; rejection does not guarantee storage when no destination is configured.

## Regression Test

Test file: `tests/Paramore.Brighter.Core.Tests/MessageDispatch/When_a_received_message_has_no_handler_should_reject_as_unacceptable.cs`.

The 18 cases use real CommandProcessor, SubscriberRegistry, JSON mapping, Reactor/Proactor, and InMemoryMessageConsumer:

- Eight cases expect missing-handler commands/events to reach the invalid-message topic, or the dead-letter fallback when no invalid-message topic is configured. They preserve the ID and body and require a handler-specific rejection description.
- Four cases exercise dynamic message type resolution and a registered router that selects no handler for the middle message. They expect rejection of that message, successful handling before and after it, and one selector invocation per request.
- Two compatibility cases preserve local publishing with no subscribers.
- Four compatibility cases preserve nested publishing of an unhandled local event from command/event handlers, using the received request context.

The initial .NET 10 run produced twelve rejection failures and six compatibility passes. Named mutation: temporarily reject every zero-handler Publish/PublishAsync, including ordinary local calls. All six compatibility cases failed on their intended assertions under that mutation, then passed after it was removed. The mutation was removed before implementation, with no production-file diff remaining at that point. All eighteen cases pass with the fix.

Existing command cardinality and application-handler exception tests remain part of the full Core regression suite.

Compilation: `dotnet build tests/Paramore.Brighter.Core.Tests/Paramore.Brighter.Core.Tests.csproj -f net10.0 -m:1 --disable-build-servers --no-restore` succeeded with zero errors. No warnings reference the new test files. Diagnosis and test review were explicitly approved before test execution and implementation.

Final verification:

- Full Core test suite on .NET 9: 1,532 passed, 7 skipped, 0 failed.
- Full Core test suite on .NET 10: 1,532 passed, 7 skipped, 0 failed.
- Both full runs include all 18 new cases. Existing application-handler exception and command-cardinality tests pass.
- ServiceActivator and its Core dependency build successfully for .NET Standard 2.0 and .NET 8 with zero warnings or errors. The full test builds also validate .NET 9 and .NET 10.
- `git diff --check` passes.

Test command: `dotnet test tests/Paramore.Brighter.Core.Tests/Paramore.Brighter.Core.Tests.csproj --no-restore -m:1 --disable-build-servers`.

## Fix

Reactor and Proactor mark the next dispatch as requiring a handler. CommandProcessor consumes that requirement before constructing pipelines and checks the actual selected pipeline count. Zero handlers throw InvalidMessageAction, invoking the existing Unacceptable rejection path and its count, logging, and destination policy. Handler selectors run once per dispatch.

The requirement is consumed before user handlers or selectors run. Nested dispatch and ordinary local publication retain their existing semantics. General application exceptions and multiple-handler command validation retain their existing behavior.

Changed production files:

- `src/Paramore.Brighter.ServiceActivator/Reactor.cs`
- `src/Paramore.Brighter.ServiceActivator/Proactor.cs`
- `src/Paramore.Brighter/CommandProcessor.cs`
- `src/Paramore.Brighter/RequestContext.cs`

The new public RequestContext.RequireHandlerForNextDispatch method carries this per-dispatch requirement across the Core/ServiceActivator assembly boundary. Both production pumps call it; tests reach the behavior through the pumps. The internal consumption method is not exposed to tests. No IAmACommandProcessor or IRequestContext interface change is required.

## PR review follow-up

PR #4501 requested an upstream merge and a release note. The merge from `9b2896dd9` preserves the
claim-check delivery state added to RequestContext and both sets of test logging registrations.
Reactor and Proactor retain upstream's delivery lifetime handling together with the missing-handler
requirement for their initial dispatch.

The release note under `Master` documents the acknowledgement-to-rejection change, transport-specific
destinations, newly counted unhandled events and `UnacceptableMessageLimit`, logging, and context
forwarding by custom command-processor decorators. Its logging description distinguishes the pump's
rejection warning from the handler-specific reason carried in rejection metadata.

Verification after the merge: the full Core suite passes on .NET 9 and .NET 10, with 1,564 passed,
7 skipped, and zero failures on each. This includes the missing-handler, claim-check delivery, and
existing unacceptable-message-limit tests. ServiceActivator and Core build for .NET Standard 2.0
and .NET 8 with zero warnings or errors. `git diff --check` passes.
