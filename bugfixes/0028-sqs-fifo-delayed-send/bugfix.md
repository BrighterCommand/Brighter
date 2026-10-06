# Bugfix: Schedule delayed SQS FIFO sends

**Linked Issue**: #4390
**Status**: Verified

## Symptom

Sending a FIFO message with a positive delay takes the native SQS send path,
which sets per-message `DelaySeconds`. SQS rejects that parameter for FIFO queues.
The message should instead be scheduled and published when the delay elapses.
Both AWS SDK versions and both synchronous and asynchronous sends are affected.

## Suspected Location

- `src/Paramore.Brighter.MessagingGateway.AWSSQS/SqsMessageProducer.cs:139-170`
- `src/Paramore.Brighter.MessagingGateway.AWSSQS.V4/SqsMessageProducer.cs:139-170`
- `src/Paramore.Brighter.MessagingGateway.AWSSQS/SqsMessageSender.cs:77-90`
- `src/Paramore.Brighter.MessagingGateway.AWSSQS.V4/SqsMessageSender.cs:75-88`

## Root-Cause Hypothesis

The scheduler condition requires a Standard queue and a delay greater than
15 minutes, so FIFO messages never reach the scheduler. The issue suggests
allowing FIFO queues into that branch. This suggestion requires a positive-delay
guard because ordinary sends also use this method with a zero or null delay.

## Confirmed Root Cause

Independent code inspection confirms that the Standard-only condition excludes
FIFO queues from scheduling regardless of delay length. The native sender then
sets `DelaySeconds` for positive delays, which FIFO does not support.

The suggested fix is partial: the FIFO condition must also require a positive
delay. Otherwise, ordinary sends and scheduled republishing would also enter the
scheduler path. Preserve the existing behavior for zero, null, and negative delays.

## Evidence

- Before the fix, both producers restricted scheduling to Standard delays over
  15 minutes at line 146.
- Both producers route `SendAsync` through a zero delay at line 132 and `Send`
  through a null delay at line 178; line 144 normalizes null to zero.
- The native senders set positive per-message delays at v3 lines 77-88 and v4
  lines 75-86. Delays above 15 minutes are capped rather than scheduled.
- The [AWS SendMessage API documentation](https://docs.aws.amazon.com/AWSSimpleQueueService/latest/APIReference/API_SendMessage.html)
  states that FIFO queues do not accept per-message `DelaySeconds`.
- Before the fix, both `SqsFifoMessageGatewayProvider` implementations created
  producers without schedulers. The SNS FIFO providers provided the reference
  scheduler integration.
- `FifoMetadataProducer` stamps a new deduplication ID for each send. Scheduled
  republishing must send the already-stamped message directly through the SQS
  producer rather than stamp it again.
- FR-9 was deferred for both SQS FIFO configurations in `conformance-status.md`.

The diagnosis was independently checked by code trace and against the AWS contract.
Executable reproduction and verification are recorded below.

## Scope Notes

- Keep the AWS SDK v3 and v4 implementations in lockstep.
- Preserve immediate sends, null delays, and Standard queue delay behavior.
- Reuse the generated `ConformanceHarnessMessageScheduler` in the FIFO test
  providers; the issue's older `SnsHarnessMessageScheduler` name is obsolete.
- Preserve the original FIFO group and deduplication identifiers when republishing.
- Update the two FR-9 ledger entries and regenerate the corresponding tests;
  do not hand-edit generated files or claim passing results before verification.
- Issue #4356 covers unguarded scheduler casts in the same block. Its fallback and
  configuration-error behavior remains outside this fix. A delayed FIFO send
  requires a configured scheduler implementing the interface matching the call.
- Delayed requeue uses visibility changes and is outside this fix.

## Regression Test

Both `tests/Paramore.Brighter.AWS.Tests` and `tests/Paramore.Brighter.AWS.V4.Tests`
contain `MessagingGateway/When_sending_a_message_without_native_delay_support_should_schedule_the_original_message.cs`
and the supporting `TestDoubles/InMemoryDelayedMessageScheduler.cs`.

The 20 cases per project cover:

- FIFO delays of 1 millisecond, 5 seconds, 15 minutes, and 901 seconds through
  synchronous and asynchronous sends. Assertions check the original message,
  full delay, FIFO group and deduplication identifiers, matching scheduler
  interface, and asynchronous cancellation token.
- Zero, null, and negative FIFO delays, plus ordinary `Send` and `SendAsync`:
  no scheduling occurs and the message is received from the local broker.
- Standard queues: exactly 15 minutes is accepted natively without scheduling;
  901 seconds delegates to the scheduler.

The existing generated Reactor and Proactor FR-9 tests are enabled for both FIFO
configurations. They assert absence before the delay and receipt afterward,
checking the message headers, including the FIFO group, and body.

### Red reproduction

- Before production changes, both projects reported 8 failed and 12 passed in
  `SqsDelayedSendTests` on .NET 10. FIFO sends tried to validate a nonexistent
  queue instead of delegating to the scheduler; the Standard and immediate-send
  cases passed.
- Before production changes, both generated FIFO delayed-send variants failed
  in each project: expected `MT_NONE` before the delay, received `MT_EVENT`.
  Floci accepts the FIFO delay parameter but delivers immediately, unlike real
  AWS's documented rejection. This reproduces the delay-contract violation,
  not the exact AWS exception.

### Verification

Infrastructure: Floci 1.5.19, with `AWS_SERVICE_URL=http://localhost:4566` and the
test harness's local credentials. No real AWS resources were used.

- Focused .NET 10 runs: 22 passed, no skips, in each AWS project.
- Release suites, using CI's `LiveAWS!=true` filter: 267 passed, 8 existing skips
  in each project on each of .NET 9 and .NET 10. The skips are the existing
  deferred requeue-budget tests, not delayed-send tests.
- Generator and conformance-ledger audits: 292 passed on .NET 10.
- Both gateways build in Release for .NET Standard 2.0, .NET 8, .NET 9, and
  .NET 10, with no warnings or errors in the final build runs.
- `git diff --check` passed.

The full-suite command, repeated for both AWS test projects, was:

```bash
AWS_SERVICE_URL=http://localhost:4566 dotnet test tests/Paramore.Brighter.AWS.Tests/Paramore.Brighter.AWS.Tests.csproj -c Release --filter 'LiveAWS!=true'
```

## Fix

Both `SqsMessageProducer` implementations now schedule positive FIFO delays, while
preserving native Standard delays up to 15 minutes and all nonpositive-delay
behavior. The existing scheduler casts remain unchanged.

Both FIFO test providers wire the generated harness scheduler and dispose it
during cleanup. Scheduled republishing uses a direct producer with the original
message and validates the existing queue rather than recreating infrastructure.

The two FR-9 ledger cells and their explanatory text are updated. The four
delayed-send test files were regenerated from the ledger, not hand-edited.
