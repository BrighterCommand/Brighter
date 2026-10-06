# Bugfix: GCP Parser sets OrderingKey on unordered publications, so keyed sends throw

**Linked Issue**: #4517
**Status**: Verified

## Symptom
**What happens:** If a message has a non-empty `PartitionKey` and goes through a `GcpPublication` whose
`EnableMessageOrdering` is `false` (the default), `Send`, `SendAsync`, `SendWithDelay(Async)` with no delay, and the
bulk `SendAsync(IAmAMessageBatch)` all throw
`InvalidOperationException: Message ordering must be enabled in settings before using OrderingKey`. The exception
comes from `Google.Cloud.PubSub.V1.PublisherClient.PublishAsync` before any network I/O.

**What should happen:** A partition key on an unordered publication should not make the send fail.

**How a partition key gets onto a message:** the user does not have to set it by hand. `JsonMessageMapper`
(`src/Paramore.Brighter/MessageMappers/JsonMessageMapper.cs:50,53`) and `CloudEventJsonMessageMapper`
(`src/Paramore.Brighter/MessageMappers/CloudEventJsonMessageMapper.cs:55`) copy it from `Context.GetPartitionKey()`.

**Repro:**
1. Create a `GcpPublication` without `EnableMessageOrdering`.
2. Build a message with `PartitionKey = "k"`.
3. Call `producer.Send(message)` or `SendAsync`. It throws.

The Google contract, from `~/.nuget/packages/google.cloud.pubsub.v1/3.36.0/lib/netstandard2.0/Google.Cloud.PubSub.V1.xml:515-519`
(`PublisherClient.Settings.EnableMessageOrdering`): "It is invalid to set `PubsubMessage.OrderingKey` in a message if
this has not been set to `true`." The same XML (around :640, `OrderingKeyState.Normal`) says "The empty ordering-key
(meaning no ordering) is always in this state." So an empty key is treated as no ordering.

## Suspected Location
- `src/Paramore.Brighter.MessagingGateway.GcpPubSub/Parser.cs:308-317`: `public static PubsubMessage ToPubSubMessage(Message message)`.
  Line 312 always sets `OrderingKey = message.Header.PartitionKey`. It takes only the `Message`, so it cannot see the
  publication's ordering setting.
- `src/Paramore.Brighter.MessagingGateway.GcpPubSub/Parser.cs:319-375` (`AddHeaders`): no attribute carries the
  partition key. `HeaderNames.cs:6-86` has no partition-key name either. `OrderingKey` is the only place the partition
  key travels.
- `src/Paramore.Brighter.MessagingGateway.GcpPubSub/Parser.cs:36` (stream path) and `:96` (pull path): on receive,
  `MessageHeader.PartitionKey` is filled from `receivedMessage.Message.OrderingKey`.
- `src/Paramore.Brighter.MessagingGateway.GcpPubSub/GcpMessageProducer.cs:61` (single send; the sync path wraps this
  through `BrighterAsyncContext.Run`) and `:134` (bulk `SendAsync(IAmAMessageBatch)`). These are the only production
  callers of `ToPubSubMessage`. The producer holds `GcpPublication publication` from its primary constructor
  (`GcpMessageProducer.cs:12-14`). The delayed path hands off to the scheduler (`:66-69`) and does not call the Parser
  directly.
- `src/Paramore.Brighter.MessagingGateway.GcpPubSub/GcpPubSubMessageProducerFactory.cs:74-77, 90-118` (`CreatePublisherClient`):
  - It seeds `Settings.EnableMessageOrdering` from the publication (:98-101).
  - It then runs the connection hook and the publication hook (:105-106).
  - It only ever forces ordering on, never off (:110-114).
  - The `builder` is local and is not kept. Only the built `PublisherClient` reaches `GcpMessageProducer` (:80-84).
    So the effective ordering setting is not currently visible to the producer.
- `src/Paramore.Brighter.MessagingGateway.GcpPubSub/GcpPublication.cs:21` (`EnableMessageOrdering`) and `:28-32` (hook
  remarks: "If EnableMessageOrdering is set, Brighter enables message ordering after both have run").
- `src/Paramore.Brighter.MessagingGateway.GcpPubSub/GcpRejectionRouter.cs:274-281`: `BuildPublication` always sets
  `EnableMessageOrdering = true`, so DLQ and invalid-message forwards are not affected.
