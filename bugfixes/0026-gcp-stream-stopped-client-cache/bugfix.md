# Bugfix: GCP Pub/Sub Stream consumer cache reuses a stopped, disposed SubscriberClient when a channel is reopened

**Linked Issue**: #4502
**Status**: Verified

## Symptom
**Observed (as reported, not yet reproduced):** In `SubscriptionMode.Stream`, take one `GcpPubSubSubscription`
instance S. Create one or more channels on S and dispose all of them. If you then create a channel on the **same
instance S** again, the channel opens but never receives a message, and nothing reports a failure from the second
`SubscriberClient.StartAsync`.

**Expected:** The reopened channel gets a working streaming client and receives messages. A failure to start, or a
later runtime fault of the stream, is reported and not silently lost.

**Where this happens in practice** (these paths were checked in the code):
- `Dispatcher.Shut(name)` followed by `Dispatcher.Open(name)`. `Open(SubscriptionName)` looks up the *existing*
  instance (`Subscriptions.Single(...)`), so it reuses S. `AddSubscriptionToSubscriptions` never replaces an
  instance that has the same name.
- `Dispatcher.SetActivePerformers(name, 0)` followed by `SetActivePerformers(name, n>0)`. Scale-up calls
  `CreateConsumer(subscription, i)` with the same instance.
- Any user code that keeps one subscription object and calls `ChannelFactory.CreateSyncChannel` or
  `CreateAsyncChannel` again after disposing the earlier channel.

**Not affected:**
- A *new* subscription instance with the same names. The cache key compares by reference, so a new instance gets a
  fresh client. That is why the existing tests don't hit this bug.
- Pull mode.

**Proposed repro** (emulator on localhost:8085, no mocks):
1. `provider.CreateSubscription(routingKey, channelName, OnMissingChannel.Create)` returns S (Stream mode).
2. `var ch1 = provider.CreateChannel(S); ch1.Dispose();` The handler count drops to 0, and the client is stopped and
   disposed.
3. `var ch2 = provider.CreateChannel(S);` Note whether this throws.
4. Send a message to `routingKey`, then call `ch2.Receive(...)` with a bounded timeout. Expect the message. The
   hypothesis predicts an empty receive (`MT_NONE`), or a throw at step 3.

Run the repro in both variants, sync (Reactor/`Channel`) and async (Proactor/`ChannelAsync`), because their dispose
paths differ (see H2). If needed, add a dispatcher-level variant: `Receive()`, `Shut(name)`, wait for the stop,
`Open(name)`, send, then assert that the handler ran.

## Suspected Location
**The cache and the client lifecycle:**
- `src/Paramore.Brighter.MessagingGateway.GcpPubSub/GcpPubSubConsumerFactory.cs:62`:
  `static readonly ConcurrentDictionary<GcpPubSubSubscription, GcpStreamConsumer> s_consumers`. It is static, so it is
  shared by every factory in the process, and it never removes an entry.
- `GcpPubSubConsumerFactory.cs:96-99`: `GetOrAdd` creates a `SubscriberClient` only on the first add, and fixes
  `BufferSize * NoOfPerformers` flow control at that point.
- `GcpPubSubConsumerFactory.cs:102`: `consumer.Start()` runs on every channel creation.
- `src/Paramore.Brighter.MessagingGateway.GcpPubSub/GcpStreamConsumer.cs:29-38`: `Start()` calls
  `Interlocked.Increment`. If the result is not above 1, it calls `client.StartAsync(...)` at **:37** and discards the
  returned `Task`.
- `GcpStreamConsumer.cs:50-58`: `StopAsync()` decrements the count. At 0 it calls `client.StopAsync(NackImmediately)`
  (**:55**) and `client.DisposeAsync()` (**:56**). It neither replaces `client` nor removes itself from `s_consumers`.
- `GcpStreamConsumer.cs:10`: `client` is captured by the primary constructor, so it can't be replaced.

