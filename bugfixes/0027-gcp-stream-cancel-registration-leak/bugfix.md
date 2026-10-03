# Bugfix: GCP Pub/Sub Stream per-message CancellationToken registration is never disposed, so every settled message stays reachable until the SubscriberClient stops

**Linked Issue**: #4505
**Status**: Confirmed

## Symptom
**Observed (suspected, not yet reproduced):** On a stream-mode GCP Pub/Sub channel, every message the
`SubscriberClient` delivers leaves a `CancellationTokenRegistration` that is never disposed. The registration's
callback closure holds the message's `GcpStreamMessage`, and that holds:
- the full `PubsubMessage`: Data `ByteString`, Attributes, OrderingKey and MessageId;
- a completed `TaskCompletionSource<Reply>` and its `Task`.

None of this can be collected after the message is acked or nacked and `HandleMessage` returns. It is released only
when the token's source is cancelled, which happens when the client stops. Retained memory therefore grows linearly
with the number of messages delivered, payload included.

There is also a cost at stop: every accumulated callback runs, including those for messages settled long ago. That
is O(total messages delivered) work on the thread calling `StopAsync`.

**Expected:** The registration lives only while the message is in flight. Once `HandleMessage` returns, nothing on
the library's token references the `GcpStreamMessage`, and stopping the client cancels only messages that are still
unsettled.

**Suspected reproduction:**
1. Open a stream channel on the emulator.
2. Receive N messages and acknowledge each one.
3. With the channel still open, force a full GC.

Every `GcpStreamMessage` (the `ReceiptHandle` in `Message.Header.Bag`) is still reachable.

**Not affected:** Pull mode (`GcpPullMessageConsumer`), which has no callback or TCS.

## Suspected Location
Line numbers are for the current tree, after 0026.
- `src/Paramore.Brighter.MessagingGateway.GcpPubSub/GcpStreamConsumer.cs:103-117`: `BrighterStreamHandler.HandleMessage`.
  - `:107`: `new GcpStreamMessage(message)`.
  - `:108`: `cancellationToken.Register(() => streamMessage.Cancel(cancellationToken));`. **The registration it
    returns is discarded.**
  - `:110`: `writer.WriteAsync` (unbounded channel); `:111`: `WaitForCompleteAsync`; `:113-116`: the
    `catch (OperationCanceledException)` that returns `Nack`.
- `GcpStreamConsumer.cs:125-183`: `record GcpStreamMessage`.
  - `:127`: `_tcs = new()`, created without `RunContinuationsAsynchronously`.
  - `:133-140`: `SetCancellationToken` looks unused. It calls `SetCanceled`, which throws on a settled message.
  - `:174-177`: `Cancel` calls `_tcs.TrySetCanceled`.
  - `:182`: `CanProcess`, used at `GcpPubSubStreamMessageConsumer.cs:261`.
- `GcpStreamConsumer.cs:49`: one `BrighterStreamHandler` per client. `:81`: `client.StopAsync(NackImmediately)` is the
  only point at which the token is cancelled.
- `src/Paramore.Brighter.MessagingGateway.GcpPubSub/Parser.cs:79`: `Bag["ReceiptHandle"] = receivedMessage`. This is
  the public path to the `GcpStreamMessage`.
- `GcpPubSubConsumerFactory.cs:139-141`: flow control limits only *in-flight* messages, not the retention of settled
  ones.

**Google.Cloud.PubSub.V1 3.36.0** (`lib/netstandard2.0`, IL read with `ikdasm`):
- `SubscriberClientImpl/SingleChannel` declares `initonly CancellationTokenSource _handlerCts`. `SingleChannel::.ctor`
  (IL_0095–IL_00f3) sets it to `CreateLinkedTokenSource(client._globalNackImmediatelyCts.Token,
  client._globalHardStopCts.Token)`. This is the only `stfld _handlerCts` in the assembly.
- `SingleChannel/<>c__DisplayClass63_1::<ProcessPullMessagesAsync>b__1` calls
  `_handler.HandleMessage(msg.Message, _handlerCts.Token)`. **This is the token Brighter receives.**
