# Bugfix: GCP Purge sends a Seek with no subscription and leaves the stream's local buffer in place

**Linked Issue**: #4508
**Status**: Confirmed

## Symptom
- **Observed (claimed, unverified):** `Purge`/`PurgeAsync` on a GCP Pub/Sub channel may not remove messages.
  - **Problem 1 (Pull and Stream):** the `SeekRequest` sets only `Time` and never sets `Subscription`.
    Pub/Sub is expected to reject it. `Purge` then logs `PurgeError` and rethrows.
  - **Problem 2 (Stream only):** a message that `SubscriberClient` has already delivered into the local
    `Channel<GcpStreamMessage>` is still there after `Purge`. The next `Receive` returns it.
- **Expected:** after `Purge` returns:
  - the Seek has been applied to the consumer's own subscription;
  - `Receive` does not return any message published before the purge, including one already in the local buffer.
- **Reach:** `Channel.Purge` and `ChannelAsync.PurgeAsync` call the consumer's purge. `CommandProcessor.Call`
  purges the reply channel through a resilience pipeline. So a GCP reply channel would see problem 1 as a
  thrown (and retried) exception before the request is sent.
- **Reproduction (proposed, not yet run):**
  1. Create a GCP subscription (Pull, then Stream). Publish N messages.
  2. Stream only: call `Receive` once so the client starts and buffers messages. Do not settle them.
  3. Call `Purge()` / `PurgeAsync()`.
  4. Problem 1: observe whether `Purge` throws (`RpcException`, likely `InvalidArgument`).
  5. Problem 2: if the Seek succeeds, call `Receive` and observe whether a pre-purge message comes back.
- **Test coverage today:** no GCP test calls `Purge` against a broker.
  - The only GCP `Purge` references outside generated code are delegating test doubles:
    `tests/Paramore.Brighter.Gcp.Tests/MessagingGateway/Pull/When_a_gcp_pull_message_is_redelivered_should_present_increasing_delivery_count.cs:266`
    and
    `tests/Paramore.Brighter.Gcp.Tests/MessagingGateway/Stream/When_gcp_budget_is_one_zero_or_below_minus_one_should_reject_on_first_deferral.cs:185`
    (both `public void Purge() => inner.Purge();`).
  - Precedent for a regression test: `When_queue_is_purged` in MQTT, MSSQL and PostgreSQL, e.g.
    `tests/Paramore.Brighter.MQTT.Tests/MessagingGateway/Reactor/When_queue_is_purged.cs:75-96`. These send,
    call `Purge`, call `Receive`, and assert they get only an `MT_NONE` message.

## Suspected Location
- **Problem 1: Seek without `Subscription`**
  - `src/Paramore.Brighter.MessagingGateway.GcpPubSub/GcpPubSubStreamMessageConsumer.cs:182-202` (`Purge`).
    The request is built at `:190-193` and sets only `Time`.
  - `src/Paramore.Brighter.MessagingGateway.GcpPubSub/GcpPubSubStreamMessageConsumer.cs:211-230` (`PurgeAsync`).
    The request is built at `:219-221` and sets only `Time`.
  - `src/Paramore.Brighter.MessagingGateway.GcpPubSub/GcpPullMessageConsumer.cs:110-126` (`Purge`).
    The request is built at `:117-118` and sets only `Time`.
  - `src/Paramore.Brighter.MessagingGateway.GcpPubSub/GcpPullMessageConsumer.cs:134-153` (`PurgeAsync`).
    The request is built at `:142-144` and sets only `Time`.
  - Both consumers already have `subscriptionName` (primary-ctor parameter):
    - Stream `GcpPubSubStreamMessageConsumer.cs:22`; Pull `GcpPullMessageConsumer.cs:19`.
    - The Pull consumer already uses it for `PullRequest` (`:182`, `:268`), `Acknowledge` (`:487`)
      and `ModifyAckDeadline` (`:506`).
  - Client factories: `GcpMessagingGatewayConnection.cs:117-138` (`GetOrCreateSubscriberServiceApiClient`) and
    `:144-165` (`CreateSubscriberServiceApiClientAsync`).
    - They build a plain `SubscriberServiceApiClientBuilder` with `Credential` and the optional
      `SubscriptionManagerConfiguration` (`:126-128`, `:153-155`).
    - Nothing there sets a default subscription. A `SubscriberServiceApiClient` is not bound to one.