**The dispose chain to `StopAsync`:**
- `src/Paramore.Brighter.MessagingGateway.GcpPubSub/GcpPubSubStreamMessageConsumer.cs:309-313` (`Dispose` calls
  `consumer.StopAsync().GetAwaiter().GetResult()`) and `:319-323` (`DisposeAsync`). Neither has a guard.
- `src/Paramore.Brighter/Channel.cs:190-202`: sync `Channel.Dispose` has no `_disposed` guard.
  `src/Paramore.Brighter/ChannelAsync.cs:197-203` and `:220-228` are guarded.
- `src/Paramore.Brighter.ServiceActivator/Reactor.cs:183` disposes the channel on quit. `Reactor.cs:105`, `:154`,
  `:296` and `:312` dispose it on the error and limit paths. The Proactor equivalents are `Proactor.cs:146`, `:195`,
  `:224`, `:333` and `:344`.
- `src/Paramore.Brighter.ServiceActivator/Dispatcher.cs:553-582` (`HandleNextStoppedPerformer` and
  `RemoveConsumerForTask`) leads to `Consumer.cs:142-147` and then to `Performer.cs:89-94`, which calls
  `_channel.Dispose()`. That is a **second** dispose after the pump's.

**The paths that recreate a channel with the same instance:**
- `Dispatcher.cs:356-359`: `Open(SubscriptionName)` resolves to the existing instance.
- `Dispatcher.cs:365-398`: `Open(Subscription)` and `AddSubscriptionToSubscriptions`.
- `Dispatcher.cs:422-434`: `Shut`.
- `Dispatcher.cs:445-475`: `SetActivePerformers`. Scale-up is at `:457-463`.
- `Dispatcher.cs:597-609` leads to `src/Paramore.Brighter.ServiceActivator/ConsumerFactory.cs:100` and `:124`, then
  to `src/Paramore.Brighter.MessagingGateway.GcpPubSub/GcpPubSubChannelFactory.cs:17`, `:46` and `:86`.

**Key equality:** `src/Paramore.Brighter/Subscription.cs:35` and
`src/Paramore.Brighter.MessagingGateway.GcpPubSub/GcpPubSubSubscription.cs:10` are plain classes with no
`Equals`/`GetHashCode` override, so the cache compares keys **by reference**.

## Root-Cause Hypothesis
**H1 (primary): the stream consumer cache outlives its client.** `s_consumers` is keyed by subscription reference.
When the handler count reaches 0, `GcpStreamConsumer.StopAsync` stops and disposes its `SubscriberClient`, but the
entry stays in the cache. A later `CreateAsync` with the same instance gets the dead consumer from `GetOrAdd`, and
`Start()` calls `StartAsync` a second time on the same client.

The Google library says `StartAsync` "cannot be called more than once per `SubscriberClient` instance". This is from
the 3.36.0 XML docs (`Directory.Packages.props:61`,
`~/.nuget/packages/google.cloud.pubsub.v1/3.36.0/lib/net462/Google.Cloud.PubSub.V1.xml:11528-11536`), and the DLL
contains the string `"Can only start an instance once."`. In either case, the new channel reads from a `Reader` that
nothing will ever write to again.

**Settled in Confirm as outcome (a): a synchronous throw. The silent symptom appears only on the reopen after that,
because the throw leaks the handler count. See Confirmed Root Cause.** Originally recorded as UNVERIFIED: the
restart failure is "silently dropped" because the Task is discarded. There are two possible outcomes:
- **(a) A synchronous throw.** `StartAsync` checks state before it returns a Task. The exception escapes `Start()` →
  `CreateAsync` → `BrighterAsyncContext.Run`, so channel creation fails loudly; under the dispatcher it surfaces from
  `CreateConsumer` in `Open` or `SetActivePerformers`. The handler count still leaks, because it was incremented
  before the throw.
- **(b) A faulted Task.** That Task is discarded at `:37`, so the channel never receives anything, as the issue
  describes.

**A separate defect in the same area, confirmed by reading the code:** the docs say the `StartAsync` Task "completes
when the subscriber is stopped, or if an unrecoverable error occurs". Discarding it at `:37` therefore also hides a
fault in a *running* stream, such as a permission error, a not-found error or a fatal gRPC error. The client stops
delivering and the pump polls an empty channel. Record this separately; don't merge it into H1.