- `SingleChannel` is constructed only in `SubscriberClientImpl.StartAsync`, once per `ClientCount` index.
- Stream reconnects (`StopStreamingPull`, `StartStreamingPull`) replace only `_pull`, never `_handlerCts`.
- `_handlerCts.Cancel()` (`<StartAsync>d__49` IL_0155) runs only as the client stops, or earlier through the link
  when `StopAsync` cancels the global CTSs.
- The library's own `NackOnShutdownRegistration` keeps the registration it gets back. Brighter's code does not.

**Other `Register` sites:** none. A grep of `src/**/*.cs` finds only `GcpStreamConsumer.cs:108`.

## Root-Cause Hypothesis
**H1 (token lifetime; established from IL):** The `HandleMessage` token is `SingleChannel._handlerCts.Token`. That
source is a read-only linked CTS created once per `SingleChannel`, which means once per client lifetime ×
`ClientCount`. Stream reconnects don't replace it, and it is cancelled only when the client stops. The leak is
therefore **unbounded for the life of the client**, not bounded by stream. Because the client is shared and cached
per subscription, that is effectively the life of the process. Confirm only needs to sanity-check this.

**H2 (retention mechanism; UNVERIFIED — to be proven or refuted in /bugfix:confirm):** `Register` adds a callback node
to the source CTS. The node, the delegate and its closure stay strongly reachable until the registration is disposed
or the CTS is cancelled. The retention chain is `_handlerCts` → callback node → delegate → display class
(`streamMessage`, `cancellationToken`) → `GcpStreamMessage` → `PubsubMessage` (payload). The Brighter `Message` is not
retained, because the Bag points to `GcpStreamMessage` and not the other way round. Each message retains about
250–350 bytes plus its payload.
- **Falsified if:** settled `GcpStreamMessage`s are collected after a full GC while the token's source is still
  alive.

**H3 (O(n) cancellation at stop; UNVERIFIED — to be proven or refuted in /bugfix:confirm):** `StopAsync`
(`NackImmediately`) cancels `_handlerCts` through the link, and that synchronously runs every accumulated callback.
For settled messages each one is a no-op `TrySetCanceled`, but the total work is O(messages delivered) inside the
`Dispose`/`StopAsync` path. For messages still in flight, the `HandleMessage` continuation runs inline, because
`_tcs` has no `RunContinuationsAsynchronously`. That is existing behaviour and not this bug.

**H4 (interactions; analysis only, no defect expected):**
- If the token is already cancelled at `Register`, the callback runs synchronously, `WriteAsync` then throws an
  `OperationCanceledException`, and the handler returns `Nack`.
- If the token is cancelled after the write, the handler returns `Nack`, and the buffered copy is skipped by
  `CanProcess`.
- The leak is on the **success** path. `SetCancellationToken` is dead code; it is a scope note, not this bug.

**Experiments for /bugfix:confirm:**
1. **Unit-level probe: real public types, no mocks, no emulator.**
   - Build `BrighterStreamHandler` over a real unbounded `Channel<GcpStreamMessage>`, with a long-lived, uncancelled
     `CancellationTokenSource` standing in for `_handlerCts` (justified by H1).
   - In a `NoInlining` helper:
     - call `HandleMessage` with a 64 KB message;
     - read the `GcpStreamMessage` back;
     - take a `WeakReference` to it;
     - call `Accepted()`;
     - await the reply (expect `Ack`).
   - Force a full GC. **Today's expectation: alive.**
   - Control: call `cts.Cancel()`, force another GC, and expect the message to be collected. This shows the CTS is the
     root.
   - Repeat with N = 1,000 and count the survivors.
2. **End-to-end emulator probe.**
   - Through `GcpStreamMessageGatewayProvider`, receive and ack N messages.
   - Take a `WeakReference` to each `Bag["ReceiptHandle"]` in a `NoInlining` helper.
   - With the channel **still open**, force a GC and count the survivors (expect about N today).
   - **Caveat:** a check made only *after* dispose passes today, because the cancel releases the list.
3. **Optional, H3:** time `cts.Cancel()` with N settled registrations.
4. **Optional, H1 sanity check:** confirm that settled messages are still alive after a stream reconnect.

**Regression-test credibility:**
- Experiment (1) is the strongest candidate: deterministic, fast, needs no infrastructure, and uses public types
  only.
