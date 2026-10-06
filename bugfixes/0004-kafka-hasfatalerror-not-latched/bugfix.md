# Bugfix: Kafka `_hasFatalError` overwritten (not latched) on each error callback

**Linked Issue**: #4227
**Status**: Verified

## Symptom
The Kafka consumer maintains a `_hasFatalError` flag that `Receive()` checks in order to throw a `ChannelFailureException` when librdkafka has reported an unrecoverable (fatal) error. Because the flag is *assigned* the value of `error.IsFatal` on every error-callback invocation rather than *latched*, a fatal error can be silently cleared by a subsequent non-fatal error before the consume-loop thread ever observes it.

- Observed: after a fatal error followed by a non-fatal error, `_hasFatalError` is `false`; `Receive()` does not throw and the consumer keeps running against a connection librdkafka has declared dead.
- Expected: once a fatal error is observed, `_hasFatalError` stays `true` until the consumer is disposed/recreated, so `Receive()` reliably throws `ChannelFailureException` regardless of later non-fatal errors.

Reproduction (conceptual, threads differ): (1) librdkafka error callback fires with `IsFatal == true` → flag set `true`; (2) another callback fires immediately with `IsFatal == false` → flag reset to `false`; (3) the consume thread calls `Receive()`, reads `false`, and does not throw — the fatal condition is masked.

## Suspected Location
File: `src/Paramore.Brighter.MessagingGateway.Kafka/KafkaMessageConsumer.cs`