**H2 (compounding, Reactor only): the double dispose drives the handler count negative.** The Reactor disposes the
channel on quit (`Reactor.cs:183`), and `Performer.Dispose` (`Performer.cs:93`) disposes it again. Sync `Channel`
doesn't guard against this, so `StopAsync` runs twice per performer and `_handlers` goes from 0 to −1. When the
channel reopens, `Start()` takes the count from −1 to 0, which is not above 1, so it calls `StartAsync`. Any fix that
relies on "0 means stopped" must keep the counter from going negative. This is defect 3 in
`bugfixes/0023-gcp-stream-pump-requeue-hang/bugfix.md`, Scope Notes 4.

**H3 (secondary race; note only):** `Start` and `StopAsync` coordinate only through `Interlocked` on the counter.
Suppose a stop decrements the count to 0, and a concurrent `Start` increments it back to 1 before
`client.StopAsync`/`DisposeAsync` finishes. `Start` then calls `StartAsync` on a client that is still stopping, and
the stop goes on to dispose that client under the new channel. This doesn't need to be the focus of the regression
test.

**Other observations (not causal):**
- `s_consumers` never evicts an entry, so each distinct subscription instance leaves a disposed client in a static
  dictionary for the life of the process.
- Flow control (`:98`) is fixed when the client is first created, and doesn't follow `SetActivePerformers`.

**Existing tests:** The hand-written Stream tests (`tests/Paramore.Brighter.Gcp.Tests/MessagingGateway/Stream/`) and
the generated Stream tests all create a **new** subscription instance for each channel. For example,
`When_a_gcp_stream_channel_is_disposed_holding_an_unsettled_message_should_return_promptly.cs:79-80` deliberately
calls `provider.CreateSubscription(...)` again. No test reuses an instance after a dispose, and no test relies on
sharing through the cache.

**Experiments for /bugfix:confirm:**
1. Run the emulator repro above in both variants, with the same instance S. Record outcome (a) or (b). In both cases,
   check that `ch2` doesn't receive the message; that confirms H1.
2. Control: repeat with a *new* instance S′ (same names). Expect the message to arrive, which shows that
   reference-keyed caching is the trigger rather than the broker's state.
3. H2: call sync `Channel.Dispose()` twice on `ch1`, then observe `_handlers` with a debugger or a throwaway probe.
   Expect −1.
4. Optional, for the runtime-fault blind spot: start a stream channel, delete the subscription on the emulator, and
   check whether anything is logged or thrown.

## Confirmed Root Cause
**Verdict: CONFIRMED, with a correction to the issue's description of the failure.**

`GcpPubSubConsumerFactory.s_consumers` (`:62`) is static, compares keys by reference, and never removes an entry.
`GcpStreamConsumer.StopAsync` (`:55-56`) stops and disposes the `SubscriberClient` when the handler count reaches 0,
but the consumer stays in the cache. A channel created later on the **same** subscription instance gets the dead
consumer from `GetOrAdd` (`:96`), and `Start()` (`GcpStreamConsumer.cs:37`) calls `StartAsync` on it a second time.

In Google.Cloud.PubSub.V1 3.36.0, `SubscriberClientImpl.StartAsync` is **not** async. Under a lock it calls
`GaxPreconditions.CheckState(_mainTcs == null, "Can only start an instance once.")`. Nothing ever resets `_mainTcs`,
so a `SubscriberClient` can never be started again.

What happens, in order:
1. **The first reopen throws synchronously:** `InvalidOperationException: Can only start an instance once.` This is
   outcome (a). The issue's claim that the error is "silently dropped" is wrong for this first reopen.
2. **The throw leaks the handler count.** `Interlocked.Increment` (`:31`) ran before the throw, and no wrapper consumer
   is built that could ever decrement it.
3. **The second reopen is silent.** `Start()` increments past 1 and returns early without calling `StartAsync`. The
   channel is built over a `Reader` that nothing writes to, and it receives `MT_NONE` forever. This is the symptom the
   issue describes.

