# Bugfix: Consistent resilience context across publish observers

**Linked Issue**: #4392
**Status**: Verified

## Symptom

Publishing with a supplied Polly resilience context uses it when one handler is
registered, but drops it when multiple handlers are registered. The async path
also changes which cancellation token reaches the handler. Outbox confirmation
callbacks create copies that omit the same context.

## Suspected Location

References use baseline `a0990d5dd`.

- `src/Paramore.Brighter/RequestContext.cs:142`: `CreateCopy` omits
  `ResilienceContext`.
- `src/Paramore.Brighter/PipelineBuilder.cs:189` and `:234`: single observers
  receive the original context; multiple observers receive copies.
- `src/Paramore.Brighter/CommandProcessor.cs:481` and `:590`: sync and async
  publish handlers execute concurrently.
- `src/Paramore.Brighter/Policies/Handlers/ResilienceExceptionPolicyHandler.cs:100`
  and `ResilienceExceptionPolicyHandlerAsync.cs:104`: context presence selects
  the Polly execution overload.
- `src/Paramore.Brighter/OutboxProducerMediator.cs:877` and `:974`: independent
  publish-confirmation callbacks copy the context before marking dispatched.

## Root-Cause Hypothesis

**Confirmed by code trace; diagnosis approved.** Observer-count-dependent
copying combines with an undocumented omission of execution-scoped resilience
state. This changes the context passed to Polly and, asynchronously, the token
passed into the handler. It does not remove the resilience pipeline registry:
multiple observers still execute resilience policies, but without the caller's
explicit execution context.

Assigning the original `ResilienceContext` to every copy would share it across
concurrent independent executions. Deliberately omitting it consistently is the
smaller alternative in the issue, but changes single-observer publishing. Fresh
per-execution contexts would require decisions about property copying, token
precedence, pooling, and lifetime ownership.

## Confirmed Root Cause

Both pipeline builders retain the supplied request context for one observer and
call `CreateCopy` for each of multiple observers. That method retains the
resilience pipeline registry but omits its execution context. The resilience
handlers consequently choose different Polly overloads according to observer
count. On the async path, this also changes cancellation-token precedence.

Sharing the original Polly context across concurrent observers is not a safe
fix. Always copying contexts in the shared builder is only a partial solution:
it would also change command context identity and single-observer publishing's
caller-visible bag behavior. Intentional omission versus isolated propagation
remains subject to maintainer review. The local implementation selects
intentional omission for publishing, without changing command behavior or
single-observer caller-visible bag updates. ADR `0074-isolate-publish-resilience-context`
records this as a proposed decision, not an accepted contract.

## Evidence

