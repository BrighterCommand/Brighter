# Bugfix: GCP Pub/Sub per-subscription/per-publication client hooks replace the connection hook instead of composing

**Linked Issue**: #4516
**Status**: Verified

## Symptom
**What happens now:**
- **Stream subscriber.** When a `GcpPubSubSubscription` sets `streamingConfiguration`, the connection-level
  `GcpMessagingGatewayConnection.StreamConfiguration` hook never runs for that subscription's streaming `SubscriberClient`.
- **Publisher.** When a `GcpPublication` sets `PublisherClientConfiguration`, `GcpMessagingGatewayConnection.PublisherConfiguration`
  never runs for that publication's `PublisherClient`.
- **Consequence.** Any connection-wide setting (`EmulatorDetection`, `Endpoint`, `ChannelCredentials`, …) is silently dropped.
  On the emulator, the client goes to production Pub/Sub and fails with `Unauthenticated`.

**Related producer trap:** Brighter sets `builder.Settings = new PublisherClient.Settings { EnableMessageOrdering = … }` *before* the
hook runs. So a hook (connection or publication, whichever wins) that assigns a new `Settings` silently disables ordering for a
publication that asked for it.

**What should happen:**
1. The connection hook runs first.
2. Then the subscription/publication hook runs; the more specific one wins where they overlap.
3. Brighter-owned settings are applied after both hooks:
   - `EnableMessageOrdering` on the publisher;
   - the flow-control cap on the stream subscriber.

**Repro (from the issue):**
1. Set the connection's `StreamConfiguration = b => b.EmulatorDetection = EmulatorDetection.EmulatorOrProduction` and run on the
   emulator.
2. Use a Stream-mode `GcpPubSubSubscription` with
   `streamingConfiguration: b => b.Settings = new SubscriberClient.Settings { AckDeadline = TimeSpan.FromSeconds(10) }`.
3. Receive. The result is `Unauthenticated`, because the client targets production.
4. The same happens for the producer with `PublisherConfiguration` and `PublisherClientConfiguration`.

**Current workaround:** repeat every connection setting in each per-subscription hook. The ADR 0077 lease-lapse tests do exactly
this.

**Doc defect:** the XML doc on `GcpMessagingGatewayConnection.StreamConfiguration` says it is "used for pull mode message
consumption". It actually configures the streaming `SubscriberClient`; Pull mode uses `SubscriberServiceApiClient`.

## Suspected Location
> Line numbers are on `fix/gcp-hook-composition` (off master `d3fb29fa1`). PR #4515 (bugfix 0026) refactors
> `GcpPubSubConsumerFactory` into `CreateStreamConsumer` plus an evict-and-replace loop. When #4515 merges, expect a
> conflict there and re-verify the lines.

**Stream subscriber (H1):**
- `src/Paramore.Brighter.MessagingGateway.GcpPubSub/GcpPubSubConsumerFactory.cs:96-99`:
  `s_consumers.GetOrAdd(... CreateSubscriberClient(..., sub.StreamingConfiguration ?? _connection.StreamConfiguration))`.
- `GcpPubSubConsumerFactory.cs:116-134` (`CreateSubscriberClient`), in this order:
  1. Builder created with `SubscriptionName` and `Credential` (L120-124).
  2. The single selected hook runs: `configure?.Invoke(builder)` (L126).
  3. `Settings ??= new()` (L128).
  4. `FlowControlSettings` is set (L129-131).
  5. `Build()` (L133).

  The flow-control cap is already applied after the hook; only the hook selection is wrong here.
- `GcpPubSubConsumerFactory.cs:62`: the `s_consumers` cache.
  - It is keyed on `GcpPubSubSubscription` by reference equality; there is no `Equals`/`GetHashCode` override.
  - The hook is consulted only on the first `GetOrAdd` for a subscription instance.
  - So a test needs a fresh subscription instance.

**Publisher (H2, H3):**
- `src/Paramore.Brighter.MessagingGateway.GcpPubSub/GcpPubSubMessageProducerFactory.cs:75-77`:
  `CreatePublisherClient(..., publication.PublisherClientConfiguration ?? _connection.PublisherConfiguration)`.
- `GcpPubSubMessageProducerFactory.cs:90-106` (`CreatePublisherClient`), in this order:
  1. The builder sets `Settings = new PublisherClient.Settings { EnableMessageOrdering = … }` (L94-102).
  2. The hook runs (L104).
  3. `BuildAsync()` (L105).

  Nothing re-applies `EnableMessageOrdering` after the hook.

