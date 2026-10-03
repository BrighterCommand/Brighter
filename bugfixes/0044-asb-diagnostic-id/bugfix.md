# Bugfix: Continue Azure Service Bus traces from Diagnostic-Id

**Linked Issue**: [#4477](https://github.com/BrighterCommand/Brighter/issues/4477)
**Pull Request**: [#4495](https://github.com/BrighterCommand/Brighter/pull/4495)
**Status**: Fixed — regression tests pass; full verification awaits Azure credentials

## Symptom

Messages from other Azure Service Bus producers can carry `Diagnostic-Id` without `cloudevents:traceparent`.
Brighter maps those messages to an empty trace parent, losing the producer's trace context.
The message creator also logs a missing-trace-parent warning.

## Suspected Location

- `src/Paramore.Brighter.MessagingGateway.AzureServiceBus/AzureServiceBusMessageCreator.cs:350`: `GetTraceParent` selects the received trace parent.
- `src/Paramore.Brighter.MessagingGateway.AzureServiceBus/ASBConstants.cs:28`: the existing key is `cloudevents:traceparent`.
- `src/Paramore.Brighter/Observability/BrighterTracer.cs:164`: tracing reads `MessageHeader.TraceParent`.

## Root-Cause Hypothesis

The message creator only reads the CloudEvents property, so a valid `Diagnostic-Id` never reaches the message header.
A fallback for an absent CloudEvents property should preserve that context without changing CloudEvents precedence.

## Confirmed Root Cause

Before the fix, `GetTraceParent` returned an empty value immediately when `cloudevents:traceparent` was absent.
It did not inspect `Diagnostic-Id`.
`MapToBrighterMessage` places that result in `MessageHeader.TraceParent`, which supplies the downstream tracing context.

## Evidence

- `AzureServiceBusMessageCreator.cs:100` obtains the trace parent; line 123 supplies it to the message header.
- The pre-fix implementation is available at base commit `6abf8c7a4`.
- Before implementation, all five valid fallback cases failed on .NET 9 and .NET 10.
  Each failure expected the supplied W3C parent but received an empty string.
- The twelve precedence and invalid-input cases initially passed and were subsequently checked with named mutations.
- After the fix, all seventeen cases pass on both frameworks through the public consumer APIs.

## Scope Notes

- Use `Diagnostic-Id` only when the CloudEvents trace-parent key is absent.
- Accept only string values that pass `ActivityContext.TryParse`.
- Preserve existing CloudEvents values, including empty or malformed strings.
- Preserve the received application properties in the header bag.
- Do not add a trace-state fallback or change missing-trace-state logging.
- No producer, dependency, public API, or other transport changes are required.

## Regression Test

The following files are under `tests/Paramore.Brighter.AzureServiceBus.Tests/MessagingGateway/`:

| File | Cases | Behavior |
| --- | --- | --- |
| `When_receiving_a_message_should_use_a_valid_diagnostic_id_when_cloud_events_traceparent_is_absent.cs` | 5 | Sync/async queue and topic consumers; unsampled trace context |
| `When_receiving_a_message_should_prefer_its_cloud_events_traceparent.cs` | 4 | CloudEvents precedence, including empty and malformed existing values |
| `When_receiving_an_invalid_diagnostic_id_should_leave_the_traceparent_empty.cs` | 8 | Null, empty, malformed, hierarchical, zero-ID, invalid-flag, and non-string values |

The tests use SDK messages and the existing in-memory Service Bus client through the real consumer factory.
No private APIs or new test doubles are used.

Named mutations were applied only to production code:

1. **IgnoreCloudEventsParent**: replace the CloudEvents lookup key with an unrecognized key. All four precedence cases failed at their trace-parent assertions.
2. **InventParentForInvalidDiagnosticId**: generate a fresh W3C parent when the diagnostic ID cannot be accepted. All eight invalid-ID cases failed at their empty-parent assertions.

Both checks ran on .NET 9 and .NET 10. Both mutations were reverted, and the unchanged assertions passed again.

## Fix

`AzureServiceBusMessageCreator.GetTraceParent` now checks `Diagnostic-Id` before returning an empty parent when the CloudEvents property is absent.
A valid fallback returns before the missing-trace-parent warning.
The mapping method's XML documentation describes the fallback.

## Verification

The final runs used Debug configuration against upstream `6abf8c7a4` plus the fix.

| Check | .NET 9 | .NET 10 |
| --- | --- | --- |
| Regression cases | 17 passed | 17 passed |
| Full Azure Service Bus suite | 402 passed, 6 skipped, 38 failed | 402 passed, 6 skipped, 38 failed |

Every full-suite failure reports `ASB ConnectionString or Namespace not set not set`.
Live Azure verification remains outstanding; the full suite is not green.
The gateway builds for `netstandard2.0`, `net8.0`, `net9.0`, and `net10.0` with zero warnings and errors.

Reproduce the focused tests:

```bash
dotnet test tests/Paramore.Brighter.AzureServiceBus.Tests/Paramore.Brighter.AzureServiceBus.Tests.csproj --filter 'FullyQualifiedName~AzureServiceBusDiagnosticIdFallbackTests|FullyQualifiedName~AzureServiceBusCloudEventsTraceParentPrecedenceTests|FullyQualifiedName~AzureServiceBusInvalidDiagnosticIdTests'
```

Run the full suite after configuring `BrighterTestsASBConnectionString` or `BrighterTestsASBNameSpace`:

```bash
dotnet test tests/Paramore.Brighter.AzureServiceBus.Tests/Paramore.Brighter.AzureServiceBus.Tests.csproj
```

## Workflow Record

This record was added after implementation to document the existing diagnosis, approvals, and test evidence.
The scoped fix and regression test were approved through the standalone test-first path before implementation.
A separate `/bugfix:confirm` step was not run.
The approval marker records the approval already given; it does not assert that the missing workflow step occurred.