**H2 is confirmed (Reactor only).** Sync `Channel.Dispose` (`Channel.cs:190-202`) has no guard, and
`GcpPubSubStreamMessageConsumer.Dispose` (`:309-313`) has none either. Each Reactor performer therefore decrements the
count twice: once in the pump (`Reactor.cs:183` and the error paths), and again via `Performer.cs:93`. `ChannelAsync`
is guarded (`:199-200`, `:222-223`).

## Evidence
- [x] **Code trace** (Plan sub-agent, opus):
  - **The same instance reaches `GetOrAdd`:** `Dispatcher.Open(name)` → `Subscriptions.Single` (`Dispatcher.cs:358`) →
    `CreateConsumers` (`:370`) → `CreateConsumer` (`:591`, `:606`/`:616`) → `ConsumerFactory.cs:100`/`:124` →
    `GcpPubSubChannelFactory.cs:46`/`:86` → `GcpPubSubConsumerFactory.cs:96`, `:102`. `SetActivePerformers` (`:447`,
    `:459`) and `End()` → `Receive()` (`:405`) take the same route.
  - **Key equality:** `Subscription.cs:35` and `GcpPubSubSubscription.cs:10` are classes with no `Equals`/`GetHashCode`
    override.
  - **The count reaches 0 only after every pump has quit:** `Shut` → `Consumer.Shut` → `Performer.Stop` → MT_QUIT. The
    pump's dispose comes first, then `RemoveConsumerForTask` (`Dispatcher.cs:570-582`) → `Consumer.cs:146` →
    `Performer.cs:93`.
  - **Throw vs. faulted Task,** settled statically by `ikdasm` on
    `google.cloud.pubsub.v1/3.36.0/lib/netstandard2.0/Google.Cloud.PubSub.V1.dll`:
    - `SubscriberClientImpl::StartAsync(SubscriptionHandler)` has no `AsyncStateMachineAttribute`.
    - It runs `CheckState(_mainTcs == null, …)` under `Monitor.Enter`.
    - The only `stfld _mainTcs` in the assembly is inside `StartAsync`.
    - `DisposeAsync` and `StopAsync` on a stopped client are idempotent: they return `_mainTcs.Task`.
  - **Other callers:** none. `Start()` is called only at `GcpPubSubConsumerFactory.cs:102`, and `StopAsync` only at
    `GcpPubSubStreamMessageConsumer.cs:311` and `:321`.
- [x] **Red repro, observed on the emulator** (localhost:8085, net10.0, 2026-10-03). This was a throwaway probe, now
  removed from the repo; a copy is in the session scratchpad as `Probe0026Tests.cs`. `_handlers` was read by reflection
  in the probe only. Stream mode throughout.

  | Case | Steps | Observed |
  |---|---|---|
  | A, sync, same instance | open, dispose, reopen | handlers 1 → 0. The reopen **threw** `InvalidOperationException: Can only start an instance once.` Handlers after the throw = **1** (leaked). |
  | D, async, same instance | open, `DisposeAsync`, reopen | handlers 0. The reopen **threw** the same exception. |
  | E, sync, retry after the throw | open, dispose, reopen (throws), reopen again, send, `Receive(20s)` | The 1st reopen threw (handlers = 1). The 2nd reopen **did not throw** (handlers = 2). The receive returned **`MT_NONE`** after 20 s: a silently dead channel. |
  | C, sync, double dispose | open, dispose twice, reopen | handlers 1 → 0 → **−1**. The reopen threw (−1 → 0 still calls `StartAsync`). Handlers after the throw = 0. |
  | B, control: new instance S′ with the same names | open, dispose, open on S′, send, receive | No throw, handlers = 1, received `MT_EVENT`. |

## Suggested-Fix Assessment
**PARTIAL.** The issue's expected behaviour, "reopening starts a working client", is right. Its claim that the start
failure is "silently dropped" is wrong for the first reopen, which throws. What actually goes unobserved is:
- the handler-count leak, which turns the next reopen into a silent dead channel;
- a *runtime* fault of a running stream, because the Task that `StartAsync` returns is discarded (Scope Note 1).

