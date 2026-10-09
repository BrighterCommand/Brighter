# Bugfix: Kafka producer and consumer disposal repeats teardown

**Linked Issue**: [#4539](https://github.com/BrighterCommand/Brighter/issues/4539)
**Status**: Verified

## Symptom

A second producer disposal flushes an already destroyed Confluent handle and throws
`ObjectDisposedException`. Consumer disposal repeats the same operation on any created requeue,
dead-letter or invalid-message producer. Mixing synchronous and asynchronous disposal has the same effect.
An error in one cleanup step also prevents later resources from being released.

## Confirmed Root Cause

Neither Kafka class records that disposal has started. The producer retains its native handle after teardown.
The consumer has separate synchronous and asynchronous cleanup sequences; its Close guard protects offset
committing, but not the rest of disposal. Both sequences stop at the first exception.

## Evidence

On upstream master `2890f8610`, all 20 repeated-disposal cases failed on both .NET 9 and .NET 10.
The failures were assertion failures caused by `ObjectDisposedException` from `KafkaMessageProducer.Flush`.
The consumer cases created their owned producers through real receive, requeue and rejection operations
against Kafka 4.0.2.

## Fix

- Each class claims disposal once with `Interlocked.Exchange`. Sync and async entry points share that state.
- The consumer uses one teardown path, retaining asynchronous timer and producer disposal when requested.
- An internal cleanup helper attempts every action and records its errors. A single error is rethrown with its
  original stack; multiple errors are reported together in an `AggregateException` after cleanup.
- Producer flush, confirmation-callback draining and native handle disposal remain ordered.
- Consumer timer shutdown, offset committing, native handle disposal, semaphore disposal and created-producer
  disposal remain ordered. Cleanup does not initialize unused lazy producers.

The guard is set before teardown, including when teardown fails. Later disposal calls are no-ops.
The change does not define concurrent use of messaging operations during disposal or reuse after disposal.

## Regression Tests

- `KafkaProducerRepeatedDisposalTests`: four combinations of sync and async disposal, each repeated three times.
- `KafkaConsumerRepeatedDisposalTests`: the same four combinations with requeue, dead-letter, invalid-message,
  or all three producers created; 16 cases.
- `KafkaConsumerCleanupFailureTests`: four combinations with a faulting timer. A separate broker client verifies
  that pending offsets were committed despite the error. The original error is preserved, the timer is disposed
  once, and subsequent disposal succeeds.

The faulting timer uses the public TimeProvider input and wraps a real timer. Tests exercise public Kafka APIs
and real broker I/O; they do not access internal implementation details. Scheduled sweeping is disabled in the
disposal fixture so offsets remain pending until shutdown, even when test setup takes longer than 30 seconds.

### Characterisation mutations

- `StopOnCleanupFailure`: rethrow immediately from the cleanup helper's catch blocks. Pending offsets remain
  uncommitted, so the broker-side assertion must fail.
- `RepeatConsumerTeardown`: remove the consumer's shared disposal guard. Repeated disposal encounters the timer
  error again, so the assertion that later disposal is harmless must fail.

Both mutations are temporary and are restored before the final regression run.

## Validation

- All 24 new regression cases pass on both .NET 9 and .NET 10 against Kafka 4.0.2.
- Each named mutation fails all four cleanup-error cases on .NET 9 for the intended assertion:
  uncommitted offsets for `StopOnCleanupFailure`, and a repeated disposal error for `RepeatConsumerTeardown`.
  The original production sources were restored before the full-suite run.
- The production project builds for netstandard2.0, net8.0, net9.0 and net10.0 with zero warnings and errors.
- New C# files pass the whitespace formatter. The two existing source files add no formatting diagnostics
  compared with master. `git diff --check` passes.
- The full Kafka suite has 262 passes and two failures on each of .NET 9 and .NET 10. Both failures are existing
  culture-dependent header-conversion assertions expecting `3,56` instead of the invariant `3.56`. The same two tests fail on an
  untouched archive of master `2890f8610` on both .NET 9 and .NET 10. No new regressions were observed.
