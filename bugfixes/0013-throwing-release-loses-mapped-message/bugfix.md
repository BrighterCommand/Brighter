# Bugfix: A throwing mapper/transform release reclassifies a successfully-mapped message

**Linked Issue**: PR #4254 review, Finding #1 (comment 5084864311)
**Status**: Fixed

## Symptom
On the consumer pump, releasing the transform pipeline (mapper + transforms) back to its factories ran
**inside** the `try` whose catch-all wraps every failure in `MessageMappingException`. So a release that
threw — a user mapper/transform `Dispose`/`DisposeAsync` or a custom `IAmAMessageMapperFactory.Release`
that faults, or (on `netstandard2.0`) MS DI's sync scope `Dispose` of an `IAsyncDisposable`-only service —
was reclassified as a **mapping failure** even though the message had already unwrapped correctly.

The pump then treats that as an **Unacceptable** message: `IncrementUnacceptableMessageCount()` +
`RejectMessage(..., RejectionReason.Unacceptable)`. The handler never runs, a message that mapped perfectly
is rejected and discarded, and after `UnacceptableMessageLimit` such messages the pump shuts down. A
cleanup-path bug becomes **silent message loss plus a consumer outage**, on the default consumer
configuration.

This is new behaviour introduced by this PR's disposal-timing change: pre-PR the release ran on the
finalizer thread and never touched the mapping path. The finalizer-crash escalation fixed in round 5 was
the loud half of this hazard; this is the quiet half.

Expected: the request/message is built before the pipeline is released, so a cleanup failure has no bearing
on whether mapping succeeded. A throwing release must be logged, not surfaced into the mapping path.

## Suspected / Confirmed Location
Pipeline disposal happening inside a mapping/build `try` (or a predicate `finally`) at:
- `src/Paramore.Brighter.ServiceActivator/Proactor.cs` — `TranslateMessage` (`await using` inside the try). **🔴 message loss.**
- `src/Paramore.Brighter.ServiceActivator/Reactor.cs` — `TranslateMessage` (`using` inside the try). **🔴 message loss.**
- `src/Paramore.Brighter/OutboxProducerMediator.cs` — `MapMessage`, `MapMessageAsync`, `CreateRequestFromMessage` (send/reply side; less severe — fails loudly to the caller, but still aborts an already-built message).
- `src/Paramore.Brighter/TransformPipelineBuilder.cs` / `TransformPipelineBuilderAsync.cs` — `HasPipeline`/`HasPipelineAsync` release the probe mapper in a `finally`, so a throwing probe release propagates out of the predicate.

## Fix
At each site the pipeline release is moved out of the mapping/build path and its failure is **logged and
swallowed**, so a mapped message is never reclassified:
- **Proactor/Reactor `TranslateMessage`** — the request is built in the `try`; the pipeline is released in
  a `finally` whose own `try/catch` logs a release failure (`Log.FailedToReleasePipeline`). If mapping
  itself failed, the original `MessageMappingException` still wins; if only the release failed, the built
  request is returned as before.
- **Mediator `MapMessage`/`MapMessageAsync`/`CreateRequestFromMessage`** — release moved into a `finally`
  via `ReleasePipeline`/`ReleasePipelineAsync` helpers that log (`Log.FailedToReleasePipeline`) rather than
  abort a send/reply whose message was already produced.
- **`HasPipeline`/`HasPipelineAsync`** — the probe-mapper release is wrapped so a throw is logged
  (`Log.FailedToReleaseProbeMapper`) instead of escaping the predicate.

The "surface release exceptions to the owner" contract (only finalizers swallow) is unchanged for callers
who genuinely own the pipeline via an explicit `Dispose`/`DisposeAsync`; this only stops a release failure
from corrupting the *outcome of mapping a message*.

## Regression Tests (RED-proven)
Each drives a real pump/mediator with a mapper factory whose `Release`/`ReleaseAsync` throws, and asserts
the message still reaches its destination. All three fail against the pre-fix code (stash-verified) and
pass after:
- `MessageDispatch/Proactor/When_a_mapper_release_throws_the_mapped_message_is_still_dispatched_async.cs`
- `MessageDispatch/Reactor/When_a_mapper_release_throws_the_mapped_message_is_still_dispatched.cs`
- `CommandProcessors/Post/When_a_mapper_release_throws_the_message_is_still_posted.cs` — the throwing factory
  also faults the `HasPipeline` probe release, so this one covers both the send-side map disposal and the
  predicate `finally` on the send path.

## Scope Notes
- No defaults changed. The change is confined to where the pipeline is released relative to the
  mapping/build `try`, plus three new `Warning`-level log messages.
- The pre-existing `When_a_pipeline_finalizer_release_throws_it_should_not_escape` covers the finalizer path;
  these cover the explicit-dispose-on-pump/send path the reviewer noted was untested.
