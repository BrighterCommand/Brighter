# Bugfix: Pump-lifetime "begin" span polluting messaging.client.operation.duration histogram

**Linked Issue**: #4086
**Status**: Verified

## Symptom
Every time a consumer message pump (`Reactor.Run` or `Proactor.EventLoop`) shuts down, a massive outlier — equal to the pump's wall-clock lifetime (minutes to hours, potentially days) — is recorded into the `messaging.client.operation.duration` histogram (defined in `src/Paramore.Brighter/Observability/MessagingMeter.cs:50`).

Expected: the `messaging.client.operation.duration` histogram should only capture genuine per-operation client durations (`publish`, `receive`), so its p50/p95/p99 percentiles remain usable for SLOs. Observed: percentiles are skewed and unusable because the long-lived pump "begin" span lifetime is recorded as if it were a client operation.

Reproduction (conceptual): start a consumer pump with messaging metrics enabled, let it run for a measurable interval, then stop it. On shutdown a single histogram sample equal to the pump's lifetime appears in `messaging.client.operation.duration`.

## Suspected Location
- `src/Paramore.Brighter/Observability/BrighterMetricsFromTracesProcessor.cs:73-75` — the `default` arm of the `switch (operation)` calls `messagingMeter.RecordClientOperation(activity)` for any unrecognised operation. The `operation` value is read from the `messaging.operation.type` tag at `src/Paramore.Brighter/Observability/BrighterMetricsFromTracesProcessor.cs:58` (constant `MessagingOperationType = "messaging.operation.type"` at `src/Paramore.Brighter/Observability/BrighterSemanticConventions.cs:93`). The explicit cases handle `"publish"`, `"receive"`, `"process"` (`:62`, `:66`, `:70`); only `"begin"` falls through to `default`.
- `src/Paramore.Brighter/Observability/BrighterSpanExtensions.cs:69` — `MessagePumpSpanOperation.Begin` maps to the string `"begin"` via `ToSpanName()`.
- `src/Paramore.Brighter/Observability/BrighterTracer.cs:548-582` — `CreateMessagePumpSpan` only accepts `MessagePumpSpanOperation.Begin` (throws otherwise at `:554-555`) and sets `messaging.operation.type` to `operation.ToSpanName()` i.e. `"begin"` at `:565`. This confirms the Begin span carries `messaging.operation.type = "begin"`, which is exactly the value that hits the `default` arm.
- `src/Paramore.Brighter/Observability/MessagingMeter.cs:162-167` — `RecordClientOperation` records `activity.Duration.TotalSeconds` into the `_clientOperationDurationHistogram` named `"messaging.client.operation.duration"` (`:47-52`).
- Pump span open sites (live for full pump lifetime):
  - `src/Paramore.Brighter.ServiceActivator/Reactor.cs:97` (open) — `Tracer?.CreateMessagePumpSpan(MessagePumpSpanOperation.Begin, ...)`; closed at `src/Paramore.Brighter.ServiceActivator/Reactor.cs:364` via `Tracer?.EndSpan(pumpSpan)` (issue cited `:358`; the `EndSpan(pumpSpan)` call is actually at `:364`, just after the `do/while` loop ends — same code path).
  - `src/Paramore.Brighter.ServiceActivator/Proactor.cs:138` (open) — same Begin span; closed at `src/Paramore.Brighter.ServiceActivator/Proactor.cs:399` via `Tracer?.EndSpan(pumpSpan)` (the matching close at the end of `EventLoop`).

## Root-Cause Hypothesis
The `BrighterMetricsFromTracesProcessor.OnEnd` `switch` on `messaging.operation.type` has a `default` arm (`src/Paramore.Brighter/Observability/BrighterMetricsFromTracesProcessor.cs:73-75`) that calls `RecordClientOperation`. The message-pump lifetime span created with `MessagePumpSpanOperation.Begin` carries `messaging.operation.type = "begin"` (set at `BrighterTracer.cs:565` via `ToSpanName()` → `"begin"` at `BrighterSpanExtensions.cs:69`). Because `"begin"` matches none of the explicit `publish`/`receive`/`process` cases, it falls through to `default`, and the span's `activity.Duration` — which spans the entire pump lifetime (opened at `Reactor.cs:97` / `Proactor.cs:138`, closed at `Reactor.cs:364` / `Proactor.cs:399`) — is recorded into `messaging.client.operation.duration` (`MessagingMeter.cs:162-167`). This produces the multi-hour outlier and skews the histogram percentiles.