- Experiment (2) also works as an integration test, but GC and JIT liveness make it noisier. Use counts over N rather
  than a single object.
- Reflection on CTS registration counts is **not** recommended. It depends on Google's and the BCL's internals, which
  differ between .NET Framework and .NET.

## Confirmed Root Cause
**Verdict: CONFIRMED.**

`GcpStreamConsumer.cs:108` throws away the `CancellationTokenRegistration` for every message. The token belongs to
`SingleChannel._handlerCts`. That source is created once per `SingleChannel`, never replaced (including on stream
reconnects), and cancelled only when the client stops. Each registration is a callback node that the source holds
strongly. The chain is:

> callback node → `Action` → closure (`streamMessage`, `cancellationToken`) → `GcpStreamMessage` → `PubsubMessage`
> (payload) + the completed TCS and its `Task`

As a result, every settled message stays reachable until the client stops. Because the client is cached in the
static `s_consumers`, that is effectively the life of the process. Neither the Brighter `Message` nor the
`HandleMessage` state machine is retained.

At stop, `SubscriberClientImpl.StopAsync` (a plain, non-async method) cancels `_globalNackImmediatelyCts` on the
calling thread. The link then cancels `_handlerCts`, which synchronously runs all N callbacks on the thread that is
running `GcpStreamConsumer.StopAsync`, that is, the channel's `Dispose`/`DisposeAsync`.

## Evidence
- [x] **Code trace** (Plan sub-agent, opus; IL read with `ikdasm` on both the netstandard2.0 and net462 DLLs):
  - **There is only one `HandleMessage` call site:** `SingleChannel/<>c__DisplayClass63_1::<ProcessPullMessagesAsync>b__1`
    IL_0026–IL_0030, which passes `_handlerCts.Token` directly. The other two matches are declarations.
    `SimpleSubscriptionHandler` serves only the `StartAsync(Func)` overload, which Brighter doesn't use.
  - **`_handlerCts` is `initonly`.** Its only `stfld` is `SingleChannel::.ctor` IL_00f3, and `SingleChannel` is
    constructed only at `StartAsync` IL_0039, once per `ClientCount`.
  - **Registrations are held strongly on both runtimes.** On .NET 8+ they are `CallbackNode`s in
    `Registrations.Callbacks`; on .NET Framework they are `CancellationCallbackInfo`s in a `SparselyPopulatedArray`.
    Neither has weak semantics or compaction; only `Dispose`/`Unregister` or cancelling the source releases a
    node.
  - **The stop path:** `StopAsync` picks `_globalNackImmediatelyCts` (IL_00b6–IL_00bc) and calls `.Cancel()` at
    IL_00d8. `ExecuteCallbackHandlers` then runs every callback synchronously.
- [x] **Red repro, observed** (2026-10-03, net10.0). This was a throwaway probe, now removed from the repo; a copy is
  in the session-7 scratchpad as `Probe0027Tests.cs`.

  | Probe | Setup | Observed |
  |---|---|---|
  | Unit, no infrastructure | Real `BrighterStreamHandler` over an unbounded channel, with a long-lived uncancelled CTS standing in for `_handlerCts`. 1,000 × 64 KB messages, each handled, `Accepted()` and replied `Ack` inside a `NoInlining` helper, then a full GC. | **1000/1000 `GcpStreamMessage`s alive.** About 155 MB retained (rough `GC.GetTotalMemory`). Control: `cts.Cancel()` took **0.21 ms**, and after a GC **0/1000** were alive, so the CTS is the root. |
  | Emulator, real `SubscriberClient` | A stream channel receives and acks 20 messages, with a `WeakReference` to each `Bag["ReceiptHandle"]`. Full GC **with the channel still open**, then dispose and GC again. | **20/20 alive while open.** **0/20** after dispose. This confirms H1 on the real client's token. |

## Suggested-Fix Assessment
**CONFIRMED:** put `using var registration = cancellationToken.Register(...)` inside the `try`, before `WriteAsync`.
It is disposed on the success path and on both cancellation (OCE) paths.
- **Disposing on the callback's own thread** (cancel → inline continuation → `Nack` → dispose) doesn't wait. Both
  .NET and .NET Framework detect that the dispose is running on the thread that is executing the callbacks.
