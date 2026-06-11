# Review: design — 0034-failed-delivery-context

**Date**: 2026-06-11
**Threshold**: 60
**Verdict**: NEEDS WORK

2 findings at or above threshold 60. Address these before approving.

> Round 6. Focus areas: the InMemory confirmation pump (Option B concurrency + two-stage drain) and the freshly-tightened Kafka data-path section. The R5 findings were verified as folded in: the pump now pins `UseAsyncPublishConfirmation` / `Func<Message,bool>? PublishFailurePredicate` / single worker / FIFO / unbounded channel (R5 #1); the `SendWithDelay`/scheduler interaction has its own bullet (R5 #2); the deferred-bus-visibility negative consequence is listed (R5 #3); the "all four Send methods end in CancellationToken" claim is scoped to the two async methods (R5 #4); C-7 cites the `EndSpans` lines (R5 #5). Load-bearing line/type references re-verified this round still match source (`OnMessagePublished` = `Action<bool,string>` at `ISupportPublishConfirmation.cs:42`; `PublishResults(PersistenceStatus, Headers)` at `KafkaMessageProducer.cs:364`; `Task.Run` raises at `:373/:381`; synthetic `DeliveryResult` never sets `.Topic` at `KafkaMessagePublisher.cs:56-61`; `CreateProducerSpan` sets `Activity.Current` at `BrighterTracer.cs:700`; `TripTopic(RoutingKey?)` + guard at `:1168/:1170`; RMQ `_pendingConfirmations` value type + raise sites). The two new R6 areas are where the problems are.

## Findings

### 1. InMemory two-stage drain: the prescribed `Interlocked`-count + `TaskCompletionSource` mechanism deadlocks on the quiescent shutdown path and under-specifies the increment ordering (Score: 64)

The drain is specified as: complete the writer → await the worker → await in-flight raises "tracked with an `Interlocked` outstanding-count plus a `TaskCompletionSource` that is **signalled when the count returns to zero after the writer has completed**." Two distinct correctness gaps:

(a) **Quiescent-path deadlock.** The TCS-signal is described purely as a *decrement-side* trigger ("count **returns to** zero after the writer has completed"). In the common shutdown case there are **no** in-flight raises when `Dispose` runs (the worker already drained and all raise tasks already decremented to zero *before* the writer was completed). The decrements that drove the count to zero fired earlier and were correctly suppressed because the writer was not yet completed; after `writer.Complete()` nothing ever decrements again, so the "returns to zero" event never re-fires and `await TCS` blocks forever. The disposer must itself re-evaluate the predicate (`count == 0 && writerCompleted`) after completing the writer and awaiting the worker, and short-circuit / pre-signal the TCS — the ADR's stated trigger does not cover the already-zero case.

(b) **Increment-vs-spawn ordering is not pinned.** The drain is only correct if the `Interlocked.Increment` happens in the **worker**, *before* `Task.Run` spawns the raise (so "await the worker" guarantees every raise has been counted). If the increment is instead placed inside the `Task.Run` body, `await worker` can complete with a spawned-but-not-yet-incremented raise outstanding; the disposer observes count == 0 and returns while a confirmation is still pending — a **lost confirmation on shutdown**, the exact failure the two-stage drain exists to prevent. The ADR says the worker "finishes ... spawning the raise tasks" but never states the increment precedes the spawn.

**Evidence**: ADR "Confirmation-capable in-memory producer" → two-stage drain bullet: "await the **worker** ... then await the **in-flight raise tasks** ... tracked with an `Interlocked` outstanding-count plus a `TaskCompletionSource` that is signalled **when the count returns to zero after the writer has completed** (bounded wait)." No disposer-side re-check of the already-zero predicate; no statement that the increment occurs in the worker before `Task.Run`.

**Recommendation**: Specify that (i) the outstanding-count is incremented in the worker loop **immediately before** each `Task.Run` spawn (decrement inside the raise's `finally`), and (ii) after `writer.Complete()` + `await worker`, the disposer **re-checks** `count == 0` and completes the TCS directly if already zero, otherwise awaits it; the raise's decrement completes the TCS only when it observes both zero and writer-completed. (Equivalently, drop the TCS and collect raise `Task` handles into a thread-safe bag and `await Task.WhenAll` after the worker — simpler and immune to both races.)

---

### 2. Kafka data-path claim "the closure already closes over the per-message `message` local" is factually false (Score: 63)

The new Kafka section asserts, in the present tense, that the existing per-produce delivery-report closure *already* captures `message`, and uses this as the load-bearing rationale for why Kafka — unlike RMQ — needs no correlation map ("the closure *is* the carrier"). The actual closure at `KafkaMessageProducer.cs:260` (sync) and `:333` (async) is `report => PublishResults(report.Status, report.Headers)`. It references only `report` and the instance method `PublishResults` (capturing `this`); it does **not** reference `message`. In C#, an outer local is captured into the closure's display class **only if the lambda body references it** — `message` is in lexical scope but is *not* captured, so it is **not** "directly in scope at confirmation time." The closure object holds `this`, nothing else.

The design *conclusion* (Kafka can carry topic/id/context by capturing them in the closure) is correct, but only **after** the closure body is rewritten to reference `message` (or captured topic/context locals). As written, an implementer is told the data is "already" reachable from the closure, which understates the change: the closure body must change, not just `PublishResults`'s signature.

**Evidence**: `KafkaMessageProducer.cs:260` `_publisher.PublishMessage(message, report => PublishResults(report.Status, report.Headers));` and `:333` (async) — neither lambda references `message`. ADR "Kafka FR-8 + FR-2 (data path)": "That closure is constructed fresh on each send and **already closes over the per-message `message` local**, so the wire topic (`message.Header.Topic`) and the message id (`message.Id`) are **directly in scope at confirmation time**".

**Recommendation**: Reword to present the capture as something the implementation *introduces*: "the closure is constructed fresh on each send within `SendWithDelay`/`SendWithDelayAsync`, so it **can** be modified to close over `message` (and the captured `ActivityContext`), making the wire topic and id reachable at confirmation time without a correlation map." Drop "already." Reconcile the "inside `Send`/`SendAsync`" phrasing with the real closure site (`SendWithDelay`/`SendWithDelayAsync`).

---

### 3. Lazy worker-start is not guarded against concurrent first-enqueuers, despite asserting `SingleReader = true` (Score: 57)

The pump's worker is "lazily started on first enqueue," and the channel is `Channel.CreateUnbounded<T>(new() { SingleReader = true, SingleWriter = false })`. With `SingleWriter = false`, multiple `Send`/`SendAsync` callers can enqueue concurrently; the *first* two concurrent enqueuers can both observe "no worker yet" and each start a worker, yielding two concurrent `ReadAllAsync` readers. `SingleReader = true` is a **caller-asserted optimization contract** that the `Channel` does not enforce — two concurrent readers on a SingleReader channel is undefined behaviour (it can drop the FIFO/deterministic-produce-order guarantee the ADR leans on, and corrupt the drain bookkeeping). The ADR states the worker is single and lazily started but never specifies the start guard. R5 #1 secured "single worker + FIFO" but not the *start* race.

**Evidence**: ADR pump section: "A **single** long-running worker `Task` (**lazily started on first enqueue** ...)"; channel created `SingleReader = true, SingleWriter = false`. No start-synchronization primitive named.

**Recommendation**: State the single-start mechanism explicitly (e.g. "the worker `Task` is started exactly once via `Interlocked.CompareExchange` on a backing field, so concurrent first-enqueuers cannot start two readers and the `SingleReader=true` contract is honoured").

---

### 4. The `Task.Run`-per-raise AC-10 concurrency claim is probabilistic and may not actually overlap in a test (Score: 46)

The ADR claims dispatching each raise via `Task.Run` means "a single producer's same-topic confirmations can **overlap**, so AC-10's same-topic concurrent callback path ... is **genuinely reproduced in-process**." `Task.Run` only *queues* work to the thread pool; whether two raises actually run simultaneously depends on pool thread availability and on the raise bodies being slow enough to overlap. For very short raise bodies (the common case — log + `TripTopic` against in-memory state) the pool may execute them effectively serially, so a naive AC-10 test could pass without ever exercising true concurrency, giving false confidence. The ADR presents the overlap as guaranteed ("genuinely reproduced") rather than as a best-effort emulation a test must actively force.

**Evidence**: ADR concurrency-scope bullet: "Because the worker dispatches each raise via `Task.Run`, a **single** producer's same-topic confirmations can **overlap**, so AC-10's ... callback path ... is **genuinely reproduced in-process**."

**Recommendation**: Soften to "can overlap" (best-effort, same model as real Kafka) and note the AC-10 test should force overlap with a synchronization gate rather than relying on the pool to schedule the raises concurrently.

---

### 5. Kafka capture-site prose mixes "`Send`/`SendAsync`" with the real closure site `SendWithDelay`/`SendWithDelayAsync` (Score: 28)

The Kafka data-path section says capture happens "inside `Send`/`SendAsync`," but Kafka's `Send`/`SendAsync` are thin delegators (`Send → SendWithDelay(message, TimeSpan.Zero)` at `:200`; `SendAsync → SendWithDelayAsync(message, TimeSpan.Zero, ct)` at `:215`); the closure and the `WriteProducerEvent`/publish calls live in `SendWithDelay`/`SendWithDelayAsync`. The capture-ordering paragraph gets this right ("top of `SendWithDelay`/`SendWithDelayAsync`"), so the ADR is internally inconsistent but the correct site is stated. Cosmetic.

**Evidence**: `KafkaMessageProducer.cs:200, 215` (delegators) vs closure at `:260, :333`.

**Recommendation**: Make the FR-8/FR-2 data-path paragraph consistent with the capture-ordering paragraph — capture at the top of `SendWithDelay`/`SendWithDelayAsync`.

---

## Summary

| Score Range | Count |
|-------------|-------|
| 90-100 (Critical) | 0 |
| 70-89 (High) | 0 |
| 50-69 (Medium) | 3 |
| 0-49 (Low) | 2 |

**Total findings**: 5
**Findings at or above threshold (60)**: 2