Verified current line numbers (issue's `~L` references were close but confirm as follows):

- Field declaration — `KafkaMessageConsumer.cs:64`: `private bool _hasFatalError;`
  - The field is **NOT** marked `volatile` (contrast with the surrounding `readonly` fields at lines 58-68). Relevant to the thread-visibility angle noted in the issue: the error callback and the `Receive()` caller are different threads, so there is currently no memory barrier guaranteeing the write is visible to the reader.
- Error handler lambda — `KafkaMessageConsumer.cs:253-261`, with the offending assignment at `KafkaMessageConsumer.cs:255`: `_hasFatalError = error.IsFatal;` (unconditional overwrite). This is the **only** site that writes the field (confirmed by grep).
- Consume-loop check — `KafkaMessageConsumer.cs:494-495` inside `Receive(TimeSpan?)` (method starts at `KafkaMessageConsumer.cs:492`): `if (_hasFatalError) throw new ChannelFailureException(...)`. This is the **only** read site.

Async variant: the class is `partial class KafkaMessageConsumer : KafkaMessagingGateway, IAmAMessageConsumerSync, IAmAMessageConsumerAsync` (`KafkaMessageConsumer.cs:47`). There is a single consumer type (no separate async consumer class in this gateway). `ReceiveAsync` at `KafkaMessageConsumer.cs:560` delegates to the sync `Receive` via `Task.Run(() => Receive(timeOut))` (`KafkaMessageConsumer.cs:565-569`), so it inherits the same (single) check and is affected transitively — there is no duplicated flag-check logic to fix separately. The producer has its own unrelated `SetErrorHandler` at `KafkaMessageProducer.cs:176` and does not use `_hasFatalError`.

## Root-Cause Hypothesis
The flag is intended as a one-way "consumer is dead" latch but is implemented as a plain assignment. At `KafkaMessageConsumer.cs:255`, `_hasFatalError = error.IsFatal;` resets the flag to `false` whenever a non-fatal error arrives after a fatal one. Since librdkafka commonly emits errors in bursts and the callback runs on a different thread from `Receive()`, a fatal error can be overwritten before the consume loop reads it, so the `ChannelFailureException` at `KafkaMessageConsumer.cs:495` is never thrown.

A secondary contributing factor is thread visibility: `_hasFatalError` (`KafkaMessageConsumer.cs:64`) is a non-`volatile` `bool` written on the callback thread and read on the consume thread with no synchronization, so even a correct latch could be delayed/invisible to the reader.

Issue's suggested direction (restated): latch instead of overwrite, e.g. `if (error.IsFatal) _hasFatalError = true;`, and consider thread-visibility of the flag across the callback and consume threads (e.g. mark it `volatile`). **UNVERIFIED — to be proven or refuted in /bugfix:confirm.**

## Confirmed Root Cause
**CONFIRMED.** The flag is meant to be a one-way "consumer is dead" latch but is written as a plain assignment on every error-callback invocation. `_hasFatalError = error.IsFatal;` (`KafkaMessageConsumer.cs:255`) makes the *last* error in a burst win: fatal (`true`) followed by non-fatal (`false`) leaves the flag `false`, so the guard in `Receive()` (`KafkaMessageConsumer.cs:494`) never throws `ChannelFailureException` and the dead consumer keeps running. This is a real behavioral defect **independent of threading** — it reproduces single-threaded.

Correction to triage: there are **TWO** read sites, not one. Besides the guard at `:494`, the log selector at `:257` (`if (_hasFatalError)`) also reads the flag. This matters for the fix (see Scope Notes / Suggested-Fix Assessment).

## Evidence
- [x] **Code-trace** (verified against current source):
  - Field decl `KafkaMessageConsumer.cs:64` — `private bool _hasFatalError;` (NOT volatile).
  - Write site `KafkaMessageConsumer.cs:255` — `_hasFatalError = error.IsFatal;` inside the `SetErrorHandler((_, error) => {...})` lambda registered on the `ConsumerBuilder` at `:253`. **Only** write site (repo-wide grep of `_hasFatalError` returns only lines 64/255/257/494).
  - Read sites: `KafkaMessageConsumer.cs:257` (log-branch selector) and `KafkaMessageConsumer.cs:494` (`if (_hasFatalError) throw new ChannelFailureException(...)`).
  - Masking trace: two consecutive callback invocations, `error.IsFatal==true` then `error.IsFatal==false`, drive `:255` to set `true` then `false`; `Receive()` at `:494` reads `false` → no throw. Latching (`if (error.IsFatal) _hasFatalError = true;`) would leave it `true`. **Proven.**
  - `ReceiveAsync` (`:560-582`) wraps sync `Receive(timeOut)` in `Task.Run` (`:565`, `:569`), so it inherits the same guard and bug.
  - No dispose/reset/reconnect path touches the flag (`Dispose`/`Close` at `:402`, `:1157` do not reference it).
- Executable red repro not written here (the handler is an un-exposed closure — see Scope Notes on test reachability). Live-broker reproduction is impractical; the code-trace is the accepted evidence for this infra-bound path.

## Scope Notes
- **Suggested-fix assessment: PARTIAL.** `if (error.IsFatal) _hasFatalError = true;` correctly latches the guard read at `:494` and matches the issue's "stays true until disposed/recreated" semantics. BUT it silently breaks the logging branch: the log selector at `:257` reads `_hasFatalError`, not `error.IsFatal`. After latching, `_hasFatalError` stays `true`, so every *subsequent non-fatal* error would be logged via `Log.FatalError` (`:258`) and the `Log.NonFatalError` else-branch (`:260`) becomes dead code. **The fix must decouple the log decision from the latch** — drive the latch off `error.IsFatal` AND the log branch off `error.IsFatal` (not off `_hasFatalError`). e.g.:
  ```csharp
  if (error.IsFatal) _hasFatalError = true;
  if (error.IsFatal) Log.FatalError(...); else Log.NonFatalError(...);
  ```
- **`volatile`: nice-to-have, NOT the root cause (triage overstated it).** confluent-kafka-dotnet dispatches the error handler as a side effect of `_consumer.Consume(...)` (`:505`) on the consuming thread — the same thread that evaluates `:494` on the next `Receive`. Under `ReceiveAsync` each `Task.Run` runs check+consume+callback on one pooled thread with a memory barrier at the `await`/`TaskCompletionSource` boundary. The bug reproduces single-threaded, so correctness does not depend on `volatile`; adding it is defensible hardening but must not be sold as fixing the reported symptom.
- **Cross-backend parity (same defect): `KafkaMessageProducer.cs:178`** — `_hasFatalProducerError = error.IsFatal;` is the identical non-latched pattern, with the same log-branch coupling at `:180` (`if (_hasFatalProducerError)`), non-volatile field decl at `:60`, and guard reads at `:253` and `:329`. If the intent is a one-way latch, the producer has the same masking bug. **DECISION (user-approved): fix BOTH consumer and producer for parity.** The regression test(s) and fix must cover the producer latch (`_hasFatalProducerError`, field `:60`, write `:178`, log branch `:180`, guards `:253`/`:329`) as well as the consumer.
- **Reset-on-recovery: none exists.** No reconnect/re-subscribe path in the consumer; on fatal error the message pump tears down and recreates the channel/consumer. A permanent latch is consistent with the issue's semantics and breaks no recovery path.
- **Test reachability:** the error handler is an anonymous closure captured into `ConsumerBuilder.SetErrorHandler` (`:253`) — not exposed, injectable, or invocable externally. No existing Kafka test references the consumer's `_hasFatalError`/`SetErrorHandler`/`IsFatal`. `/bugfix:test` will likely need a small refactor extracting the handler body into a testable instance method (call it twice: fatal then non-fatal; assert the latch holds and `Receive()` throws), or a code-trace-backed argument rather than a live red repro.

## Regression Test
Two RED tests (parity — consumer + producer), one `[Fact]` per file:

- `tests/Paramore.Brighter.Kafka.Tests/MessagingGateway/When_a_fatal_consumer_error_is_followed_by_a_non_fatal_should_still_throw.cs`
  — invokes `HandleError(fatal)` then `HandleError(non-fatal)` and asserts `Receive(TimeSpan.Zero)` throws `ChannelFailureException`.
- `tests/Paramore.Brighter.Kafka.Tests/MessagingGateway/When_a_fatal_producer_error_is_followed_by_a_non_fatal_should_still_throw.cs`
  — invokes `HandleError(fatal)` then `HandleError(non-fatal)` and asserts `Send(message)` throws `ChannelFailureException`.

**Enabling structural change (Tidy First — behaviour-preserving):** the error-handler closures were extracted verbatim into `public void HandleError(Error error)` on `KafkaMessageConsumer` and `KafkaMessageProducer`, wired via `.SetErrorHandler((_, error) => HandleError(error))`. (`InternalsVisibleTo` is banned in this codebase, so the seam is a `public` method, consistent with the gateways' existing public operational surface.) This makes the bug reachable from a unit test without a live broker (`Receive(TimeSpan.Zero)` short-circuits on the latch before `Consume`; producer uses `MakeChannels=Assume` so `Init()`/`EnsureTopic()` do not contact a broker). No behavioural change was made — the assignment bug is intact.

**RED confirmed** (both fail for the right reason — `Assert.IsType<ChannelFailureException>` got `null`, i.e. the non-fatal error cleared the latch so nothing threw):
```
Failed: 2, Passed: 0 — Paramore.Brighter.Kafka.Tests (net9.0)
  Assert.IsType() Failure: Value is null; Expected: typeof(ChannelFailureException); Actual: null
```

**Log-decoupling guard tests (GREEN now, RED against a naive fix):** using the existing `Serilog.Sinks.TestCorrelator` harness (already wired via `tests/Paramore.Brighter.Kafka.Tests/Initializer.cs`):

- `tests/Paramore.Brighter.Kafka.Tests/MessagingGateway/When_a_non_fatal_consumer_error_follows_a_fatal_error_should_log_as_non_fatal.cs`
- `tests/Paramore.Brighter.Kafka.Tests/MessagingGateway/When_a_non_fatal_producer_error_follows_a_fatal_error_should_log_as_non_fatal.cs`

Each fires `HandleError(fatal)` then `HandleError(non-fatal)` and asserts the non-fatal error is logged at `Warning` (`NonFatal*Error`), not `Error` (`Fatal*Error`). These are **green against current code** (today each error logs per its own `IsFatal`), but they go **RED against the naive latch fix** (`if (error.IsFatal) _hasFatalError = true;` while the log branch still reads `_hasFatalError`), locking in the requirement that the fix decouple the log decision from the latch (drive both off `error.IsFatal`).

**Current state:** `Failed: 2, Passed: 2` — the two latch tests are RED (the bug), the two log guards are GREEN (correct today, protect the fix).

## Fix
Latched the flag and decoupled the log decision from it, in both `HandleError` methods:

- `src/Paramore.Brighter.MessagingGateway.Kafka/KafkaMessageConsumer.cs` — `HandleError(Error)`: `if (error.IsFatal) _hasFatalError = true;` (latch; never cleared by a later non-fatal error), and the log branch now switches on `error.IsFatal` rather than `_hasFatalError`.
- `src/Paramore.Brighter.MessagingGateway.Kafka/KafkaMessageProducer.cs` — `HandleError(Error)`: same change for `_hasFatalProducerError`.

**Deliberately NOT changed (scope discipline):** the fields remain non-`volatile`. The confirmed diagnosis established the defect is a single-threaded logic error and `volatile` is optional hardening, not the cause; adding it would exceed the proven scope. Can be revisited separately if desired.

**Result:** all four tests GREEN (`Failed: 0, Passed: 4`, 236 ms). The two latch tests now throw `ChannelFailureException` after fatal→non-fatal; the two log guards confirm the post-fatal non-fatal error still logs at `Warning`, proving the decoupling holds.

## Verification
- Regression tests: **4/4 pass**.
- Broker-free unit tests in `Paramore.Brighter.Kafka.Tests`: **20/20 pass** (238 ms) — no regressions in the touched project.
- **Full suite against a live broker** (Kafka brought up via `docker-compose-kafka.yaml` on Podman): **85 passed, 9 failed** (net9.0, 5m07s). The 4 regression tests pass against the live broker (229 ms).
- **The 9 failures are pre-existing and unrelated to this fix.** They are all produce→consume roundtrip tests failing on a single assertion — `KafkaMessageAssertion.cs:50`, `expected.Header.TimeStamp.ToString("yyyy-MM-ddTHH:mm:ss")` vs `actual` — off by exactly one hour (`16:45` UTC vs `17:45` BST). This is a timezone sensitivity in the message-timestamp round-trip that only manifests when the host's local time ≠ UTC (BST/+1 in July); it would pass on a UTC CI host. It is not a `ChannelFailureException` and does not touch `HandleError`, so this fix cannot cause it. Out of scope for #4227 (candidate for a separate issue).