- **Problem 2: stream local buffer not drained**
  - `src/Paramore.Brighter.MessagingGateway.GcpPubSub/GcpStreamConsumer.cs:16-21` declares an unbounded
    `Channel<GcpStreamMessage>`. The only public view of it is the `Reader` at `:26`.
  - `GcpStreamConsumer.cs:103-119`: `BrighterStreamHandler.HandleMessage` writes each message (`:112`).
    It then awaits the message's TCS (`:113`), so the message is held unsettled and its lease keeps being extended.
  - `GcpStreamConsumer.cs:127-185`: `GcpStreamMessage` holds the TCS.
    - `Accepted()` gives Ack (`:158-161`); `Reject()` gives Nack (`:167-170`).
    - `Cancel` (`:176-179`) and `CanProcess` (`:184`) handle cancellation.
  - `GcpStreamConsumer.cs:66-84`: `StopAsync` nacks unsettled messages with `NackImmediately` (`:81`).
    That runs only when the last handler stops. It is not a purge path.
  - `GcpPubSubStreamMessageConsumer.cs:250-276`: `ReceiveAsync` reads `consumer.Reader` (`:258`, `:263`).
    It returns any message with `CanProcess` true (`:264-266`). Nothing in `Purge`/`PurgeAsync` touches the reader.
  - Effective buffer size: the channel is unbounded, but the `SubscriberClient` flow control caps outstanding
    messages at `BufferSize * NoOfPerformers` (`GcpPubSubConsumerFactory.cs:121-124`, `:139-141`).
  - Sharing: one `GcpStreamConsumer` (and so one buffer) is shared by every channel on the same subscription
    (`GcpPubSubConsumerFactory.cs:94-105`).
- **Core callers**
  - `src/Paramore.Brighter/Channel.cs:136-140`: `Purge` calls `_messageConsumer.Purge()` and then replaces its
    own `_queue` (`:139`). `_queue` is declared at `:42`.
  - `src/Paramore.Brighter/ChannelAsync.cs:140-144`: `PurgeAsync` calls `_messageConsumer.PurgeAsync` and then
    replaces `_queue` (`:143`).
  - So Brighter's own channel-level buffer is cleared. Messages the transport has buffered are not.
  - `src/Paramore.Brighter/CommandProcessor.cs:1497`: `ExecuteWithResiliencePipeline(() => responseChannel.Purge())`.
- **Pull mode has no transport-local buffer.**
  - `GcpPullMessageConsumer.ReceiveAsync`/`Receive` (`:167-220`, `:257-296`) make one `Pull` RPC per call and
    return that batch directly.
  - Any batch left over sits only in `Channel._queue`, which the core `Purge` already clears.
- **Earlier notes**
  - `bugfixes/0026-gcp-stream-stopped-client-cache/bugfix.md:251-253` (Scope Notes item 6) flagged the
    `SeekRequest` with no `Subscription` as unverified.
  - `bugfixes/0023-gcp-stream-pump-requeue-hang/bugfix.md:203` says: "Nothing else drains the channel …
    `Purge` only Seeks on the server".
  - The same file at `:253` lists "`Purge` leaves locally buffered messages in place".
  - At `:254` it notes that Pull has no callback or TCS, so it has no equivalent defect.

## Root-Cause Hypothesis
**H1: Problem 1, the Seek is rejected because it names no subscription (Pull and Stream).**
- All four purge paths build `new SeekRequest { Time = … }` and never set
  `Subscription`/`SubscriptionAsSubscriptionName`.
- In proto3 the C# field defaults to `""`, and the client library does not check it.
- The server should treat the empty resource name as invalid, return `InvalidArgument` (or `NotFound`), and
  `Purge` would log `PurgeError` and rethrow.
- The connection's client factories add no default subscription, so nothing upstream fills the gap.
- The issue's suggested fix is to set `SubscriptionAsSubscriptionName = subscriptionName` on the request.
  **UNVERIFIED — to be proven or refuted in /bugfix:confirm.**
- **Falsifiable by:**
  - Calling `GcpPullMessageConsumer.Purge()` and `PurgeAsync()`, and the same on
    `GcpPubSubStreamMessageConsumer`, against both the emulator and real Pub/Sub.
  - Capturing the exception (type, `StatusCode`, detail) and the `PurgeError` log.
  - H1 is proven if real Pub/Sub throws.
  - H1 is refuted if the call succeeds and a published backlog is actually removed. Check with a `Pull` straight
    after the purge.
  - Record the emulator's result separately. It may accept the call or behave differently from real Pub/Sub.
  - Control experiment: the same Seek with `Subscription` set. It should succeed and clear the backlog on both.