Any fix must satisfy these constraints:
1. When the last handler leaves, either replace the client or evict the entry from the cache. A `SubscriberClient`
   cannot be restarted.
2. Eviction must be atomic with a concurrent `GetOrAdd` + `Start`, because the dictionary is static and shared by
   every factory. Otherwise the H3 race returns: a `Start` that lands between the decrement to 0 and
   `client.StopAsync`/`DisposeAsync` loses its client.
3. The count must neither leak nor go negative:
   - Roll back the increment if `StartAsync` throws.
   - Make the stream consumer's `Dispose`/`DisposeAsync` idempotent (see Scope Note 2).
4. Compute flow control (`BufferSize * NoOfPerformers`, `:98`) for the new client. `SetActivePerformers` changes
   `NoOfPerformers` before it creates consumers (`Dispatcher.cs:454`).
5. Prefer a fresh `Channel<GcpStreamMessage>` per client over reusing the old `Reader`.

## Scope Notes
**User's scope decision at the Confirm gate (2026-10-03): cache plus idempotent dispose.** This fix will:
- evict or replace the stopped client;
- undo the increment when the start throws;
- make the GCP stream consumer's `Dispose`/`DisposeAsync` idempotent, which closes Scope Note 2 for GCP.

The guard on the core sync `Channel` stays as defect 3. Each behaviour gets its own RED test before any code is
written.

1. **The runtime-fault blind spot is a separate defect, confirmed by the docs and the IL.** The Task that
   `StartAsync` returns completes on stop *or on an unrecoverable fault*, and `GcpStreamConsumer.cs:37` discards it.
   So a NotFound, a PermissionDenied or a fatal gRPC error stops delivery with no log and no throw. Unverified: a
   later `StopAsync` on a faulted client may rethrow out of `Dispose`. This should be its own issue and bugfix.
2. **New finding, not in the triage: in the Reactor, a double dispose stops the shared client under live
   performers.** It is code-traced only, not probed. Take two or more Reactor performers on one subscription. Scaling
   down by one, or one performer hitting a Reactor error or limit dispose path (`Reactor.cs:105`, `:154`, `:296`,
   `:312`), decrements twice. With 2 performers that goes 2 → 1 → 0, which stops and disposes the client while
   performer B is still running. B then silently receives nothing. No reopen is involved. The cause is the unguarded
   `Channel.cs:196-202` (defect 3) plus the unguarded `GcpPubSubStreamMessageConsumer.cs:309-313`. A guard on the GCP
   consumer alone would close this for GCP without touching core.
3. **A Reactor `Shut` followed immediately by `Open`, before the old pumps quit** (H2 + H3): the new channel shares
   the live client (1 → 2). Then the old pump's two disposes take the count 2 → 1 → 0, which kills the client under
   the new channel. The Proactor is not affected.
4. **`SetActivePerformers` is left inconsistent** when `CreateConsumer` throws, because `NoOfPerformers` was already
   changed at `Dispatcher.cs:454`. This is core ServiceActivator behaviour, and it is out of scope here.
5. **A related static cache with the same "outlives the resource" shape.**
   `GcpPubSubMessageGateway.s_topicOrSubscriptionAlreadyCreatedUpdate` (`GcpPubSubMessageGateway.cs:20`) has two
   problems:
   - It records an entry with `TryAdd` *before* the ensure work runs (`:51`, `:208`), so a failed create is never
     retried.
   - `DeleteSubscription`/`DeleteTopic` (`GcpPubSubChannelFactory.cs:98-230`) never clear it, so delete-then-recreate
     in the same process skips creation.

   This is a separate defect and has not been raised.
6. **Unverified:** the `SeekRequest` built by `Purge`/`PurgeAsync` (`GcpPubSubStreamMessageConsumer.cs:185-188`,
   `:215`) and by `GcpPullMessageConsumer.cs:114` and `:139` sets only `Time` and no `Subscription`. This is relevant to
   defect 4 (`Purge`). Check it during that triage.