- `src/Paramore.Brighter/PartitionKey.cs:33` (implicit conversion to `string?`) and `:53` (`Empty => new("")`).
  `MessageHeader.cs:270, 446` default to `PartitionKey.Empty`, so unkeyed messages produce `OrderingKey = ""` and pass
  Google's check.
- Test coupling, which works around the behaviour rather than covering it:
  - All four conformance providers have `RepublishToPubSub`, which turns ordering on whenever the message has a
    partition key. The comment says "A message carrying a partition key is published with an OrderingKey (see Parser)".
    - `tests/Paramore.Brighter.Gcp.Tests/MessagingGateway/GcpPullMessageGatewayProvider.cs:100-109`
    - `GcpStreamMessageGatewayProvider.cs:100-109`
    - `GcpPullOrderingMessageGatewayProvider.cs:104-113`
    - `GcpStreamOrderingMessageGatewayProvider.cs:104-113`
  - `tests/Paramore.Brighter.Gcp.Tests/test-configuration.json`: only `PullOrdering` (:19) and `StreamOrdering` (:42)
    use `FifoMessageBuilder`, which assigns a random key (`FifoMessageBuilder.cs:47`). `Pull` and `Stream` use the
    default builder, where the key is `PartitionKey.Empty` (`DefaultMessageBuilder.cs:60`). So no conformance test
    sends a keyed message through an unordered publication.
  - `tests/Paramore.Brighter.Gcp.Tests/MessagingGateway/PullOrdering/When_a_publisher_configuration_replaces_the_settings_of_an_ordered_publication_should_still_send_in_order_async.cs:76-88`
    (from #4516) covers the ordered publication whose hook replaces the settings.

## Root-Cause Hypothesis
`Parser.ToPubSubMessage` (`Parser.cs:312`) copies `Header.PartitionKey` into `PubsubMessage.OrderingKey` without
checking whether the publisher client has ordering enabled. On a publication with `EnableMessageOrdering = false`,
where no hook turns ordering on, the client's `Settings.EnableMessageOrdering` is `false`. Any non-empty partition key
then makes `PublishAsync` throw before any I/O.

This is falsifiable:
- A producer built by `GcpPubSubMessageProducerFactory` for an unordered publication should throw on `Send` or
  `SendAsync` of a message with `PartitionKey = "k"`. It should not throw for `PartitionKey.Empty`.
- An ordered publication should not throw in either case.

The issue suggests setting `OrderingKey` only when ordering is enabled, or making the behaviour explicit and
documented. **UNVERIFIED — to be proven or refuted in /bugfix:confirm.**

Open design questions for the Confirm gate (no option chosen here):
1. **Which ordering signal decides?** Either `publication.EnableMessageOrdering`, or the effective
   `PublisherClient.Settings.EnableMessageOrdering` after hooks. A user hook can turn ordering on for a publication
   marked unordered. If the decision uses the publication flag alone, those users' messages lose their `OrderingKey`,
   and with it their ordering. Capturing the effective setting would need the factory to record it, because
   `PublisherClient` does not expose its `Settings` (to be confirmed against the 3.36.0 XML/DLL).
2. **What happens to `PartitionKey` on receive?** `OrderingKey` is its only carrier (`Parser.cs:36, :96`). If it is
   not set on unordered publications, consumers receive `PartitionKey.Empty`, which changes current behaviour. Options
   to weigh include adding a Brighter attribute for the partition key on all publications (and reading it with
   fallback to `OrderingKey`, plus adding it to `s_ignoreHeaders`), or accepting the loss on unordered publications.
3. **Should the unordered-with-key case be explicit?** Choices include silently dropping the key, logging, or
   documenting it on `GcpPublication.EnableMessageOrdering`.
4. **API shape.** `ToPubSubMessage` is public static and takes only the `Message`. Giving it the ordering decision
   changes or overloads a public signature. Both callers (`GcpMessageProducer.cs:61, :134`) must stay consistent.
5. **Test harness.** If the Parser stops setting `OrderingKey` for unordered publications, the `RepublishToPubSub`
   workaround in the four providers may no longer be needed, or may need rework. Is a conformance case for "keyed
   message on unordered publication" in scope?

## Confirmed Root Cause
**Verdict: CONFIRMED.**

- `Parser.cs:312` sets `OrderingKey = message.Header.PartitionKey` unconditionally.
- `GcpMessageProducer` publishes that message on a `PublisherClient` whose `Settings.EnableMessageOrdering` comes from
  `publication.EnableMessageOrdering`. That is seeded at `GcpPubSubMessageProducerFactory.cs:98-101` and is `false`
  by default (`GcpPublication.cs:21`).
- Google.Cloud.PubSub.V1 3.36.0 rejects a non-empty `OrderingKey` when ordering is off. It throws
  `InvalidOperationException("Message ordering must be enabled in settings before using OrderingKey")` before any I/O.
- `PartitionKey.Empty` is `""` (`PartitionKey.cs:53`), and an empty key is allowed. So only keyed messages fail.

**Correction to the triage:** `Parser` is `internal static class Parser` (`Parser.cs:10`), and the GCP project has no
`InternalsVisibleTo`. So changing `ToPubSubMessage`'s signature is **not** a public API change. Only
`GcpMessageProducer.cs:61` and `:134` call it.

## Evidence
- [x] **Code-trace**
  1. A key gets onto a message without the user setting it: `JsonMessageMapper.cs:50,53` and
     `CloudEventJsonMessageMapper.cs:55` copy it from `Context.GetPartitionKey()`.
  2. Single send: `GcpMessageProducer.cs:61` calls `Parser.ToPubSubMessage`, then `:64` calls `client.PublishAsync`.
     Sync `Send`/`SendWithDelay` reach this through `BrighterAsyncContext.Run` (`:95`). Bulk send has the same flaw at
     `:134`.
  3. `Parser.cs:308-317` takes only the `Message`. No test calls it (grep of `src`, `tests` and `samples`).
  4. Factory, `GcpPubSubMessageProducerFactory.cs`:
     - `:98-101` seeds ordering from the publication;
     - `:105-106` run the connection hook, then the publication hook;
     - `:110-114` only ever force ordering on;
     - `:116` builds the client.
     The effective `builder.Settings.EnableMessageOrdering` is known at `:110-116`, but it is thrown away. Only the
     client and the publication reach the producer (`:80-84`).
  5. Delayed sends fail too, but at fire time:
     - `GcpMessageProducer.cs:69` hands off to the scheduler.
     - The scheduler's `FireSchedulerMessageHandler` goes to `OutboxProducerMediator.cs:520-523` / `:545-548`, then to
       the registry's `GcpMessageProducer` with the original publication.
     - The same throw happens then, not at the caller.
- **Google 3.36.0, checked offline:**
  - (a) The DLL contains the exception string, as UTF-16LE.
  - (b) An empty key is permitted with ordering off:
    - XML `:515-519`: "It is invalid to set `PubsubMessage.OrderingKey` … if this has not been set to `true`".
    - `:637-641`: "The empty ordering-key (meaning no ordering)…".
    - `:11843`: "Empty string if no ordering key".
  - (c) The client can't tell you whether ordering is on. `PublisherClient` and `PublisherClientImpl` expose only
    `TopicName` and `PendingKeysCount`, and `Settings` is a nested type, not an instance property.
- **Corroboration (not proof):** 0049 read the IL and found the check runs before I/O. 0049 also saw this exception as
  RED on the emulator for a client with ordering off (`bugfixes/0049-gcp-client-hook-composition/bugfix.md:152-155, 181, 268`).
- [ ] **Red repro (not run; written in /bugfix:test).**
  - Arrange: a factory-built producer for an unordered publication.
  - Act: `Send` or `SendAsync` a message with `PartitionKey = "k"`.
  - Assert: no throw. Today it throws at `GcpMessageProducer.cs:64`.
  - RED could run offline (`Assume` plus `localhost:1`/`Insecure`). GREEN can't run offline cleanly, though:
    `PublishAsync` retries against `localhost:1`, and a check on the received side needs a real receive. So use
    **emulator tests in `MessagingGateway/Pull/`**, sync and async: send a keyed message on an unordered publication,
    receive it, assert the id and body, and then check `PartitionKey` as decided in B below.

### Suggested-Fix Assessment
**PARTIAL.** The suggestion is "set `OrderingKey` only when the publication enables ordering".
1. **Users who turn ordering on in a hook.** Gating on `publication.EnableMessageOrdering` silently drops their key,
   and their ordering with it. That is a regression, because the setup works today.
   - The factory knows the effective value (`:110-116`).
   - But the public ctor `GcpMessageProducer(PublisherClient, GcpPublication, …)` is also called with clients built by
     hand: by the test providers (`GcpPullMessageGatewayProvider.cs:112,280,309` and its siblings,
     `Pull/When_a_gcp_channel_is_created_for_an_unenforceable_budget…cs:244`), and probably by users.
2. **Received `PartitionKey`.** `OrderingKey` is the key's only carrier (`Parser.cs:36`, `:96`). `AddHeaders` writes
   no attribute for it, and `HeaderNames.cs` has none. So with the gate alone, a key sent on an unordered publication
   is received as `Empty`.
3. "Document the behaviour" alone leaves the throw in place, including the throw at scheduler fire time.

## Scope Notes
1. The only `PubsubMessage` built in `src` is at `Parser.cs:310-313`, via `GcpMessageProducer.cs:61` and `:134`.
2. **The rejection router is safe.** `GcpRejectionRouter.cs:250-268` builds its producer through the factory with
   `BuildPublication` (`:274-281`), which sets `EnableMessageOrdering = true`.
3. **Scheduler:** there is no GCP-specific scheduler. Every scheduler re-sends through the registry's
   `GcpMessageProducer`, so a fix in the producer covers it.
4. **If we add a partition-key attribute:**
   - add it to `HeaderNames`;
   - add it to `s_ignoreHeaders` (`Parser.cs:12-31`), so it doesn't leak into `Bag` (`:73-77`, `:133-137`) and get
     re-sent by the bag loop (`:368-374`);
   - on receive, read the attribute first and fall back to `OrderingKey`.
5. **Test harness coupling.** All four providers' `RepublishToPubSub` builds a client with ordering on but passes an
   unordered publication. Their comment calls this a workaround for this Parser behaviour:
   - `GcpPullMessageGatewayProvider.cs:100-109`
   - `GcpStreamMessageGatewayProvider.cs:100-109`
   - `GcpPullOrderingMessageGatewayProvider.cs:104-113`
   - `GcpStreamOrderingMessageGatewayProvider.cs:104-113`

   A gate on the publication flag would strip the key on this path.
6. No `GcpPublication` usage in `samples/`, and no GCP `OrderingKey` mention in `docs/`.
7. **Other transports:**
   - AWS V4 copies the key into `MessageGroupId` whenever it is non-empty (`SnsMessagePublisher.cs:76-83`,
     `SqsMessageSender.cs:90-97`), and reads back only `MessageGroupId`. The native field is the only carrier, the
     same as GCP today.
   - ASB sets its native `PartitionKey` and also defines `cloudEvents:partitionkey` (`ASBConstants.cs:26`). That is
     precedent for a separate attribute.
8. **Design decisions for the gate:**
   - **A. Which signal decides whether `OrderingKey` is set?**
     - A1: `publication.EnableMessageOrdering`. The smallest change, but users who enable ordering in a hook lose it,
       and `RepublishToPubSub` loses the key.
     - A2: the factory captures the effective `builder.Settings.EnableMessageOrdering` and passes it through a new
       optional producer ctor parameter, which defaults to the publication flag. This keeps hook-enabled ordering
       working. Producers built by hand must pass the flag.
     - A3: catch the exception and retry without the key. This hides misconfiguration. Not recommended.
   - **B. What carries `PartitionKey` on an unordered publication?**
     - B1: nothing. The key arrives as `Empty`. That is not a regression, because these sends throw today.
     - B2: a Brighter attribute on every publication, read first with a fallback to `OrderingKey`, and added to
       `s_ignoreHeaders`. The key round-trips everywhere, at the cost of one attribute per message.
   - **C. How visible is a dropped key?** Silent, a log, and/or an XML doc note on
     `GcpPublication.EnableMessageOrdering` (`GcpPublication.cs:17-21`).
   - **D. Conformance coverage:** whether to add a "keyed message on an unordered publication" case to the Pull and
     Stream providers.

### The user's decisions at the Confirm gate (2026-10-05)
- **Diagnosis approved.**
- **A2: the effective setting decides.** The factory captures `builder.Settings.EnableMessageOrdering` after the hooks
  run, and passes it to `GcpMessageProducer` through a new optional ctor parameter. That parameter defaults to
  `publication.EnableMessageOrdering`. `OrderingKey` is set only when that flag is on. So ordering turned on by a hook
  keeps working.
- **B2: a Brighter partition-key attribute on every publication.**
  - On receive, read the attribute first and fall back to `OrderingKey`.
  - Add the attribute to `HeaderNames` and `s_ignoreHeaders`.
- **C: an XML doc note** on `GcpPublication.EnableMessageOrdering`: the key is sent as an `OrderingKey` only when
  ordering is on, and it is always carried as an attribute. No logging.
- **D:** The user did not select the emulator Pull tests or the `RepublishToPubSub` rework as extra scope. The
  regression tests and their shape are decided at `/bugfix:test` (✋ per test). The harness is left as it is unless a
  test needs a change.

## Regression Test
Approved by the user on 2026-10-05. These are emulator tests in category `GcpPubSubPull`, collection `Pull`. They
also run in `gcp-emulator-ci` and `gcp-ci`.
- `tests/Paramore.Brighter.Gcp.Tests/MessagingGateway/Pull/When_a_keyed_message_is_sent_through_an_unordered_publication_should_receive_it_with_its_partition_key_async.cs`
- `tests/Paramore.Brighter.Gcp.Tests/MessagingGateway/Pull/When_a_keyed_message_is_sent_through_an_unordered_publication_should_receive_it_with_its_partition_key.cs`

**What each test does:**
- Builds a producer through the factory for an unordered publication, with `MakeChannels = Assume`.
- Sends a message with `PartitionKey("customer-42")`, then receives it.
- Asserts on the id and the `PartitionKey`.

**RED (net10.0, emulator):** both fail at `GcpMessageProducer.cs:64` with
`InvalidOperationException: Message ordering must be enabled in settings before using OrderingKey`.

**Second behaviour (A2), approved by the user on 2026-10-05:**
- `tests/Paramore.Brighter.Gcp.Tests/MessagingGateway/Pull/When_a_publisher_configuration_enables_ordering_on_an_unordered_publication_should_send_the_partition_key_as_the_ordering_key_async.cs`
  - The publication is unordered, and a connection `PublisherConfiguration` sets `Settings.EnableMessageOrdering = true`.
  - The test sends a keyed message, pulls it raw with `SubscriberServiceApiClient`, and asserts
    `OrderingKey == "customer-42"`.
- It was written after the minimal GREEN for the first tests, which gated on `publication.EnableMessageOrdering`. So it
  was a genuine RED: `Expected: "customer-42", Actual: ""`.
- On master (key always set) this setup worked, so the test pins that hook-enabled ordering is not regressed.
- Async only at first. The sync twin was added later; see below.

**Characterisation tests, added after the fix at the user's request (`/bugfix:test` re-run, 2026-10-05).** Each test
was green on arrival. Each one turned RED under a named mutation of production code, which was then reverted. Each
was approved by the user.
- `…/Pull/When_a_message_carries_its_partition_key_only_as_the_ordering_key_should_receive_it_with_that_partition_key_async.cs`
  - A raw `PubsubMessage` with only `OrderingKey = "customer-42"` (an older producer) is received with that
    `PartitionKey`.
  - Mutation: the `ReadPartitionKey` fallback returns `""`. Result: `Expected: customer-42, Actual: ` (empty).
- `…/Pull/When_a_keyed_message_is_received_should_not_copy_the_partition_key_attribute_into_the_bag_async.cs`
  - `Header.Bag` has no `ce-partitionkey` entry.
  - Mutation: `HeaderNames.PartitionKey` removed from `s_ignoreHeaders`. Result: "Header.Bag must not admit the
    partition key attribute".
- `…/Pull/When_a_batch_of_keyed_messages_is_sent_through_an_unordered_publication_should_receive_them_with_their_partition_key_async.cs`
  - A bulk `SendAsync(IAmAMessageBatch)` on an unordered publication delivers both messages with their key.
  - Mutation: the bulk loop passes `true` to `ToPubSubMessage`. Result: `InvalidOperationException: Message ordering
    must be enabled…`.
- `…/Pull/When_a_publisher_configuration_enables_ordering_on_an_unordered_publication_should_send_the_partition_key_as_the_ordering_key.cs`
  (the sync twin)
  - Mutation: the factory passes `publication.EnableMessageOrdering` instead of the effective flag. Result:
    `Expected: "customer-42", Actual: ""`.

All 0050 tests plus 0049's ordered-publication test: **8/8 green** (net10.0, emulator).

## Fix
Uncommitted. Summary: send the partition key as the Pub/Sub ordering key only when the publisher client was built
with ordering enabled, and always carry it in a `ce-partitionkey` attribute, read back with a fallback to `OrderingKey`.

- `src/Paramore.Brighter.MessagingGateway.GcpPubSub/HeaderNames.cs`: new `PartitionKey = "ce-partitionkey"`.
- `src/Paramore.Brighter.MessagingGateway.GcpPubSub/Parser.cs`:
  - `ToPubSubMessage(Message, bool enableMessageOrdering)`. `OrderingKey` is the partition key only when ordering
    is on; otherwise it is `""`.
  - `AddHeaders` writes `ce-partitionkey` when the key is non-empty.
  - Both receive paths use `ReadPartitionKey`: the attribute first, then `OrderingKey` for messages from older
    producers.
  - `HeaderNames.PartitionKey` is added to `s_ignoreHeaders`, so it stays out of `Bag`.
- `src/Paramore.Brighter.MessagingGateway.GcpPubSub/GcpMessageProducer.cs`:
  - new optional ctor parameter `bool? enableMessageOrdering = null`, defaulting to
    `publication.EnableMessageOrdering`;
  - both `ToPubSubMessage` calls pass the effective flag;
  - XML `<param>` docs.
- `src/Paramore.Brighter.MessagingGateway.GcpPubSub/GcpPubSubMessageProducerFactory.cs`: `CreatePublisherClient`
  returns the client plus `builder.Settings.EnableMessageOrdering` after the hooks and Brighter's force-on. That value
  is passed to the producer.

**Targeted run (net10.0, emulator):** 4/4 pass. That is the 3 new tests plus 0049's
`…replaces_the_settings_of_an_ordered_publication…`.

**Still to do in the `docs:` commit:**
- the XML doc note on `GcpPublication.EnableMessageOrdering` (C);
- a release note;
- this record.

**Docs (for the `docs:` commit):**
- `<remarks>` on `GcpPublication.EnableMessageOrdering`;
- a `release_notes.md` entry, "GCP Pub/Sub: keyed messages can be sent through an unordered publication (#4517)".

### Verify (2026-10-05, net10.0; TRXs in the session scratchpad `e941ccb2-…/scratchpad/`)
Compared with the 0049 baselines by test name (`cmp.py`).

| Run | 0049 baseline | 0050 | Difference |
|---|---|---|---|
| Emulator, CI filter | 194/49/30 | **201/49/30** | the 7 new tests, all passing; no outcome changed. The 49 are Firestore/GCS tests, which need real GCP |
| Emulator, Stream | 125/2/25 | **125/2/25** | identical. The 2 are the pre-existing clock-skew `GcpStreamPurge(Async)Tests` |
| Real Pub/Sub (`brighter-gcp-diag-51720`), the `gcp-ci` messaging scope | 165/0/30 | **171/1/30** | +6: the 7 new tests, minus the 1 failure below. The 1 failure is a transient flake |

The one real-GCP failure was
`GcpPullConsumerRejectRoutingAsyncTests.When_rejecting_async_with_unacceptable_on_pull_and_no_invalid_key_should_fall_back_to_dlq`.
- It hit gRPC `DeadlineExceeded` in test **cleanup** (`GcpPubSubChannelFactory.DeleteTopicAsync`, called from the
  provider's `CleanUpAsync`), not in the code under test.
- This is the known transient class of failure on real GCP. **It passed when re-run alone (1/1).**
- No test that goes through the unchanged `RepublishToPubSub` regressed.

**Not changed:** the test providers' `RepublishToPubSub`. They pass an unordered publication with a client that has
ordering on, and no flag. So scheduled re-publishes now carry the key as the attribute only, not as `OrderingKey`.
Verify must show whether any conformance test depends on that.