Falsifiable prediction: if a `MessagePumpSpanOperation.Begin` activity (tag `messaging.operation.type = "begin"`) is passed through `BrighterMetricsFromTracesProcessor.OnEnd`, `IAmABrighterMessagingMeter.RecordClientOperation` will be invoked exactly once with that activity's full duration. Removing/short-circuiting the `default` arm would prevent that invocation while leaving `publish`/`receive`/`process` recording unchanged.

Proposed fix (from the issue) — **UNVERIFIED — to be proven or refuted in /bugfix:confirm**:
1. Drop the `default` arm so unknown operations are not counted as client operations (issue's preferred safer minimum); or
2. Special-case `"begin"` to no-op (optionally recording into a separate pump-lifecycle gauge).
In either case the Begin span is still emitted as a trace span; it simply should not feed the client-operation duration metric.

## Confirmed Root Cause
**CONFIRMED** (code-trace). `BrighterMetricsFromTracesProcessor.OnEnd` records the consumer message-pump "begin" span into the client-operation-duration histogram via the `default` arm of the inner `switch (operation)`. Because that span is opened at pump start and closed at pump shutdown, its `Activity.Duration` equals the entire pump wall-clock lifetime, producing a massive outlier in `messaging.client.operation.duration`.

## Evidence
Code-trace (executable repro would need a live broker; trace is the accepted path here):

1. Pump span opened at pump start (`Reactor.cs:97` `Tracer?.CreateMessagePumpSpan(MessagePumpSpanOperation.Begin, ...)` inside `Run()`), closed only after the `do...while(true)` loop exits at shutdown (`Reactor.cs:364` `Tracer?.EndSpan(pumpSpan)`). Same pattern in `Proactor.cs`. The span lives the full pump lifetime.
2. `BrighterTracer.CreateMessagePumpSpan` (`BrighterTracer.cs:548-582`) builds tags via `GetNewTagsCollection(options)` (`:561`), which defaults `domain` to `MessagingInstrumentationDomain` ("messaging") and adds `instrumentation.domain` when options != None (`BrighterTracer.cs:1141-1148`, tag `:1146`). It also adds `messaging.operation.type = operation.ToSpanName()` when the `RequestInformation` flag is set (`:563-566`). `MessagePumpSpanOperation.Begin.ToSpanName()` => `"begin"` (`BrighterSpanExtensions.cs:65-71`, `:69`). Activity started with `startTime` captured at pump start (`:559`, `:576`).
3. `EndSpan` finalizes duration: `span.SetEndTime(...)` (`BrighterTracer.cs:799`) then `span.Dispose()` (`:800`). `Duration = endTime - startTime` = full pump lifetime.
4. Processor subscribes to the same ActivitySource: `AddProcessor<BrighterMetricsFromTracesProcessor>()` (`BrighterTracerBuilderExtensions.cs:24`) alongside `builder.AddSource(brighterTracer.ActivitySource.Name)` (`:18`); processor caches `_brighterActivitySourceName = brighterTracer.ActivitySource.Name` (`BrighterMetricsFromTracesProcessor.cs:41`).
5. **Adversarial guard check** in `OnEnd` (`BrighterMetricsFromTracesProcessor.cs:45-85`): the only pre-switch guards are `!Enabled` (`:47`), `activity == null` (`:49`), source-name mismatch (`:51`), and missing `instrumentation.domain` (`:53`). The begin span passes ALL of them. No ActivityKind filter, no begin-specific exclusion. Enters `case MessagingInstrumentationDomain` (`:57`), reads `operation = "begin"` (`:58`), matches none of `"publish"/"receive"/"process"` (`:62/:66/:70`), hits `default: messagingMeter.RecordClientOperation(activity)` (`:73-74`).
6. `RecordClientOperation` records `activity.Duration.TotalSeconds` into histogram `name: "messaging.client.operation.duration"`, `unit: "s"` (`MessagingMeter.cs:47-52`, `:162-167`). Name and unit match the symptom.

Bug manifests under the default `InstrumentationOptions.All` config (sets `RequestInformation`, so the tag is present). Does NOT manifest when: processor unregistered (requires both `IAmABrighterMessagingMeter` AND `IAmABrighterDbMeter` — `BrighterTracerBuilderExtensions.cs:23-24`), `InstrumentationOptions.None` (tag absent, `is string operation` guard short-circuits at `:58`), or no ActivitySource listeners (activity null).

## Scope Notes
- **The issue's preferred fix (option 1 — drop the `default` arm) is a REGRESSION.** The `default` arm is NOT exclusive to `"begin"`. Command-processor / producer spans with operation values from `CommandProcessorSpanOperation.ToSpanName()` (`BrighterSpanExtensions.cs:37-47`) — `create`, `deposit`, `send`, `clear`, `archive`, `scheduler` (only `publish` has an explicit case) — plus the confirmation span's `"settle"` (`BrighterTracer.cs:733,741`) are all genuine bounded client operations created via `CreateSpan`/`CreateConfirmationSpan` that currently rely on the `default` arm to be recorded. Dropping it would silently stop recording them.
- **Correct fix: option 2 — special-case `"begin"` to no-op** (e.g. `case "begin": break;`). Surgical: removes only the pump-lifetime pollution while leaving `create/deposit/send/clear/archive/scheduler/settle/publish/receive/process` recording unchanged.
- The fix is backend-agnostic (single `switch` in `BrighterMetricsFromTracesProcessor.cs:60-76`), so it covers both Reactor (`Reactor.cs`) and Proactor (`Proactor.cs:138` open / matching `EndSpan` close) automatically.
- `CreateMessagePumpExceptionSpan` (`BrighterTracer.cs:594-610`) can also emit `messaging.operation.type="begin"`; option 2 suppresses that too (short-lived, not the primary defect).
- No other defect in `RecordClientOperation`/`Filter` (`MessagingMeter.cs:162-167`, `TagObjectsExtensions.cs:42`); the bug is purely operation-type routing.
- **Regression test must assert the no-regression direction too**: ending a span with operation type `create`/`send`/etc. (or at minimum one non-publish/receive/process op) STILL invokes `RecordClientOperation`, so the fix doesn't over-suppress.

## Regression Test
Processor-level unit tests (no broker needed), under `tests/Paramore.Brighter.Core.Tests/Observability/Metrics/`:

- `When_ending_a_message_pump_begin_span_should_not_record_a_client_operation.cs` (`BeginSpanMetricsTests`) — **the red regression test**. Builds a real begin span via `tracer.CreateMessagePumpSpan(Begin, …)`, ends it, calls `processor.OnEnd`, and asserts `SpyMessagingMeter.RecordClientOperationCallCount == 0`. Was red before the fix (Actual: 1) and green after.
- `When_ending_a_send_span_should_still_record_a_client_operation.cs` (`SendSpanMetricsTests`) — **over-suppression guard** (per Scope Notes). A `CreateSpan(Send, …)` span must STILL record (`RecordClientOperationCallCount == 1`), proving the fix does not break the `default` arm that `create/deposit/send/clear/archive/scheduler/settle` rely on.

Both use a `SpyMessagingMeter` (`Enabled => true`, counts calls) and a disabled `IAmABrighterDbMeter` stub.

## Fix
`src/Paramore.Brighter/Observability/BrighterMetricsFromTracesProcessor.cs` — added `case "begin": break;` to the `switch (operation)` in `OnEnd`, immediately before the `default` arm. This short-circuits only the pump-lifetime begin span; the `default` arm is untouched, so all other operations continue to record. Chosen over the issue's preferred "drop the default arm" because the Confirm step proved that would regress `create/deposit/send/clear/archive/scheduler/settle`.

**Verification**: targeted tests green (2/2); full `Observability` suite green (77 passed, 2 pre-existing skips, 0 failed) on net9.0.