**Configuration surface:**
- `GcpMessagingGatewayConnection.cs:34`: `PublisherConfiguration`.
- `GcpMessagingGatewayConnection.cs:43-46`: `StreamConfiguration`. Its doc wrongly says "pull mode".
- `GcpPubSubSubscription.cs:112-115`: `StreamingConfiguration`, assigned at L184 from the ctor parameter at L162.
- `GcpPublication.cs:24-28`: `PublisherClientConfiguration`. `EnableMessageOrdering` is at L21.

**Additional site (H4):**
- `src/Paramore.Brighter.MessagingGateway.GcpPubSub/GcpRejectionRouter.cs:250-268` builds DLQ and invalid-message producers via
  `new GcpPubSubMessageProducerFactory(_connection, [publication])`.
- `BuildPublication` (L274-281) sets `EnableMessageOrdering = true` and no `PublisherClientConfiguration`. So the connection hook
  runs, and H2 does not apply.
- H3 does apply: a connection `PublisherConfiguration` that replaces `Settings` disables the ordering the router forces on. Fixing
  this in the shared factory covers it.

**Checked and unaffected:**
- `GcpPullMessageConsumer.cs` and `GcpPubSubStreamMessageConsumer.cs` (Seek/Ack/Nack) use `SubscriberServiceApiClient`, which is
  configured by `SubscriptionManagerConfiguration`. That hook has no per-subscription override.
- `TopicManagerConfiguration` and `ProjectsClientConfiguration` have no per-entity override.
- The DLQ subscription created in `GcpPubSubMessageGateway.cs:230-238` is management-only.

**Workaround in use:**
- `docs/adr/0077-delivery-count-contract.md:268-270`.
- The per-subscription hooks repeat `EmulatorDetection` and replace `Settings` in:
  - `tests/Paramore.Brighter.Gcp.Tests/MessagingGateway/Stream/When_a_gcp_stream_lease_lapses_should_present_greater_delivery_count.cs:95-110`
    and L208;
  - the `_async` variant;
  - `GcpStreamLeaseLapseBufferMeasurementTests.cs:168-176`.
- The test comment's citation `GcpPubSubConsumerFactory.cs:110-121` is stale.

## Root-Cause Hypothesis
- **H1, stream subscriber replaces instead of composing.** `GcpPubSubConsumerFactory.cs:99` uses `??`. With a non-null subscription
  hook, the connection hook is never invoked at L126.
  - *Falsifiable:* set a connection hook with `EmulatorDetection` and a subscription hook that only sets `Settings`. On the emulator
    this should fail with `Unauthenticated` or receive nothing. A spy connection hook should record 0 invocations.
- **H2, publisher replaces instead of composing.** `GcpPubSubMessageProducerFactory.cs:77` uses `??`, with the same effect at L104.
  - *Falsifiable:* the same arrangement for publish. A spy connection hook should record 0 invocations.
- **H3, publisher ordering is overwritten.** `EnableMessageOrdering` is set at `GcpPubSubMessageProducerFactory.cs:98-101`, before
  the hook at L104, and is never re-applied.
  - *Falsifiable:* on an ordered publication, a hook that assigns a new `PublisherClient.Settings` should yield a client that
    rejects or ignores ordering keys.
  - The stream subscriber has no equivalent: `FlowControlSettings` is already applied after the hook.
- **H4, rejection-router variant of H3.** `GcpRejectionRouter.BuildPublication` forces ordering, and a connection hook that replaces
  `Settings` turns it off. The root cause is the same as H3.
- **Doc defect.** `GcpMessagingGatewayConnection.cs:43` says "pull mode".

**Suggested fix, from the issue and the user-approved scope. UNVERIFIED — to be proven or refuted in /bugfix:confirm.**
- Compose the hooks for both builders: run the connection hook, then the subscription/publication hook.
- Then apply the Brighter-owned settings: `Settings ??= new()`, then `EnableMessageOrdering` (publisher) or `FlowControlSettings`
  (subscriber).
- Fix the "pull mode" XML doc.

**Expected effect on existing tests (to confirm):**
- The lease-lapse tests set both hooks. Under composition, the connection hook runs first and sets only `EmulatorDetection`; the
  subscription hook then sets the same value and replaces `Settings`. So their behaviour should not change.
- Their duplicated `EmulatorDetection` lines and "replaces" comments would become redundant.