- **Disposing from another thread** spins only until the current callback finishes. That callback is
  `TrySetCanceled`, which never blocks, and the stop thread never waits on the consumer thread, so there is no
  cycle.
- **No constraints are needed:**
  - Plain `Dispose` works on every TFM. `Unregister` and `DisposeAsync` are netcoreapp3.0+, and `UnsafeRegister` isn't
    on netstandard2.0; none of them is needed.
  - `RunContinuationsAsynchronously` on `_tcs` isn't needed for correctness. It would be optional hardening, and it is
    out of scope.
- **Optional:** the state overload `Register(s => …, streamMessage)` avoids the display-class allocations. That's a
  style choice for the fix.

## Scope Notes
1. **The O(n) cancellation at stop goes away with the fix.** Once each registration is disposed on settlement, only
   in-flight messages are still registered at stop. Measured cost today: 0.21 ms per 1,000 settled messages, so it is
   minor. It needs no separate test.
2. **Dead code:** `GcpStreamMessage.SetCancellationToken` (`GcpStreamConsumer.cs:133-140`) has no callers in `src` or
   `tests`. It calls `SetCanceled`, which throws on a settled TCS. Leave it out of this fix; remove or fix it
   separately. Removing it is a public API change.
3. **No other `CancellationToken.Register` sites** in `src/**/*.cs`. `ServiceCollectionExtensions.cs:815` is
   `messageMapperRegistry.Register`, which is unrelated. There is no cross-transport parity gap.
4. **Minor and bounded, not this bug:** a stopped `GcpStreamConsumer` stays in `s_consumers` until the next
   `CreateAsync` for the same subscription instance (0026's eviction is lazy). Until then it keeps the disposed client
   alive, along with any cancelled but unread `GcpStreamMessage`s in its channel, which flow control bounds by
   `BufferSize * NoOfPerformers`.
5. **No other per-message retention.** `GcpPubSubStreamMessageConsumer`, `GcpRejectionRouter` and `Parser` hold no
   per-message collections.

## Regression Test
Unit level with no infrastructure, as the user chose at the Confirm gate. Approved 2026-10-03.
- `tests/Paramore.Brighter.Gcp.Tests/MessagingGateway/Stream/When_a_gcp_stream_message_is_settled_should_not_be_kept_alive_by_the_handler_token.cs`
  (`GcpStreamSettledMessageRetentionTests`, Category `GcpPubSub`, no `Stream` category, so **CI's GCP filter runs
  it**).
- Setup: the public `BrighterStreamHandler`, a long-lived CTS standing in for `_handlerCts`, and 100 messages, each
  handled, `Accepted()` and replied `Ack`, followed by a full GC. Asserts that 0 survive.
- RED: `Expected: 0, Actual: 100`.

## Fix
`GcpStreamConsumer.cs` `HandleMessage`: `using var registration = cancellationToken.Register(...)`, inside the `try`
and before `WriteAsync`, with a comment explaining why. The registration is disposed when `HandleMessage` exits, on
the success path and on both cancellation paths. That is safe on every TFM; see the Suggested-Fix Assessment.

**Verification (2026-10-03):**
- The gateway builds on all TFMs.
- The new test passed **3/3 on net9.0 and 3/3 on net10.0**, the test project's two TFMs.
- Emulator, net10.0:
  - **First Stream run: 123/2/25.** One failure was the known StreamOrdering two-message Nack flake. The other was
    `GcpStreamDisposeWithBufferedRedeliveryTests` (sync), where the redelivery after dispose didn't arrive within
    20 s.
  - In isolation that test passed **6/6 with the fix and 6/6 with `GcpStreamConsumer.cs` reset to HEAD**, and it had
    passed in both of 0026's full runs. I treat it as a one-off; it wasn't reproduced.
  - **Stream re-run: 125/0/25**, with no test changing outcome against the 8.7 TRX, compared by name.
  - **CI filter: 157/49/30.** No baseline test is missing. The only change is the unrelated GCS test
    `LuggageStoreExistsTests.When_checking_store_that_does_not_exist` going from Failed to Passed. The 157 includes
    the new test.
- **Real Pub/Sub: not applicable.** The regression test doesn't touch Pub/Sub, and the emulator probe at Confirm
  already showed the real client's token behaves as H1 predicts.