7. **Cross-transport parity:** no gap. The RMQ connection pools (`RmqMessageGatewayConnectionPool.cs`) check
   `IsOpen`, recreate the connection, and have reset paths.

## Regression Test
**Behaviour 1: reopening a channel on the same subscription instance.** Approved 2026-10-03.
- `tests/Paramore.Brighter.Gcp.Tests/MessagingGateway/Stream/When_a_gcp_stream_channel_is_reopened_on_the_same_subscription_should_receive_messages.cs`
  (sync)
- `…/When_a_gcp_stream_channel_is_reopened_on_the_same_subscription_should_receive_messages_async.cs` (async)
- RED on the emulator: 0/2. Both failed with `InvalidOperationException: Can only start an instance once.` at
  `GcpStreamConsumer.Start()` (`GcpStreamConsumer.cs:37`), called from `GcpPubSubConsumerFactory.cs:102`.
- The retry-after-throw variant can't be set up through public APIs once a reopen works, so these two tests cover it.

**Behaviour 3: idempotent dispose of the stream consumer.** Approved 2026-10-03.
- `tests/Paramore.Brighter.Gcp.Tests/MessagingGateway/Stream/When_a_gcp_stream_channel_is_disposed_twice_should_not_stop_another_channel_on_the_subscription.cs`
  (sync only)
- RED: `Assert.Equal` failed with an empty `Actual`, meaning the receive returned `MT_NONE` after 20 s. The second
  dispose took the shared count from 1 to 0 and stopped the client under the remaining channel.
- There is no async test. Through channels the double dispose can only happen on the sync path, because
  `ChannelAsync` is guarded. The user chose to guard `DisposeAsync` as well, for symmetry, without a test of its own.

## Fix
1. **`f87f16bac`, behaviour 1.**
   - `GcpStreamConsumer.Start()` is replaced by `bool TryStart()`. Under a lock, it refuses once the consumer has
     stopped.
   - `StopAsync` marks the consumer stopped, under the same lock, when the last handler leaves.
   - `GcpPubSubConsumerFactory` evicts a stopped consumer. The eviction is a compare-remove through
     `ICollection<KeyValuePair>.Remove`, because netstandard2.0 has no `TryRemove(KeyValuePair)`. It then creates a
     fresh consumer and client, so flow control is recomputed from the current `NoOfPerformers`.
   - **Public API change, accepted by the user:** `GcpStreamConsumer.Start()` has been removed. Add this to the
     release notes at Verify.
   - The rollback of the increment on a throw was **not** written. `StartAsync` can no longer hit "start once", so no
     test could make that code fail first.
2. **Behaviour 3.**
   - `GcpPubSubStreamMessageConsumer.Dispose` and `DisposeAsync` are now idempotent. They share an `_disposed` flag
     set with `Interlocked.Exchange`.
   - The core `Channel` is unchanged; its guard is defect 3.

**Regression runs** on the emulator (not reset), net10.0. The new tests passed 3/3.
- Stream: **125/0/25**, which is the baseline's 122 plus 3. No test changed outcome against the 8.7 TRX, compared by
  name.
- CI filter: **156/49/30**. No test is missing. One test changed: the GCS test
  `LuggageStoreExistsTests.When_checking_store_that_does_not_exist` went from Failed to Passed, and it is unrelated
  to this fix.

**Real Pub/Sub** (2026-10-03, project `brighter-gcp-diag-51720`, `PUBSUB_EMULATOR_HOST` unset, ADC, net10.0). The
user asked that GCP stream fixes are also verified against real Pub/Sub.
- **At HEAD:** the 3 new tests passed **3/3**, in two separate runs.
- **With the three src files temporarily reset to `34db6c64e`,** the commit before the fix, they **failed 3/3** for
  the same reasons as on the emulator:
  - both reopen tests: `InvalidOperationException: Can only start an instance once.`
  - the double-dispose test: `Assert.Equal` values differ (`MT_NONE`).
- The source was restored to HEAD afterwards, and the working tree was clean.
- The full Stream suite has **not** been run on real Pub/Sub.