**How a regression test could observe the bug:**
- **No internal seam.** There is no `InternalsVisibleTo`, the builder methods are private, and built clients aren't exposed.
- **A spy-hook seam may work without the emulator.** With `MakeChannels = OnMissingChannel.Assume`, the ensure-exists calls return
  before any server call, so the factories reach the builder and run the hooks.
  - Recording lambdas can then assert that both hooks ran, in order, and that the specific hook sees state the connection hook set.
  - Caveats: `Build`/`BuildAsync` may need an endpoint or credentials, and stream start connects in the background.
  - This shows hook order only, not the final `EnableMessageOrdering` or flow-control values.
- **Emulator behavioural tests:**
  - **H1:** the connection hook sets `EmulatorDetection` and the subscription hook does not; a Stream receive succeeds.
  - **H2:** the same arrangement for publish.
  - **H3/H4:** on an ordered publication, a hook replaces `Settings`, yet keyed publishes succeed and keep their order.

## Confirmed Root Cause
**Verdict: H1–H4 are all CONFIRMED.** H3 and H4 fail loudly, not silently.

1. **H1/H2: the factories select one hook with `??` instead of composing.**
   - `GcpPubSubConsumerFactory.cs:99` passes `sub.StreamingConfiguration ?? _connection.StreamConfiguration`, which is invoked at L126.
   - `GcpPubSubMessageProducerFactory.cs:77` passes `publication.PublisherClientConfiguration ?? _connection.PublisherConfiguration`,
     which is invoked at L104.
   - With a non-null per-entity hook, the connection hook never runs, so its `EmulatorDetection`, `Endpoint` and
     `ChannelCredentials` are lost. Both builders default `EmulatorDetection` to `None`, so the client targets production.
2. **H3: ordering is lost when a publisher hook replaces `Settings`.**
   - `CreatePublisherClient` sets `Settings = new() { EnableMessageOrdering = … }` before the hook (L94-102) and never re-applies it
     (L104 hook, then L105 build).
   - A hook that assigns a new `Settings` leaves ordering at its default, `false`.
   - Brighter always sets `OrderingKey = message.Header.PartitionKey` (`Parser.cs:312`).
   - The Google client checks ordering first in `PublishAsync`: with ordering off and a non-empty key, it throws
     `InvalidOperationException("Message ordering must be enabled in settings before using OrderingKey")` before any I/O. **So every
     keyed `Send` throws.**
3. **H4: the rejection router hits H3.** `GcpRejectionRouter.BuildPublication` (`GcpRejectionRouter.cs:274-281`) forces ordering on and
   sets no per-publication hook. A connection `PublisherConfiguration` that replaces `Settings` turns ordering off, so the DLQ or
   invalid-message forward of a keyed message throws and the router reports `RoutingOutcome.Failed`.
4. **Doc:** `GcpMessagingGatewayConnection.cs:42-46` says "pull mode"; it is the streaming `SubscriberClient`.

## Evidence
- [x] **Code-trace**
  - **H1:** `GcpPubSubConsumerFactory.cs`, in this order:
    1. `:96-99` selects the hook.
    2. `:120-124` creates the builder; `Settings` is not created and stays null.
    3. `:126` invokes the selected hook.
    4. `:128-131` runs `Settings ??= new()`, then sets `FlowControlSettings`.
    5. `:133` builds.
  - **H2/H3:** `GcpPubSubMessageProducerFactory.cs`:
    1. `:75-77` selects the hook.
    2. `:94-102` pre-creates `Settings` with `EnableMessageOrdering`.
    3. `:104` invokes the hook.
    4. `:105` calls `BuildAsync`.

    `GcpMessageProducer.cs:61-64` sends `Parser.ToPubSubMessage`, and `Parser.cs:312` sets `OrderingKey` from `PartitionKey`.
    `PartitionKey.Empty` is `""`, so only keyed messages throw.
  - Only these two sites in `src` build a `SubscriberClientBuilder` or `PublisherClientBuilder`.
  - **Google.Cloud.PubSub.V1 3.36.0** (`Directory.Packages.props:61`). The main agent re-checked each of these:
    - The `EmulatorDetection` default is `None` ("environment variables are ignored") for both builders (XML L597-604 and
      L11753-11760).
    - The DLL contains the string "Message ordering must be enabled in settings before using OrderingKey". The sub-agent read the
      IL: the check in `PublisherClientImpl.<PublishAsync>` runs before any network I/O.
    - Builder `Settings` is null by default (XML L586-589 and L11742-11745).
  - No `InternalsVisibleTo` exists for the GCP project.
  - Still **assumed**, not run: the exact `Unauthenticated` failure on the emulator. It matches the ADR 0077:268-270 field
    observation.