**H2: Problem 2, a successful Seek does not drain messages already buffered (Stream only).**
- A Seek changes only the server-side ack state of the subscription.
- Messages that `SubscriberClient` has already given to `BrighterStreamHandler.HandleMessage` sit in the local
  channel. Their TCS is unset and their lease keeps being extended.
- `ReceiveAsync` reads them without checking whether a purge has happened. So a `Receive` after `Purge` returns
  messages published before the purge.
- Pull mode is not affected: it has no local buffer, and `Channel._queue` is cleared by core.
- The issue implies draining the buffer and settling each drained message. Whether to Ack (treat as purged) or
  Nack is open. **UNVERIFIED — to be proven or refuted in /bugfix:confirm.**
- **Falsifiable by:**
  1. Use a stream subscription with `BufferSize >= 2`.
  2. Publish several messages and wait until they are buffered (call `Receive` once so the client starts).
  3. Call `Purge` with a Seek that works. Either H1's control version, or the emulator if it accepts the
     current request.
  4. Call `Receive` again.
  - H2 is proven if a pre-purge message is returned.
  - H2 is refuted if `Receive` returns only `MT_NONE`. For example, the client may cancel outstanding handler
    tokens after a Seek, which would make `CanProcess` false.
  - Run on the emulator and on real Pub/Sub, because lease and stream-reset behaviour after a Seek may differ.
  - Also record whether the buffered messages are redelivered later after they are settled (Ack vs Nack) once the
    Seek has happened.

**Open questions for Confirm**
- What do real Pub/Sub and the emulator each return for a `SeekRequest` with no subscription? (Status code and
  message.)
- After a successful seek-to-time, does `SubscriberClient` cancel or reset in-flight handler callbacks, or does it
  leave buffered messages untouched?
- When buffered messages are drained, which reply is right?
  - Ack matches "purged": the server already counts them as acked after the Seek.
  - Nack risks redelivery if the Seek did not cover them.
- Is a pre-purge message redelivered at all after a successful Seek? GCP describes Seek as eventually
  consistent, so a short window of redelivery may be expected.
- The Seek time is `now + 1 minute`. Any message published in the next minute is also marked acknowledged.
  - Is this intended? It also matters for `CommandProcessor.Call`: it purges the reply channel and then sends the
    request, so a fast reply could be purged.
  - Flag this as a scope-note candidate; do not fix it here.
- Draining must happen on the shared `GcpStreamConsumer`, which serves every performer on the subscription. Is that
  acceptable for a purge? (It probably is, since the Seek is subscription-wide anyway.)
- Does seek-to-time need any subscription setting, such as `retain_acked_messages`, for a forward seek? (It
  should not.)

## Confirmed Root Cause
**Verdict: H1 CONFIRMED, H2 CONFIRMED.** Both proven by code trace and by an executable probe on the emulator and on
real Pub/Sub.

- **H1 (Pull and Stream): `Purge` always fails.** All four purge paths build `new SeekRequest { Time = … }` and never
  set `Subscription`. The client library passes the empty name through: it does no validation, and it sends the
  routing header `x-goog-request-params: subscription=`. The server rejects the request with `InvalidArgument`, and
  `Purge`/`PurgeAsync` log `PurgeError` and rethrow. So GCP `Purge` has never worked, on either mode. The backlog is
  untouched.
- **H2 (Stream only): a working Seek still leaves the local buffer.** `GcpStreamConsumer` keeps a private unbounded
  `Channel<GcpStreamMessage>`. A message in it is unsettled: its TCS is unset, and `SubscriberClient` keeps extending
  its lease. Nothing on the purge path touches the channel:
  - `Purge` only sends a server-side Seek, on a different client object.
  - `Channel.Purge` and `ChannelAsync.PurgeAsync` clear only Brighter's own `_queue`.

  The Seek cannot cancel the handler's token, so `CanProcess` stays true. `ReceiveAsync` returns every buffered
  pre-purge message, up to the flow-control cap of `BufferSize * NoOfPerformers`.