- `Directory.Packages.props:135` pins Polly 8.7.0. Its installed XML documentation
  defines `ResilienceContext` as single-execution state and warns against reusing
  an instance across executions. The [Polly API documentation](https://www.pollydocs.org/api/Polly.ResilienceContext.html)
  states the same lifetime constraint.
- `ResilienceExceptionPolicyHandlerAsync.cs:104-115` selects the supplied
  resilience context's token when present; otherwise it passes the caller's
  `cancellationToken` through Polly's token-based overload.
- `CommandProcessor.cs:1533-1543` preserves a supplied request context during
  initialization. Publishing passes it to the builders at `:474` and `:581`.
  Their observer-count branches at `PipelineBuilder.cs:189` and `:234` lead to
  the omission at `RequestContext.cs:142-153`.
- The async resilience handler retrieves the retained pipeline registry at
  `ResilienceExceptionPolicyHandlerAsync.cs:77-85`: configured strategies still
  execute even when their explicit execution context is absent.
- Outbox copies at `OutboxProducerMediator.cs:877` and `:974` reach wrappers at
  `:1386-1393` and `:1415-1424`, which also select overloads by context presence.
- Existing tests in `When_A_Request_Context_Is_Provided.cs:121-123` and
  `:151-153` require single-observer publish handlers' bag writes to appear in
  the original context. The constructor at `RequestContext.cs:47-49` clones the
  dictionary, so unconditional copying would break these assertions.
- Regression tests compile on .NET 9 and .NET 10. Each target reports 21 passed
  and four failed: precisely the single-observer publish cases with a supplied
  context, for sync/async and typed/untyped pipelines. Retry observations retain
  the caller's context, operation key, and property instead of omitting them.

## Scope Notes

- Cover `Publish` and `PublishAsync`, with one and multiple observers, and typed
  and untyped resilience pipelines.
- Preserve `Send` and `SendAsync` behavior. They share `PipelineBuilder.Build`
  and `BuildAsync`, so changing the builder's single-observer rule unconditionally
  also changes commands.
- Never clear or return a caller-owned resilience context to the pool merely to
  make publishing consistent. Keep the original request context intact.
- Preserve single-observer caller-visible bag writes. `CreateCopy` also omits
  `Destination`; using it merely to clear resilience state can therefore lose
  unrelated routing metadata. Do not silently expand this issue into a separate
  metadata-copy correction.
- Document and test the intended omission in `CreateCopy`, including the
  independent outbox confirmation paths. Do not assume a broker callback shares
  the original caller's execution lifetime.
- PR #4428 also touches `RequestContext.cs`, but its current diff adds scheduler
  documentation rather than changing `CreateCopy`. Coordinate if modifying
  `CommandProcessor`, which is also touched by active core work.
- The choice between intentional omission and isolated propagation must be
  explicit before implementation. Ian specifically requested a design decision.

## Regression Test

Approved for execution and confirmed red on .NET 9 and .NET 10 before the fix.
The tests select consistent omission for publishing. The local implementation
remains subject to maintainer clarification and review before publication.

- `tests/Paramore.Brighter.Core.Tests/CommandProcessors/When_publishing_should_omit_caller_resilience_context_regardless_of_observer_count.cs`:
  16 publish cases cover sync/async, typed/untyped first observer, one/two
  observers, and supplied/absent execution context. Two-observer cases register
  one typed and one untyped handler. Each handler fails once then succeeds,
  checking that configured retry strategies still execute. Retry callbacks
  capture operation key, custom property, context identity, and token before
  Polly returns internally owned contexts to its pool. Handler observations
  check token precedence and that the caller's execution context stays intact
  during execution. Single-observer bag writes and destination metadata remain
  visible; multi-observer bags remain separate. Eight additional send cases
  preserve the existing command context and cancellation behavior.
- `tests/Paramore.Brighter.Core.Tests/CommandProcessors/When_copying_request_context_should_omit_execution_state_but_preserve_resilience_pipelines.cs`:
  pins deliberate omission, retained pipeline registry, bag independence, and
  unchanged caller-owned Polly state through the public `CreateCopy` API.
- Five dedicated test-double files supply one request and four handlers for the
  shared fan-out scenario. The request type is isolated from other test
  scenarios; the handlers intentionally share it to exercise multiple observers.
  Closed generic logger initialization is registered in `Initializer.cs`.

Verification on baseline `a0990d5dd`, with only the test additions:

- New tests: 25 executed per target, 21 passed and four failed on each target.
  Every failure is the expected `UsesSuppliedContext` mismatch (`false` expected,
  `true` observed) for publishing to one observer with a supplied context.
- Existing context and resilience checks: 213 passed per target, none failed
  or skipped. This is a targeted baseline check, not the full core suite.
- The initial sandboxed .NET 10 invocation could not create MSBuild sockets.
  It was terminated; the approved unsandboxed runs produced the results above.
- TRX results are under `/private/tmp/brighter-4392-test-results`, named
  `4392-red-net9.trx`, `4392-red-net10.trx`, `4392-existing-net9.trx`, and
  `4392-existing-net10.trx`.

Reproduction command (repeat with `-f net9.0`):

```bash
dotnet test tests/Paramore.Brighter.Core.Tests/Paramore.Brighter.Core.Tests.csproj \
  -f net10.0 \
  --filter 'FullyQualifiedName~PublishResilienceContextTests|FullyQualifiedName~RequestContextCopyResilienceTests'
```

Existing-test filter:
`FullyQualifiedName~RequestContextPresentTests|FullyQualifiedName~ExceptionPolicy|FullyQualifiedName~ContextForAHandler`.

No outbox callback integration test has been added. The copy test pins the
omission at that boundary, not end-to-end callback behavior; review callback
coverage once the final implementation scope is agreed.

## Fix

- `CommandProcessor` selects execution-context exclusion only for `Publish` and
  `PublishAsync`, through internal `PipelineBuilder` overloads. Existing public
  builder methods and command dispatch retain their behavior and signatures.
- Internal `PublishRequestContext` delegates request data and writable metadata
  while exposing no Polly execution context. The builder uses it only when an
  observer context contains execution state. It does not mutate or return the
  caller-owned context, and subscriber selection still sees the original context.
- `RequestContext.CreateCopy`, `IRequestContext`, and the public publishing API
  document deliberate omission and async cancellation precedence. Outbox callback
  logic is unchanged.
- Proposed ADR `0074-isolate-publish-resilience-context` records the decision,
  alternatives, and compatibility impact; the ADR index is regenerated.

Post-fix checks:

- .NET 10 targeted regression run: 25 passed, none failed or skipped.
- Core library build: all four target frameworks (`netstandard2.0`, `net8.0`,
  `net9.0`, `net10.0`) succeeded with zero warnings and errors.
- Full core suite on .NET 9 and .NET 10: 1,319 passed, seven existing skips,
  zero failures per framework. This includes all 25 new regression cases.
  Results: `4392-full-core_net9.0_20260927192334.trx` and
  `4392-full-core_net10.0_20260927192335.trx` in the results directory above.
- `git diff --check` passed. Proposed ADR metadata has all seven required keys,
  a matching filename/id and body/frontmatter status, a valid date, and four
  taxonomy tags. Its generated index entry is current.
- No full-solution or external transport integration suite was run; the change
  is confined to core publishing and does not modify any transport or callback.