- [ ] **Red repro:** not run in Confirm. These are the cheapest designs.
  - **H1, offline spy (no container).**
    - Arrange:
      - a fresh Stream subscription with `MakeChannels = Assume`;
      - a connection hook that records "conn";
      - a subscription hook that records "sub" and sets `Endpoint = "localhost:1"` and `ChannelCredentials.Insecure`, so `Build`
        works without ADC;
      - `Credential` set to null.
    - Act: `Create(sub)`.
    - Assert: the calls were `["conn","sub"]`. Today they are `["sub"]`.
    - Assumed: `Build` with `Insecure` doesn't contact the server, and `StartAsync` is fire-and-forget.
  - **H2, offline spy:** the same arrangement on the producer factory, with `MakeChannels = Assume`.
  - **H3:** an ordered publication whose hook replaces `Settings`.
    - Today, a keyed `SendAsync` throws the ordering `InvalidOperationException` immediately. That can be shown offline.
    - The green path needs the emulator, because offline the publish would retry against `localhost:1`. Best covered as an emulator
      test: a keyed `Send` succeeds and the messages arrive in order.
  - **H4:** on the emulator, a connection `PublisherConfiguration` that replaces `Settings`, plus a rejected keyed message.
    - Today: `RoutingOutcome.Failed` and nothing reaches the DLQ.
    - Alternatively, cover it through the shared factory test, since it is the same code path.

## Suggested-Fix Assessment
**PARTIAL.** The direction is right, with these corrections.
1. **Compose the hooks.** Run the connection hook, then the per-entity hook.
   - Keep Brighter's pre-hook fields (`SubscriptionName`, `TopicName`, `Credential`) before the hooks, so hooks can still override
     them.
   - "More specific wins" holds field by field only. A per-entity hook that assigns a new `Settings` replaces whatever the
     connection hook put in `Settings`. Document that.
2. **Publisher `Settings`: keep the pre-hook initialisation**, so hooks that mutate `b.Settings.X` don't hit a null reference. Then
   add a post-hook `Settings ??= new()` and apply ordering.
3. **Subscriber:** flow control is already applied after the hook, so only composition is needed.
   - Optional: pre-create `Settings` before the hooks. Today a hook doing `b.Settings.AckDeadline = …` hits a null reference; this is
     an existing quirk, not a regression.
4. **Optional:** writing Brighter-owned values mutates a `Settings` instance the user assigned, so `Clone()` it before writing.
5. **Decision for the user: "Brighter-owned wins" vs "only force true" for `EnableMessageOrdering`.**
   - Today, a hook can switch ordering on for a publication with `EnableMessageOrdering = false`. That may be how some users send keyed
     messages, because of the Parser coupling below.
   - Always assigning the publication's flag would make those sends throw: a breaking change.

## Scope Notes
**The user's decisions at the Confirm gate (2026-10-04):**
- **Diagnosis:** approved.
- **Ordering:** "only force true". After the hooks, set `Settings.EnableMessageOrdering = true` if `publication.EnableMessageOrdering`.
  Otherwise leave whatever the hook set.
- **Subscriber:** pre-create `SubscriberClient.Settings` before the hooks. This is in scope.
- **Not in scope:** cloning a hook-assigned `Settings`.
- **Parser defect** (item 1): a separate GitHub issue, drafted for the user's approval. It is not part of 0049.

1. **Latent related defect, out of scope:** `Parser.cs:312` always sets `OrderingKey` from `PartitionKey`. On a publication with
   `EnableMessageOrdering = false`, every keyed message throws today. This bears on decision 5. It could be a separate issue.
2. **No other hook-selection sites.**
   - `TopicManagerConfiguration`, `SubscriptionManagerConfiguration` and `ProjectsClientConfiguration` have no per-entity override.
   - The Firestore hook (`FirestoreConfiguration.cs:67`) and the GCS hook (`GcsLuggageOptions.cs:151`) are single hooks.
   - Spanner has no hooks.
3. **Docs to update:**
   - XML docs: `GcpMessagingGatewayConnection.cs:30-34,42-46`, `GcpPubSubSubscription.cs:111-115` and `GcpPublication.cs:23-28`.
   - `docs/adr/0077-delivery-count-contract.md:268-270`, the workaround note. Also `:241`, whose line citation is stale.
   - A `release_notes.md` entry, because this changes behaviour.
4. **Tests that encode the workaround:**
   - the lease-lapse tests, sync and async;
   - `GcpStreamLeaseLapseBufferMeasurementTests.cs:168-176`.

   They stay green under composition. Their "replaces the connection's" comments become false and should be reworded.
5. **Conflict risk:** PR #4515 (bugfix 0026) restructures `GcpPubSubConsumerFactory` around L96-99.

