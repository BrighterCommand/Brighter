# Bugfix: Guard SQS delayed-send scheduler selection

**Linked Issue**: #4356
**Status**: Verified

## Symptom

A scheduled SQS send fails with `NullReferenceException` when no scheduler is
configured, or `InvalidCastException` when the scheduler supports only the other
sync/async interface. Both AWS SDK versions are affected. Standard delays over
15 minutes and positive FIFO delays use this path.

## Suspected Location

- `src/Paramore.Brighter.MessagingGateway.AWSSQS/SqsMessageProducer.cs:139-158`
- `src/Paramore.Brighter.MessagingGateway.AWSSQS.V4/SqsMessageProducer.cs:139-158`

## Root-Cause Hypothesis

The triage hypothesis was that the delayed-send branch casts the nullable scheduler directly to the
interface matching the call, without checking for absence or accepting the other
supported interface. SNS already uses a prefer-then-fallback policy and reports a
configuration error when neither interface is available.

## Confirmed Root Cause

Independent code tracing confirms the hypothesis. `Scheduler` is nullable and
implements only a marker contract. Casting null leaves a null reference for the
subsequent invocation; casting an opposite-interface-only or marker-only object
throws immediately. Neither path reports the missing usable scheduler as a
configuration error. The diagnosis and scope are approved.

## Evidence

The following line references describe the unchanged base commit `4fecf11f0`.

- Both SQS producers forward async sends with `useAsyncScheduler = true` at
  lines 135-136 and sync sends with `false` through `BrighterAsyncContext.Run`
  at lines 181-182.
- The scheduling condition at lines 144-147 covers positive FIFO delays and
  Standard delays greater than 15 minutes.
- The async cast/invocation at lines 151-152 and sync cast/invocation at lines
  156-157 assume an interface that the property at line 56 does not guarantee.
- `src/Paramore.Brighter/IAmAMessageScheduler.cs:6` is a marker; the sync and
  async interfaces extend it independently.
- AWS queue confirmation and client creation occur later, at lines 164 and 166.
  The failure can therefore be tested without contacting AWS.
- Both SNS producers already prefer the matching scheduler interface, accept
  the other, and otherwise throw `ConfigurationException` at lines 173-208.
- The approved regression tests reproduced the defect before the fix: 12 failed
  and 10 passed per SDK version per test framework. Missing schedulers produced
  `NullReferenceException`; unsupported and opposite-interface-only schedulers
  produced `InvalidCastException`.

## Scope Notes

Keep the native-versus-scheduled routing introduced for FIFO delays unchanged.
Keep AWS SDK V3 and V4 behavior aligned. Issue #4312 is a separate follow-up and
must not be included in this branch.

Cover missing and marker-only schedulers, both opposite-interface fallbacks,
and matching-interface preference. Preserve the message, delay, and async
cancellation token. Propagate scheduler exceptions without retrying through the
other interface. A sync scheduler has no cancellation-token parameter; this fix
does not change that contract. Sync-to-async fallback already runs within the
existing async context and must not introduce a second blocking wrapper.

## Regression Test

The proposed tests are in
`tests/Paramore.Brighter.AWS.Tests/MessagingGateway/When_sending_with_an_available_sqs_scheduler_should_schedule_the_original_message.cs`
and its `AWS.V4.Tests` mirror. Each has 22 theory cases covering:

- Matching and opposite-interface-only scheduler selection for both send APIs.
- Missing and marker-only schedulers producing actionable configuration errors.
- Matching-interface preference when both interfaces are implemented.
- Original message, delay, and async cancellation-token preservation.
- Cancellation propagation from an asynchronous scheduler.

Each matrix covers positive FIFO delays and Standard delays over 15 minutes.
The async-only test scheduler yields before recording delivery, exercising actual
asynchronous completion. Existing `SqsDelayedSendTests` continue to cover native
delay boundaries and FIFO zero/null-delay sends.

The regression tests were reviewed and approved before execution and implementation.

### Verification

- Before the fix: 12 failed, 10 passed, no skips per SDK version on both .NET 9
  and .NET 10. Failures match the confirmed null-reference and invalid-cast paths.
- After the fix: all 22 targeted cases passed per SDK version on both frameworks.
- Full emulator-compatible suite (`LiveAWS!=true`): 289 passed, 8 existing skips,
  no failures per SDK version on both frameworks, in Release configuration.
- Both gateway packages built in Release for .NET 8, 9, and 10 with zero warnings
  and zero errors.
- `git diff --check` passed.

Integration verification used Floci 1.5.19 at `http://localhost:4566`, matching the
repository's emulator CI configuration. No live AWS tests were run and no real
AWS resources were touched. The eight skips are existing delivery-budget
conformance deferrals, not tests disabled by this change.

## Fix

Both `SqsMessageProducer` implementations now prefer the scheduler interface
matching the send API, accept the other supported interface, and otherwise throw
`ConfigurationException` naming `MessageSchedulerFactory`. XML exception
documentation describes the configuration requirement on both delayed-send APIs.
Native delay routing remains unchanged. No scheduler exception is intercepted or
retried through the alternate interface.