## Evidence
- [x] **Code-trace** (Plan sub-agent, verified by the main agent)
  - **H1:**
    - Request sites: `GcpPubSubStreamMessageConsumer.cs:190-193`, `:219-221`; `GcpPullMessageConsumer.cs:117-118`,
      `:142-144`.
    - Both consumers hold `subscriptionName` (`GcpPubSubStreamMessageConsumer.cs:22`, `GcpPullMessageConsumer.cs:19`).
      The Pull consumer already uses it for `PullRequest` (`:182`, `:268`) and `ModifyAckDeadlineRequest` (`:518`).
    - The connection's client builders set no subscription (`GcpMessagingGatewayConnection.cs:117-165`).
    - Library: Google.Cloud.PubSub.V1 3.36.0 (`Directory.Packages.props:61`).
      - The XML docs mark `SeekRequest.Subscription` "Required. The subscription to affect."
      - In the IL, `SubscriberServiceApiClientImpl.Seek` only calls `_callSeek.Sync`, which is wired with
        `WithGoogleRequestParam("subscription", r => r.Subscription)`. Gax maps an empty value to `subscription=`
        and does not throw.
  - **H2:**
    - `HandleMessage` writes to the channel and awaits the TCS (`GcpStreamConsumer.cs:107-113`).
    - The channel is private, and only `Reader` is exposed (`:16-21`, `:26`).
    - `ReceiveAsync` returns any message with `CanProcess` true (`GcpPubSubStreamMessageConsumer.cs:258-266`).
      `CanProcess => !_tcs.Task.IsCanceled` (`GcpStreamConsumer.cs:184`), and only the client's hard-stop token can
      cancel it (`:110`).
    - Purge (`GcpPubSubStreamMessageConsumer.cs:182-230`) never touches `consumer`. `Channel.cs:139` and
      `ChannelAsync.cs:143` only replace `_queue`.
- [x] **Red repro: an executable probe, run 2026-10-04 and NOT committed** (copy in the session-9 scratchpad as
  `Probe0047Tests.cs`).
  - Setup:
    - Pull: publish 2 messages.
    - Stream: `BufferSize = 3`; publish 3 messages; wait 8 s so the client buffers them.
    - Then call Brighter's `channel.Purge()`, then a control Seek with `SubscriptionAsSubscriptionName` set, then
      `Receive`.

  | Case | Emulator | Real Pub/Sub (`brighter-gcp-diag-51720`) |
  |---|---|---|
  | Pull, Brighter `Purge` | throws `InvalidArgument`: `Invalid [subscriptions] name: (name=)` | throws `InvalidArgument`: `Invalid resource name given (name=)` |
  | Pull, `Receive` after Brighter `Purge` | a pre-purge message | a pre-purge message |
  | Pull, control Seek, then `Receive` | `MT_NONE` (backlog cleared) | `MT_NONE` (backlog cleared) |
  | Stream, Brighter `Purge` | throws `InvalidArgument` (as above) | throws `InvalidArgument` (as above) |
  | Stream, control Seek, then `Receive` ×4 | **all 3 pre-purge messages, then `MT_NONE`** | **all 3 pre-purge messages, then `MT_NONE`** |
  | Pull, a message published straight after a control Seek to `now + 1 min` | arrives | arrives |

  - A first probe run built every message with one `DefaultMessageBuilder`, which reuses one message id. That run
    was discarded, and the probe was re-run with a fresh id per message (the table above).

## Suggested-Fix Assessment
- **H1: CONFIRMED.** Set `SubscriptionAsSubscriptionName = subscriptionName` on all four `SeekRequest`s. That matches
  the Pull consumer's existing pattern, and the control Seek in the probe proves it clears the backlog on both
  servers.
- **H2: PARTIAL.** The direction (drain the local buffer and settle each message) is right, but the issue does not
  say how. The trace adds:
  - **There is no drain hook today.** The channel's writer is private, and the reader is shared by every performer on
    the subscription (`GcpPubSubConsumerFactory.cs:94-105`). Draining belongs on `GcpStreamConsumer`.
  - **Settle drained messages with Ack, not Nack.**
    - After the Seek, the server already counts them as acked, so an Ack agrees with it.
    - A Nack asks for redelivery, which works against the purge (and Seek is eventually consistent).
    - Dropping a message without settling it would keep its flow-control slot and lease for ever, and that stalls the
      subscription (`GcpPubSubStreamMessageConsumer.cs:76-80`).
  - **Order: Seek first, then drain.** If the drain runs first, the client can fetch more pre-purge messages before
    the Seek applies.
  - **Concurrency.** A drain cannot stop another performer's `ReceiveAsync` from taking a pre-purge message while it
    runs, because there is no purge barrier. A plain `TryRead` loop would also Ack messages that arrived after the
    purge started.
    - One option: filter on `PublishTime` against the Seek's timestamp.
    - That is a design choice for the fix, to be settled in `/bugfix:test` and `/bugfix:fix`.
  - Messages a performer has already received but not settled are outside the buffer. They settle through the normal
    Ack/Reject path, which is acceptable.