## Regression Test
All four are RED, and each was approved by the user (2026-10-04). They are on `fix/gcp-hook-composition`, which was
fast-forwarded to master `4345579e9` after #4515 merged. On that base, H1 is at `GcpPubSubConsumerFactory.cs:124` and the
hook is invoked at `:136`.

Paths are under `tests/Paramore.Brighter.Gcp.Tests/MessagingGateway/`.

1. **H1 (offline):** `When_a_gcp_stream_subscription_sets_its_own_streaming_configuration_should_run_the_connection_configuration_first.cs`
   - Spy hooks; `MakeChannels = Assume`; the subscription hook points at `localhost:1` with `Insecure` credentials.
   - RED: `Expected ["connection","subscription"]`, `Actual ["subscription"]`.
2. **H2 (offline):** `When_a_gcp_publication_sets_its_own_publisher_configuration_should_run_the_connection_configuration_first.cs`
   - The same arrangement on `GcpPubSubMessageProducerFactory.CreateAsync`.
   - RED: `Actual ["publication"]`.
3. **H3/H4 (emulator, and real Pub/Sub too):** `PullOrdering/When_a_publisher_configuration_replaces_the_settings_of_an_ordered_publication_should_still_send_in_order_async.cs`
   - The connection `PublisherConfiguration` replaces `Settings` (and sets `EmulatorDetection`). The ordered publication
     has no hook of its own, which isolates H3 from H2; this is H4's shape.
   - Three keyed sends must arrive in order.
   - RED: `InvalidOperationException: Message ordering must be enabled in settings before using OrderingKey` at
     `GcpMessageProducer.cs:64`.
   - Category `GcpPubSubPullOrdering`, so it runs in `gcp-emulator-ci` and `gcp-ci`.
4. **Subscriber `Settings` pre-created (offline):** `When_a_gcp_stream_subscription_configuration_sets_a_builder_setting_should_not_need_to_create_the_settings.cs`
   - The hook sets `builder.Settings.AckDeadline` without creating `Settings` first.
   - RED: `NullReferenceException` in the hook, at `GcpPubSubConsumerFactory.cs:136`.

The offline tests take about 0.1–0.2 s. Building a client against `localhost:1` with `Insecure` doesn't contact a server,
and the stream start is fire-and-forget.

## Fix
A straight fix: the change is local to the two builder methods, so no tidy-first step was needed. All 4 regression
tests are green on the emulator (4/0/0).

- `src/Paramore.Brighter.MessagingGateway.GcpPubSub/GcpPubSubConsumerFactory.cs`
  - `CreateStreamConsumer` passes only `subscription.StreamingConfiguration`.
  - `CreateSubscriberClient` does the following, in order:
    1. Pre-creates `Settings = new SubscriberClient.Settings()`.
    2. Invokes `_connection.StreamConfiguration`.
    3. Invokes the subscription's hook.
    4. Runs `Settings ??= new()` and sets `FlowControlSettings`. This step is unchanged.
- `src/Paramore.Brighter.MessagingGateway.GcpPubSub/GcpPubSubMessageProducerFactory.cs`
  - `CreateAsync` passes only `publication.PublisherClientConfiguration`.
  - `CreatePublisherClient` keeps the pre-hook `Settings { EnableMessageOrdering }`, then does the following, in order:
    1. Invokes `_connection.PublisherConfiguration`.
    2. Invokes the publication's hook.
    3. Runs `Settings ??= new()`.
    4. Sets `EnableMessageOrdering = true` if the publication asked for it ("only force true").
  - This also fixes H4, because `GcpRejectionRouter` uses the same factory.

**Verified (2026-10-04, net10.0):**
- The Release build has 0 errors.
- Emulator CI filter: 194/49/30. This is the baseline plus the 4 new tests, and no test changed outcome by name.
- Emulator Stream: 125/2/25. The 2 failures are 0047's `GcpStreamPurge(Async)Tests`. They are pre-existing and
  environmental:
  - They fail identically on master `4345579e9`.
  - The emulator's `PublishTime` runs ~45–53 ms ahead of the Mac's clock, and 0047's drain rule compares it with the
    host clock. That is a follow-up for 0047, not for 0049.
- Real Pub/Sub `gcp-ci` scope: **165/0/30**. All 4 new tests pass. The only change by name is the known PullOrdering
  delivery-count flake, which failed in the baseline and now passes.

**The `docs:` commit covers:**
- XML docs (Scope Notes 3), including the "pull mode" correction.
- ADR 0077 `:241` and `:268-270`.
- A `release_notes.md` entry.
- Rewording the lease-lapse tests' "replaces the connection's" comments (Scope Notes 4).