## Scope Notes
1. **Same defect class: only the four Seeks.** The sub-agent searched every `new *Request` in the GcpPubSub project.
   Every other request names its resource:
   - PullRequest, ModifyAckDeadlineRequest, Acknowledge and ModifyAckDeadline (`GcpPullMessageConsumer.cs:182`,
     `:268`, `:487-518`).
   - Delete calls (`GcpPubSubChannelFactory.cs:107-221`).
   - IAM calls (`GcpPubSubMessageGateway.cs:515-608`).
   - Update calls (`:131`, `:400`).
2. **The `now + 1 min` seek time** (`GcpPubSubStreamMessageConsumer.cs:192`, `:220`; `GcpPullMessageConsumer.cs:118`,
   `:143`).
   - The probe shows that a message published straight after the Seek still arrives, on both servers. A Seek only
     marks messages already retained. So `CommandProcessor.Call` (`CommandProcessor.cs:1497`: purge, then send)
     does not lose a fast reply on the server side.
   - **It does matter for H2's drain.** A drain that filters on `PublishTime < seekTime` would Ack messages published
     up to a minute after the purge, which the server itself would deliver. The drain should use the purge's own
     start time, not the Seek's +1 min.
   - The seek time reads `TimeProvider`, so a fake clock in a test changes the real seek time.
   - Not changed here unless the fix needs it.
3. **`CommandProcessor.Call` on a GCP reply channel cannot get past the purge today.** It purges through a resilience
   pipeline before sending (`CommandProcessor.cs:1491-1497`). With H1, every attempt throws `InvalidArgument`. Fixing
   H1 fixes this; no change is needed in core.
4. **Test doubles hide it.** No GCP test runs `Purge` against a broker. The only references are delegating doubles
   (`…/Pull/When_a_gcp_pull_message_is_redelivered_should_present_increasing_delivery_count.cs:266`,
   `…/Stream/When_gcp_budget_is_one_zero_or_below_minus_one_should_reject_on_first_deferral.cs:185`). The regression
   tests should follow the `When_queue_is_purged` precedent (MQTT, MSSQL, PostgreSQL) and cover Pull and Stream, sync
   and async.
5. **Cross-transport parity, at a glance (not in scope, not verified).**
   - MQTT drains its local channel with `TryRead` (`MQTTMessageConsumer.cs:249-254`). That is the precedent for H2.
   - **RMQ.Async** purges with `QueuePurgeAsync` (`RmqMessageConsumer.cs:216`). RabbitMQ does not purge unacked
     deliveries, and `PullConsumer.cs:43` keeps a local `ConcurrentQueue` that `Purge` does not drain. This may be the
     same class of defect. It is worth a separate look, not this fix.
   - Kafka seeks to End, and librdkafka clears its fetch queue, so it is probably fine. RocketMQ drains by
     receive-and-Ack.
6. **The bound on H2's leak.** At most `BufferSize * NoOfPerformers` pre-purge messages leak back after one purge
   (`GcpPubSubConsumerFactory.cs:121-124`, `:139-141`).

## Regression Test
**Behaviour 1: purging a Pull channel clears the published messages (H1, Pull).** Approved 2026-10-04.
- `tests/Paramore.Brighter.Gcp.Tests/MessagingGateway/Pull/When_a_gcp_pull_channel_is_purged_should_receive_no_message_published_before_the_purge.cs`
  (sync, `GcpPullPurgeTests`)
- `…/When_a_gcp_pull_channel_is_purged_should_receive_no_message_published_before_the_purge_async.cs`
  (async, `GcpPullPurgeAsyncTests`)
- Publish two messages (a fresh id each), `Purge`, `Receive` with a 10 s window, and assert `MT_NONE`.
- RED 0/2 on both servers. `Purge` threw `RpcException InvalidArgument`:
  - emulator: `Invalid [subscriptions] name: (name=)`
  - real Pub/Sub: `Invalid resource name given (name=)`
- GREEN after the fix: 2/2 in each of 5 emulator runs and 5 real Pub/Sub runs. So far there's no sign of the
  Seek's eventual consistency, with no delay between `Purge` and `Receive`.

## Fix
_(left blank — filled by /bugfix:fix)_
