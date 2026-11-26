# Tasks: Outbox notification channel and message max age

**Specification:** `specs/0041-outbox-failure-channel/requirements.md` (issue #4489)
**Design:** `docs/adr/0093-outbox-failure-channel.md` (Accepted)

**Ordering constraint.**
- Dependencies come first. Each STRUCTURAL task comes before the slice that needs it, and the three STRUCTURAL tasks come before all behaviour, as the ADR's Implementation Approach asks.
- The registry comes before the mediator, and the mediator's raise sites come before the channel's delivery rules.
- The max age on the sweep comes before the stores. The stores come before the archiver, and the archiver before DI. Documentation comes last.

**What a task is.**
- Each TEST + IMPLEMENT task is one slice of behaviour, and each slice holds more than one test.
- Each test in a slice is written and seen failing one at a time, through its own `/tdd:test-first`. Only then is the production code for that test written.
- The test files of a slice are listed in the order they are written. Each one fails before its own production code exists.
- A few slice tests may be green on arrival, because an earlier test's code in the same slice already covers them. Each such test is named in its slice (tasks 5, 7, 17, 19, 20, 21 and 31), is pinned by a named RED mutation as in the CHARACTERISE rules below, and is committed with its slice. `.agent_instructions/testing.md` gives a `test:`-only commit to a test that earlier work already makes green, and this list reads "alone" there as "without production code", as in the CHARACTERISE tasks. A slice test is made green by code of the same slice, so it goes in that slice's commit, whose message names the test's mutation.
- Generated store tests are the other exception. A template cannot be written through `/tdd:test-first`. Its generated tests are seen failing on every store in the GENERATE task, and each store slice then makes them pass. Until a store's new generated max-age tests pass, every full-suite run leaves them out, because they are known to fail. The run filters them by name, with `--filter "FullyQualifiedName!~It_Should_Exclude_Expired_Messages&FullyQualifiedName!~It_Should_Return_Only_A_Topics_Expired_Messages"` on the projects of the stores not yet done. `Paramore.Brighter.Gcp.Tests` holds Spanner and Firestore, so once Spanner is done and until Firestore is, it runs with `--filter "(FullyQualifiedName!~It_Should_Exclude_Expired_Messages|FullyQualifiedName!~Outbox.Firestore)&(FullyQualifiedName!~It_Should_Return_Only_A_Topics_Expired_Messages|FullyQualifiedName!~Outbox.Firestore)"`. Task 25 counts each of its four dialects as done once that dialect's tests pass. Every other test must pass.

**Points this list follows from ADR 0093-outbox-failure-channel.** Q-6 and Q-8 are still marked Open there. The list follows the ADR's reading of both.
- Only three failure kinds raise: `SendFailed`, `ConfirmationFailed` and `MarkDispatchedFailed` (Q-6). `Expired` comes only from the archiver.
- A `Call`'s confirmation notifies like any other (Q-8).
- A replayed causation message keeps its `MessageHeader.TimeStamp`.
- No `ValidatePipelines()` finding is added. An invalid option fails at construction.

**Standing obligations.**
- Test only public exports. No `InternalsVisibleTo`.
- One test class per file. Test names follow `When_..._should_...`.
- Test doubles go in the `TestDoubles` folder beside the slice that first uses them.
- Reuse what already exists:
  - `FakeTimeProvider` (Microsoft.Extensions.TimeProvider.Testing);
  - the `CapturingLoggerProvider`/`CapturingLogger`/`CapturedLogEntry` family in `tests/Paramore.Brighter.Core.Tests/Observability/StaleMessages/TestDoubles`;
  - `SyncOnlyOutboxWrapper` and `AsyncOnlyOutboxWrapper` in `tests/Paramore.Brighter.Core.Tests/Archiving/TestDoubles`;
  - the existing producer and outbox doubles named in each task.
- Every Core test that captures log entries joins `[Collection(StaleMessageLoggingCollection.Name)]`, because `ApplicationLogging.LoggerFactory` is process-wide. `Paramore.Brighter.Extensions.AspNetCore.Tests` already turns off test parallelisation for the whole assembly.
- A reader in a test always waits with a bounded timeout (5 seconds unless a criterion sets another).
- No generated file is written or edited by hand (`.agent_instructions/generated_tests.md`).

**CHARACTERISE rules.** Each phase that has born-green tests ends in one CHARACTERISE task that holds them, except the slice tests named under "What a task is", numbered `N.1`, `N.2` and so on. Each of those tests is known in advance to pass on its first run. For each test, in order:
1. Write the test through its own `/tdd:test-first` and run it. It passes.
2. Apply the named temporary RED mutation to production code. Confirm the test fails on the assertion the test names.
3. Revert the mutation, and stop at the test's own approval gate.
4. Run the full suite.

An item that lists several test files runs these steps once per file: one `/tdd:test-first`, one mutation and one gate each.

When every test in the task has been through these steps, commit the task once, with `test:` only.

A CHARACTERISE test is never rewritten to fail, and never marked "already complete". If a test fails on its first run, it has found a gap: write the production code for it, as in a slice.

**Task labels.**
- `TEST + IMPLEMENT`: a slice of behaviour.
- `STRUCTURAL`: a `refactor:` change with no change of behaviour, kept green by the existing suite.
- `CHARACTERISE`: the born-green tests of a phase, committed together as one `test:`.
- `GENERATE`: generator templates plus the regenerated tests.
- `PROJECT`: work outside the code that a later task needs.
- `DOC`: documentation.

**Left to code review.** These ADR points have no test of their own:
- the `ArgumentNullException` of the registry constructor and of the DI step for a null argument, which are one-line guard clauses;
- the NULL-safe expiry fragment of each relational dialect. A message header always carries a topic and a timestamp, so only a row written outside Brighter can hold a null, and the generated suites write rows only through the outbox's `Add`;
- a container without `IServiceProviderIsService`, which counts as having no archiver. Microsoft's container always provides the service, and the DI tests use only that container.

The rule that `Attach` is atomic is tested in tasks 8 and 17. The rule that an unattached registry still warns about drops, with `TimeProvider.System`, is tested in task 31.

**Paths.**
- New Core tests go in `tests/Paramore.Brighter.Core.Tests/OutboxNotifications` (raising and delivery), `tests/Paramore.Brighter.Core.Tests/OutboxMaxAge` (sweep and `InMemoryOutbox` queries), and the existing `tests/Paramore.Brighter.Core.Tests/Archiving`.
- New DI tests go in `tests/Paramore.Brighter.Extensions.AspNetCore.Tests/OutboxNotifications`.
- The hand-written test of task 24 goes in `tests/Paramore.Brighter.Extensions.Tests/Outbox`.
- Generated store tests are cited by their parent folder. They land in the folders where each store's generated outbox tests already live, for example `Sqlite.Tests/Outbox/Binary/Generated`, `DynamoDB.Tests/Outbox/Generated` and `Gcp.Tests/Outbox/Firestore/Generated`.

---

## Phase 1: Structural preparation
ADR: Implementation Approach steps 1-3

- [ ] **STRUCTURAL: 1 — Move the producer lookup topic onto the message**
  - Depends on: none
  - Needed by: 31 (archiver routing of `Expired`)
  - Move the private static `GetProducerLookupTopic` in `OutboxProducerMediator<TMessage, TTransaction>` to an internal `Message.ProducerLookupTopic()`. Every call site in the mediator then calls the message's method.
  - No new test. The existing dispatch tests stay green. Commit as `refactor:`.

- [ ] **STRUCTURAL: 2 — Let the resilience helpers return the exception they caught**
  - Depends on: none
  - Needed by: 7 and every raise-site slice
  - Move the bodies of `ExecuteWithResiliencePipeline` and `ExecuteWithResiliencePipelineAsync` into new private `TryExecuteWithResiliencePipeline` and `TryExecuteWithResiliencePipelineAsync`. Each returns `Exception?`: null on success, the caught exception on failure.
  - The bool helpers stay as wrappers that return `exception is null`. The async wrapper awaits with `ConfigureAwait(continueOnCapturedContext)`.
  - The `GetPipeline` lookup stays outside the helper's `try`, as it is today.
  - No new test. Commit as `refactor:`.

- [ ] **STRUCTURAL: 3 — Extract the archiver's dispatched pass**
  - Depends on: none
  - Needed by: 30
  - In `OutboxArchiver<TMessage, TTransaction>`, extract the dispatched pass of `Archive` and `ArchiveAsync` into private methods. The early `return` on an empty page in `Archive` then ends only that pass.
  - The existing tests in `tests/Paramore.Brighter.Core.Tests/Archiving` stay green. Commit as `refactor:`.

## Phase 2: Options and registry
ADR: step 4; Key Components `OutboxNotificationKind`, `OutboxNotification`, `OutboxNotificationOptions`, `IAmAnOutboxNotificationRegistry`, `OutboxNotificationRegistry`, `IAmAnOutboxNotificationChannel`, `OutboxNotificationChannel`

- [ ] **TEST + IMPLEMENT: 4 — Notification options reject an out-of-range capacity and an empty or undefined set of kinds**
  - Depends on: none
  - Test location: "tests/Paramore.Brighter.Core.Tests/OutboxNotifications"
  - Test files, in order:
    1. `When_notification_options_have_a_capacity_out_of_range_should_throw_naming_the_setting_and_value.cs` (AC-2, direct path; a theory over 0, -1 and `MaxCapacity + 1`)
       - **USE COMMAND**: `/tdd:test-first when notification options have a capacity out of range should throw naming the setting and value`
    2. `When_notification_options_choose_no_kind_or_an_undefined_kind_should_throw_naming_the_setting.cs` (AC-55, second part; a theory over `None` and an undefined bit)
       - **USE COMMAND**: `/tdd:test-first when notification options choose no kind or an undefined kind should throw naming the setting`
  - Test should verify:
    - `new OutboxNotificationRegistry(producerRegistry, options)` throws `ConfigurationException`, and the message names `Capacity` or `Kinds` and the value given.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - Add `[Flags] OutboxNotificationKind` (None=0, SendFailed=1, ConfirmationFailed=2, MarkDispatchedFailed=4, Expired=8, All).
    - Add `OutboxNotificationOptions`:
      - `DefaultCapacity = 1024`, `MaxCapacity = 1_048_576`;
      - `Capacity` and `Kinds = All`;
      - `Validate()`, which throws `ConfigurationException` naming the setting and value.
    - Add the `OutboxNotificationRegistry` public constructor, which calls `Validate()` before anything else.

- [ ] **TEST + IMPLEMENT: 5 — The registry keys one channel per publication and finds it by topic and type**
  - Depends on: 4
  - Test location: "tests/Paramore.Brighter.Core.Tests/OutboxNotifications"
  - Test files, in order:
    1. `When_a_registry_is_built_should_list_one_key_per_publication.cs` (FR-14)
       - **USE COMMAND**: `/tdd:test-first when a registry is built should list one key per publication`
    2. `When_two_typed_publications_share_a_topic_should_give_each_its_own_key_and_channel.cs` (FR-14, AC-58)
       - **USE COMMAND**: `/tdd:test-first when two typed publications share a topic should give each its own key and channel`
    3. `When_a_registry_keyed_by_routing_key_holds_a_typed_publication_should_list_its_key_with_an_empty_type.cs` (FR-14)
       - **USE COMMAND**: `/tdd:test-first when a registry keyed by routing key holds a typed publication should list its key with an empty type`
    4. `When_a_producer_registry_cannot_find_its_own_producer_should_throw_from_the_constructor.cs` (FR-14; new double `ForgetfulProducerRegistry`)
       - **USE COMMAND**: `/tdd:test-first when a producer registry cannot find its own producer should throw from the constructor`
    5. `When_looking_up_an_unknown_key_should_throw_a_configuration_exception_naming_topic_and_type.cs` (ADR further test 1, first part)
       - **USE COMMAND**: `/tdd:test-first when looking up an unknown key should throw a configuration exception naming topic and type`
    6. `When_an_unattached_registry_is_disposed_should_end_a_waiting_read.cs` (ADR further test 4)
       - **USE COMMAND**: `/tdd:test-first when an unattached registry is disposed should end a waiting read`
  - Test 2 is expected green on arrival if test 1's code keys each channel by publication. If it is, treat it as a characterisation: pin it with the RED mutation "key the channels by topic alone" and confirm test 2 fails on the two keys sharing one channel, then revert it and stop at the gate.
  - Test should verify:
    - `Keys` holds one `ProducerKey` per producer, and each key's channel has that key and `Capacity`.
    - Typed publications on one topic give distinct channels. A `Dictionary<RoutingKey, …>` `ProducerRegistry` yields `CloudEventsType.Empty` in the key.
    - A third-party registry that returns no producer for both probes makes the constructor throw.
    - `LookupBy` with an unknown key throws `ConfigurationException`.
    - A `ReadAllAsync` waiting on an unattached registry ends after `Dispose`, without throwing.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - Add `IAmAnOutboxNotificationRegistry` (`Keys`, `LookupBy(RoutingKey, CloudEventsType? = null)`) and `IAmAnOutboxNotificationChannel` (`Key`, `DroppedCount`, `ReadAllAsync(CancellationToken)`).
    - Add `OutboxNotificationChannel` with public `Capacity` and an internal constructor and `Close`. Its `ReadAllAsync` delegates to the buffer's reader, which is enough for test 6. Task 15 replaces it with its own iterator. It holds a `Channel.CreateBounded` buffer with `AllowSynchronousContinuations = false` and `SingleReader`/`SingleWriter` false.
    - Add the registry's constructor:
      - probe each producer by (topic, type), then by topic alone, comparing by reference;
      - throw when neither probe finds the producer;
      - keep the keys in a dictionary, so lookups do not depend on the producer registry later.
    - Add `Dispose`, which closes every channel.
    - Add `OutboxNotification` as `(Id, Topic, Kind, Exception, OccurredAt)`.

- [ ] **CHARACTERISE: 6 — Born-green tests for the options and registry**
  - Depends on: 5
  - 1 test. It goes through its own `/tdd:test-first`, its own RED mutation and its own approval gate, as the CHARACTERISE rules in the header say. The task ends in one `test:` commit.
  - **6.1 — A capacity of 1 is accepted**
    - Test location: "tests/Paramore.Brighter.Core.Tests/OutboxNotifications"
    - Test file: `When_notification_options_have_a_capacity_of_one_should_build_the_registry.cs` (AC-3, second part)
    - **USE COMMAND**: `/tdd:test-first when notification options have a capacity of one should build the registry`
    - Test should verify: the registry constructs, and each channel's `Capacity` is 1.
    - 🔁 Characterisation — green on first run: task 4 rejects only values below 1. RED mutation: change the lower-bound check in `OutboxNotificationOptions.Validate` to `Capacity <= 1`. Confirm the test fails on construction throwing `ConfigurationException`.
    - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE once RED is observed — before committing if the test was green on arrival, before implementing if it was not** *(fires in the `review-before` gear, which is the default)*

## Phase 3: The mediator raises at each failure site
ADR: steps 5 and 6; raise-site table (13 rows); test groups "Each site raises once" and "Scope and routing"; further tests 1-3

- [ ] **TEST + IMPLEMENT: 7 — A failed async send notifies on its own publication's channel**
  - Depends on: 2, 5
  - Test location: "tests/Paramore.Brighter.Core.Tests/OutboxNotifications"
  - Test files, in order:
    1. `When_an_async_send_fails_should_notify_once_with_the_failure.cs` (AC-4; new doubles `ScriptedThrowingProducerAsync` and `RecordingSubscriber`)
       - **USE COMMAND**: `/tdd:test-first when an async send fails should notify once with the failure`
    2. `When_an_async_send_fails_on_one_of_two_publications_should_notify_only_on_that_publications_channel.cs` (FR-14)
       - **USE COMMAND**: `/tdd:test-first when an async send fails on one of two publications should notify only on that publication's channel`
  - Test 2 is expected green on arrival, because test 1's code already raises on the channel of the failing producer, through `ChannelFor`. Pin it with the RED mutation "give every producer the first channel" and confirm test 2 fails on the `orders` channel receiving the `invoices` failure, then revert it and stop at the gate.
  - Test should verify:
    - With `FakeTimeProvider` at 12:00:00, `ClearOutboxAsync(["m1"])` returns. There is exactly one notification: (`m1`, `orders`, `SendFailed`, `E`, 12:00:00).
    - With publications on `orders` and `invoices`, a failure on `invoices` reaches only that channel.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - Add the trailing optional `OutboxNotificationRegistry? notifications = null` after `outboxMeter` on the mediator's constructor, and a field beside `_outboxMeter`.
    - Add the registry's internal `ChannelFor`, keyed by a reference-equality comparer of its own, because netstandard2.0 lacks `ReferenceEqualityComparer`, and find the failing producer's channel through it.
    - Add the internal `OutboxNotificationChannel.Raise`.
    - At the `DispatchAsync` send site, raise from the exception that `TryExecuteWithResiliencePipelineAsync` returns. The raise goes directly after the helper returns, and before the untrack and `TripTopic` (raise-site row 4). The site first checks that it has a channel.
    - In `BuildOutBoxProducerMediator`, pass `null` for `notifications` after `OutboxMeter(serviceProvider)`. `Activator.CreateInstance` applies no constructor default, so without it every DI resolve of the mediator finds no constructor. Task 34 replaces the `null` with the container's registry. The existing suites of `Paramore.Brighter.Extensions.AspNetCore.Tests` and `Paramore.Brighter.Extensions.Tests`, which this task's full-suite run includes, pin it.
- [ ] **TEST + IMPLEMENT: 8 — A registry attaches to exactly one mediator over its own producer registry**
  - Depends on: 7
  - Test location: "tests/Paramore.Brighter.Core.Tests/OutboxNotifications"
  - Test files, in order:
    1. `When_a_second_mediator_attaches_to_a_registry_should_throw_and_add_no_handler_to_a_shared_producer.cs` (ADR further test 2)
       - **USE COMMAND**: `/tdd:test-first when a second mediator attaches to a registry should throw and add no handler to a shared producer`
    2. `When_a_registry_built_from_another_producer_registry_is_attached_should_throw.cs` (ADR further test 3)
       - **USE COMMAND**: `/tdd:test-first when a registry built from another producer registry is attached should throw`
  - Test should verify:
    - The second mediator's constructor throws `ConfigurationException`. The shared producer, a new double `HandlerCountingConfirmingProducer` that counts the handlers added to its confirmation event, still holds exactly one handler, the first mediator's.
    - A mediator given a registry built over a different producer registry throws from its constructor, and the same registry then attaches to a mediator over its own producer registry, so the failed attach changed no state.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - Add the internal `Attach(IAmAProducerRegistry)` to the registry. Task 17 adds the `TimeProvider` parameter the ADR names.
    - Make `Attach` throw on a second call without changing any state.
    - Make `Attach` check that every producer of the mediator's registry has a channel.
    - Call `Attach` in the mediator's constructor before `ConfigureCallbacks`.

- [ ] **TEST + IMPLEMENT: 9 — A failed sync send, confirming sync send and bulk send each notify**
  - Depends on: 7
  - Test location: "tests/Paramore.Brighter.Core.Tests/OutboxNotifications"
  - Test files, in order:
    1. `When_a_sync_send_fails_should_notify_send_failed.cs` (AC-5; reuse `FakeErroringMessageProducerSync`)
       - **USE COMMAND**: `/tdd:test-first when a sync send fails should notify send failed`
    2. `When_a_confirming_sync_send_fails_should_notify_send_failed_with_the_exception.cs` (AC-6; new double `SyncConfirmingProducer`, which can throw from `Send`)
       - **USE COMMAND**: `/tdd:test-first when a confirming sync send fails should notify send failed with the exception`
    3. `When_a_bulk_send_fails_should_notify_once_per_id_with_the_same_exception.cs` (AC-7; new double `NonConfirmingBulkProducerAsync`, a bulk producer without publish confirmation. Its `CreateBatchesAsync` returns one batch of the messages it is given, through a factory the constructor may replace, and its `SendAsync(batch)` throws the scripted exception, or succeeds when none is scripted. Tasks 10 and 14.7 use it with nothing scripted)
       - **USE COMMAND**: `/tdd:test-first when a bulk send fails should notify once per id with the same exception`
  - Test should verify:
    - One `SendFailed` for `m1` on the sync paths. For AC-6 it carries the producer's exception.
    - Three notifications for `m1`, `m2` and `m3` on `orders`, each with the same exception object.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - Switch `Dispatch`'s send and the confirming send to `TryExecuteWithResiliencePipeline`. Raise directly after the first returns an exception (raise-site row 2), and directly after the second, before the untrack (row 1).
    - In `BulkDispatchAsync`, switch the batch send to `TryExecuteWithResiliencePipelineAsync`. When it returns an exception, and before the untrack loop and `TripTopic`, raise one notification per `batch.Ids()` with `batch.RoutingKey` and that exception (row 6). Do this through one small mediator method. Task 17 wraps it in its own guard.

- [ ] **TEST + IMPLEMENT: 10 — A failed mark dispatched notifies on the sync, async and bulk paths**
  - Depends on: 9
  - Test location: "tests/Paramore.Brighter.Core.Tests/OutboxNotifications"
  - Test files, in order:
    1. `When_marking_dispatched_fails_after_a_sync_send_should_notify_mark_dispatched_failed.cs` (AC-12, sync; new double `FailingOutbox`, a wrapper over `InMemoryOutbox` that throws a set exception from chosen methods)
       - **USE COMMAND**: `/tdd:test-first when marking dispatched fails after a sync send should notify mark dispatched failed`
    2. `When_marking_dispatched_fails_after_an_async_send_should_notify_mark_dispatched_failed.cs` (AC-12, async)
       - **USE COMMAND**: `/tdd:test-first when marking dispatched fails after an async send should notify mark dispatched failed`
    3. `When_marking_dispatched_fails_after_a_bulk_send_should_notify_once_per_id.cs` (AC-12, bulk; reuse `NonConfirmingBulkProducerAsync` from task 9 with nothing scripted)
       - **USE COMMAND**: `/tdd:test-first when marking dispatched fails after a bulk send should notify once per id`
  - Test should verify: one `MarkDispatchedFailed` with `E` per message, and each message is still undispatched.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - Switch the mark call in `Dispatch` and `DispatchAsync` to `TryExecuteWithResiliencePipeline(Async)`, and raise directly after it returns an exception (rows 3 and 5).
    - Switch the per-id mark in `BulkDispatchAsync` to `TryExecuteWithResiliencePipelineAsync`, and raise per id with the stored topic when it returns an exception (row 7).

- [ ] **TEST + IMPLEMENT: 11 — A failed confirmation notifies, unless its kind is not chosen**
  - Depends on: 9
  - Test location: "tests/Paramore.Brighter.Core.Tests/OutboxNotifications"
  - Test files, in order:
    1. `When_an_async_confirmation_fails_should_notify_confirmation_failed_without_an_exception.cs` (AC-10; `InMemoryMessageProducer` with `UseAsyncPublishConfirmation = true` and a predicate that fails `m1`)
       - **USE COMMAND**: `/tdd:test-first when an async confirmation fails should notify confirmation failed without an exception`
    2. `When_a_sync_confirmation_fails_with_an_empty_result_should_notify_with_an_empty_id_and_topic.cs` (AC-11, first part; reuse `SyncConfirmingProducer` from task 9)
       - **USE COMMAND**: `/tdd:test-first when a sync confirmation fails with an empty result should notify with an empty id and topic`
    3. `When_confirmation_failures_are_not_chosen_should_raise_nothing_for_a_failed_confirmation.cs` (FR-22)
       - **USE COMMAND**: `/tdd:test-first when confirmation failures are not chosen should raise nothing for a failed confirmation`
  - Test should verify:
    - AC-10 gives (`m1`, `orders`, `ConfirmationFailed`, null). It is read after `ClearOutboxAsync` has returned.
    - `(false, Id.Empty, null, null)` gives (`Id.Empty`, `RoutingKey.Empty`, `ConfirmationFailed`), and nothing throws on the raising thread.
    - With `Kinds = SendFailed`, a failed confirmation gives no notification and `DroppedCount` stays 0.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - In `ConfigureCallbacks`, capture each producer's channel in its handlers.
    - Raise as the first statement of the `else` in `HandleAsyncPublishConfirmation` (row 8) and in `ConfigurePublisherCallbackMaybe` (row 11).
    - Make `Raise` skip a kind that is not in `Kinds`, before it counts or writes anything.

- [ ] **TEST + IMPLEMENT: 12 — A failed mark after a successful confirmation notifies, including when the pipeline is missing**
  - Depends on: 10, 11
  - Test location: "tests/Paramore.Brighter.Core.Tests/OutboxNotifications"
  - Test files, in order:
    1. `When_marking_dispatched_fails_after_an_async_callback_confirmation_should_notify_mark_dispatched_failed.cs` (AC-13, first two parts; a theory over `m1` and `Id.Empty`; new double `AsyncCallbackConfirmingProducer`, with sync and async producer roles and `UseAsyncPublishConfirmation = true`, which raises a scripted result. Task 13.8 reuses it, because `CallViaExternalBus` looks the producer up through `LookupSyncBy`, and `StubConfirmingProducerAsync` has no sync role)
       - **USE COMMAND**: `/tdd:test-first when marking dispatched fails after an async callback confirmation should notify mark dispatched failed`
    2. `When_marking_dispatched_fails_after_a_sync_confirmation_should_notify_mark_dispatched_failed.cs` (AC-13, sync)
       - **USE COMMAND**: `/tdd:test-first when marking dispatched fails after a sync confirmation should notify mark dispatched failed`
    3. `When_an_async_callback_confirmation_finds_no_outbox_pipeline_should_notify_with_the_lookup_exception.cs` (AC-13, third part, async)
       - **USE COMMAND**: `/tdd:test-first when an async callback confirmation finds no outbox pipeline should notify with the lookup exception`
    4. `When_a_sync_confirmation_finds_no_outbox_pipeline_should_notify_with_the_lookup_exception.cs` (AC-13, third part, sync)
       - **USE COMMAND**: `/tdd:test-first when a sync confirmation finds no outbox pipeline should notify with the lookup exception`
  - Test should verify:
    - One `MarkDispatchedFailed` with `E`, with id `m1` or `Id.Empty`.
    - With no `CommandProcessor.OutboxProducer` pipeline, one `MarkDispatchedFailed` with the `KeyNotFoundException`, and nothing throws on the raising thread.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - Switch both confirmation mark calls to `TryExecuteWithResiliencePipeline(Async)`, and raise when the returned exception is not null.
    - Raise as the last statement of the `else` of `marked`, after the untrack, in `HandleAsyncPublishConfirmation` (row 9) and `ConfigurePublisherCallbackMaybe` (row 12).
    - Raise as the first statement of each catch, only when `result.Success` (rows 10 and 13).

- [ ] **CHARACTERISE: 13 — Born-green tests for the raise sites**
  - Depends on: 7, 11, 12
  - 11 items, in order. Each item, and each file of an item that lists several, goes through its own `/tdd:test-first`, its own RED mutation and its own approval gate, as the CHARACTERISE rules in the header say. The task ends in one `test:` commit.
  - **13.1 — A non-bulk sweep notifies once per failed message**
    - Test location: "tests/Paramore.Brighter.Core.Tests/OutboxNotifications"
    - Test file: `When_a_non_bulk_sweep_fails_for_two_messages_should_notify_once_per_id.cs` (AC-8)
    - **USE COMMAND**: `/tdd:test-first when a non bulk sweep fails for two messages should notify once per id`
    - Test should verify: `ClearOutstandingFromOutboxAsync` with `useBulk` false gives two `SendFailed`, one each for `m1` and `m2`.
    - 🔁 Characterisation — green on first run: the sweep calls `DispatchAsync`, which task 7 changed. RED mutation: skip the raise in `DispatchAsync` when it is called from `BackgroundDispatchUsingAsync`. Confirm the test fails on the count of notifications.
    - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE once RED is observed — before committing if the test was green on arrival, before implementing if it was not** *(fires in the `review-before` gear, which is the default)*
  - **13.2 — A cancelled send notifies with the cancellation**
    - Test location: "tests/Paramore.Brighter.Core.Tests/OutboxNotifications"
    - Test file: `When_a_send_is_cancelled_should_notify_send_failed_with_the_cancellation.cs` (AC-9; new double `TokenWaitingProducerAsync`)
    - **USE COMMAND**: `/tdd:test-first when a send is cancelled should notify send failed with the cancellation`
    - Test should verify: one `SendFailed` whose exception is an `OperationCanceledException`.
    - 🔁 Characterisation — green on first run: the helper returns every exception it catches. RED mutation: skip the raise when the exception is an `OperationCanceledException`. Confirm the test fails on the notification count.
    - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE once RED is observed — before committing if the test was green on arrival, before implementing if it was not** *(fires in the `review-before` gear, which is the default)*
  - **13.3 — Retries inside the pipeline notify once, with the last exception**
    - Test location: "tests/Paramore.Brighter.Core.Tests/OutboxNotifications"
    - Test file: `When_a_send_fails_after_three_retries_should_notify_once_with_the_last_exception.cs` (AC-18)
    - **USE COMMAND**: `/tdd:test-first when a send fails after three retries should notify once with the last exception`
    - Test should verify: four sends, one notification, and its exception is `E4`.
    - 🔁 Characterisation — green on first run: the raise sits after the helper returns. RED mutation: move the raise from after the helper into the pipeline's per-attempt delegate, as a catch that raises and rethrows, so each of the four failed tries raises and the site after the helper does not. Confirm the test fails on the count (4, not 1).
    - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE once RED is observed — before committing if the test was green on arrival, before implementing if it was not** *(fires in the `review-before` gear, which is the default)*
  - **13.4 — A retried message notifies again for each failed sweep**
    - Test location: "tests/Paramore.Brighter.Core.Tests/OutboxNotifications"
    - Test file: `When_a_message_fails_twice_and_then_succeeds_should_notify_twice.cs` (AC-19)
    - **USE COMMAND**: `/tdd:test-first when a message fails twice and then succeeds should notify twice`
    - Test should verify: across three sweeps there are two notifications, both for `m1`, and none for the third sweep.
    - 🔁 Characterisation — green on first run: the raise keeps no state between dispatches. RED mutation: keep a set of ids already notified and skip a repeat. Confirm the test fails on the count (1, not 2).
    - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE once RED is observed — before committing if the test was green on arrival, before implementing if it was not** *(fires in the `review-before` gear, which is the default)*
  - **13.5 — A failed confirmation for a `Call` notifies**
    - Test location: "tests/Paramore.Brighter.Core.Tests/OutboxNotifications"
    - Test file: `When_a_call_confirmation_fails_on_a_sync_confirming_producer_should_notify_confirmation_failed.cs` (AC-11, second part; Q-8)
    - **USE COMMAND**: `/tdd:test-first when a call confirmation fails on a sync confirming producer should notify confirmation failed`
    - Test should verify: `CallViaExternalBus`, followed by a failed result for that message, gives one `ConfirmationFailed`.
    - 🔁 Characterisation — green on first run: the callback raises from the result alone. RED mutation: raise only when the outbox holds the result's id. Confirm the test fails on the count.
    - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE once RED is observed — before committing if the test was green on arrival, before implementing if it was not** *(fires in the `review-before` gear, which is the default)*
  - **13.6 — A confirmation with no topic still reaches its producer's channel**
    - Test location: "tests/Paramore.Brighter.Core.Tests/OutboxNotifications"
    - Test file: `When_a_sync_confirming_producer_for_invoices_raises_an_empty_result_should_reach_the_invoices_channel.cs` (AC-57, second part)
    - **USE COMMAND**: `/tdd:test-first when a sync confirming producer for invoices raises an empty result should reach the invoices channel`
    - Test should verify: on a fresh mediator, the `invoices` channel returns the notification.
    - 🔁 Characterisation — green on first run: the handler captured the producer's channel. RED mutation: look the channel up with `LookupBy(result.Topic)` instead of using the captured channel. Confirm the test fails because the `invoices` channel returns nothing.
    - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE once RED is observed — before committing if the test was green on arrival, before implementing if it was not** *(fires in the `review-before` gear, which is the default)*
  - **13.7 — A typed publication's send failure reaches only its own channel**
    - Test location: "tests/Paramore.Brighter.Core.Tests/OutboxNotifications"
    - Test file: `When_a_send_fails_for_a_typed_publication_should_reach_only_that_types_channel.cs` (AC-58, first part)
    - **USE COMMAND**: `/tdd:test-first when a send fails for a typed publication should reach only that type's channel`
    - Test should verify: a failed `ClearOutboxAsync(["m1"])` for type `A`, on a producer that is not a bulk producer, reaches `A`'s channel, and `B`'s channel stays empty.
    - 🔁 Characterisation — green on first run: the send site uses the channel of the producer it looked up. RED mutation: make `ChannelFor` look up by topic only, returning the first channel on `orders`. Confirm the test fails on the channel that receives it.
    - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE once RED is observed — before committing if the test was green on arrival, before implementing if it was not** *(fires in the `review-before` gear, which is the default)*
  - **13.8 — A `Call`'s successful confirmation notifies when the store cannot mark it**
    - Test location: "tests/Paramore.Brighter.Core.Tests/OutboxNotifications"
    - Test file: `When_a_call_is_confirmed_and_the_outbox_cannot_mark_it_should_notify_mark_dispatched_failed.cs` (AC-13, fourth part; Q-8)
    - **USE COMMAND**: `/tdd:test-first when a call is confirmed and the outbox cannot mark it should notify mark dispatched failed`
    - Test should verify: `CallViaExternalBus` on the async-callback producer, then success, with `FailingOutbox` throwing `E` from `MarkDispatchedAsync`, gives one `MarkDispatchedFailed` with `E`.
    - 🔁 Characterisation — green on first run: the catch raises from the exception alone. RED mutation: raise only when the outbox holds the id. Confirm the test fails on the count.
    - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE once RED is observed — before committing if the test was green on arrival, before implementing if it was not** *(fires in the `review-before` gear, which is the default)*
  - **13.9 — Two mediators keep their notifications apart**
    - Test location: "tests/Paramore.Brighter.Core.Tests/OutboxNotifications"
    - Test file: `When_two_mediators_have_their_own_registries_should_notify_only_the_failing_mediators_subscriber.cs` (AC-32)
    - **USE COMMAND**: `/tdd:test-first when two mediators have their own registries should notify only the failing mediator's subscriber`
    - Test should verify: only the first subscriber receives anything, and the second channel's `DroppedCount` is 0.
    - 🔁 Characterisation — green on first run: each registry holds its own channels. RED mutation: make the key-to-channel dictionary that `LookupBy` reads `static`, and clear it at the start of the constructor, so the second registry replaces the first one's entries. Confirm the test fails on the first subscriber receiving nothing, because its `LookupBy` now returns the second registry's channel.
    - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE once RED is observed — before committing if the test was green on arrival, before implementing if it was not** *(fires in the `review-before` gear, which is the default)*
  - **13.10 — Lookups still work after the mediator is disposed**
    - Test location: "tests/Paramore.Brighter.Core.Tests/OutboxNotifications"
    - Test file: `When_the_mediator_is_disposed_should_still_look_up_each_key.cs` (ADR further test 1, second part)
    - **USE COMMAND**: `/tdd:test-first when the mediator is disposed should still look up each key`
    - Test should verify: `LookupBy` returns the same channel before and after the mediator's `Dispose`.
    - 🔁 Characterisation — green on first run: the registry keeps its own keys. RED mutation: resolve `LookupBy` through the producer registry at call time, which `CloseAll` has cleared. Confirm the test fails on `LookupBy` throwing after the dispose.
    - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE once RED is observed — before committing if the test was green on arrival, before implementing if it was not** *(fires in the `review-before` gear, which is the default)*
  - **13.11 — A producer shared by two mediators raises on both registries' channels**
    - Test location: "tests/Paramore.Brighter.Core.Tests/OutboxNotifications"
    - Test file: `When_a_producer_shared_by_two_mediators_reports_a_failed_confirmation_should_notify_both_registries.cs` (FR-14; reuse `SyncConfirmingProducer` from task 9, one instance in two producer registries, each with its own registry and mediator)
    - **USE COMMAND**: `/tdd:test-first when a producer shared by two mediators reports a failed confirmation should notify both registries`
    - Test should verify: one failed result for `m1` gives one `ConfirmationFailed` for `m1` on each registry's `orders` channel.
    - 🔁 Characterisation — green on first run: each mediator's handler captured its own registry's channel. RED mutation: keep the captured channels in a `static` dictionary keyed by producer, and have each handler read its channel from that dictionary when it fires, so the second mediator's `ConfigureCallbacks` replaces the first one's channel. Confirm the test fails on the first registry's channel receiving nothing.
    - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE once RED is observed — before committing if the test was green on arrival, before implementing if it was not** *(fires in the `review-before` gear, which is the default)*

## Phase 4: Nothing else raises, and behaviour is unchanged
ADR: test groups "Nothing else raises", "Behaviour unchanged", "Success raises nothing", "Scope and routing" (AC-33); raise-site table reading notes 1-4

- [ ] **CHARACTERISE: 14 — Nothing else raises, and behaviour is unchanged**
  - Depends on: 9, 10, 12
  - 8 items, in order. Each item, and each file of an item that lists several, goes through its own `/tdd:test-first`, its own RED mutation and its own approval gate, as the CHARACTERISE rules in the header say. The task ends in one `test:` commit.
  - **14.1 — A missing id raises nothing**
    - Test location: "tests/Paramore.Brighter.Core.Tests/OutboxNotifications"
    - Test files: `When_clearing_a_missing_id_should_log_and_raise_nothing.cs` and `When_clearing_a_missing_id_async_should_log_and_raise_nothing.cs` (AC-14; join the log collection)
    - **USE COMMAND**: `/tdd:test-first when clearing a missing id should log and raise nothing` (once per file)
    - Test should verify: `m1` is dispatched, nothing throws, the existing `Error` entry is written, and no notification is raised.
    - 🔁 Characterisation — green on first run: the missing-id branch has no raise. RED mutation: beside the existing `Error` log, raise `SendFailed` with the missing id, a null topic and no exception on the channel of the registry's first key. The test has one publication and reads that channel. Confirm each test fails on the count.
    - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE once RED is observed — before committing if the test was green on arrival, before implementing if it was not** *(fires in the `review-before` gear, which is the default)*
  - **14.2 — A missing producer and a failed batch creation raise nothing**
    - Test location: "tests/Paramore.Brighter.Core.Tests/OutboxNotifications"
    - Test files (AC-15):
      - `When_clearing_a_message_with_no_producer_should_throw_as_before_and_raise_nothing.cs`
      - `When_sweeping_a_message_with_no_producer_should_throw_as_before_and_raise_nothing.cs`
      - `When_batch_creation_throws_during_a_bulk_sweep_should_throw_as_before_and_raise_nothing.cs`
      - `When_a_bulk_sweep_meets_a_producer_that_is_not_a_bulk_producer_should_throw_as_before_and_raise_nothing.cs`
    - **USE COMMAND**: `/tdd:test-first when clearing a message with no producer should throw as before and raise nothing` (and the matching phrase per file)
    - Test should verify: the same exception type as today reaches the caller, `m2` is not attempted, and no notification is raised.
    - 🔁 Characterisation — green on first run: these throw before any helper runs. RED mutations, per file:
      - wrap the producer lookup in a try/catch that raises `SendFailed` with the message's id and topic and the caught exception, and rethrows;
      - the same, on the sweep path;
      - wrap `CreateBatchesAsync` the same way, raising for the first message of the page;
      - in the `else` branch taken when the producer is not a bulk producer, raise `SendFailed` for the first message of the page with the `InvalidOperationException`, just before it is thrown.
      Each mutation raises on the channel of the registry's first key, because the first two paths have no producer to find a channel by. Each test has one publication and reads that channel. Confirm each fails on the count.
    - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE once RED is observed — before committing if the test was green on arrival, before implementing if it was not** *(fires in the `review-before` gear, which is the default)*
  - **14.3 — An outbox read that throws raises nothing**
    - Test location: "tests/Paramore.Brighter.Core.Tests/OutboxNotifications"
    - Test files (AC-16; reuse `FailingOutbox`):
      - `When_the_outbox_get_throws_during_a_clear_should_propagate_and_raise_nothing.cs`
      - `When_the_outbox_get_async_throws_during_a_clear_should_propagate_and_raise_nothing.cs`
      - `When_the_outstanding_fetch_throws_during_a_sweep_should_propagate_and_raise_nothing.cs`
    - **USE COMMAND**: `/tdd:test-first when the outbox get throws during a clear should propagate and raise nothing` (and the matching phrase per file)
    - Test should verify: the outbox's exception propagates unchanged, and no notification is raised.
    - 🔁 Characterisation — green on first run: no raise surrounds the reads. RED mutation: wrap each read in a try/catch that raises `SendFailed` with the requested id, or `Id.Empty` for the outstanding fetch, a null topic and the caught exception, on the channel of the registry's first key, and rethrows. Each test has one publication and reads that channel. Confirm each test fails on the count.
    - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE once RED is observed — before committing if the test was green on arrival, before implementing if it was not** *(fires in the `review-before` gear, which is the default)*
  - **14.4 — The nine FR-4 cases raise nothing**
    - Test location: "tests/Paramore.Brighter.Core.Tests/OutboxNotifications"
    - Test files (AC-17, one per case, each with its RED mutation):
      - `When_every_message_of_a_clear_succeeds_should_raise_nothing.cs` — mutation: raise when the helper returns null.
      - `When_a_sweep_cannot_take_the_semaphore_should_raise_nothing.cs` — mutation: raise one `SendFailed` with `Id.Empty` on the channel of the registry's first key when the semaphore is not acquired. The test has one publication and reads that channel.
      - `When_a_sweep_skips_a_tripped_topic_should_raise_nothing.cs` (reuse `ListedTopicsCircuitBreaker`) — mutation: when the breaker filters a topic out, raise one `SendFailed` with `Id.Empty` and that topic on the channel of the registry's first key. The test has one publication, on the tripped topic, and reads that channel.
      - `When_adding_to_the_outbox_fails_should_throw_channel_failure_and_raise_nothing.cs` — mutation: in the `AddToOutboxAsync` failure path, raise `SendFailed` for the message being added on the channel of the registry's first key. The test has one publication and reads that channel.
      - `When_a_call_fails_on_a_non_confirming_producer_should_raise_nothing.cs` — mutation: switch `CallViaExternalBus` to the `TryExecute` helper and raise.
      - `When_a_confirming_producer_never_raises_a_result_should_raise_nothing.cs` — mutation: raise `ConfirmationFailed` after a confirming send returns.
      - `When_in_memory_producer_reports_success_to_a_sync_only_outbox_should_raise_nothing.cs` (reuse `SyncOnlyOutboxWrapper`) — mutation: raise `MarkDispatchedFailed` when the async outbox is absent.
      - `When_a_clear_has_no_outbox_pipeline_should_throw_the_lookup_exception_and_raise_nothing.cs` — mutation: catch the pipeline lookup exception, raise `SendFailed` with it for the message being sent on the channel of the registry's first key, then rethrow it. The exception still escapes, so the test fails on the count. The test has one publication and reads that channel.
      - `When_the_logger_throws_inside_the_send_helpers_catch_should_propagate_and_raise_nothing.cs` (join the log collection; `CapturingLoggerProvider.ThrowOnce`) — mutation: catch the logger's exception at the `DispatchAsync` send site and raise `SendFailed` from it.
    - **USE COMMAND**: `/tdd:test-first when every message of a clear succeeds should raise nothing` (and the matching phrase per file)
    - Test should verify: in each case, no notification and the same caller-visible outcome as today.
    - 🔁 Characterisation — green on first run: none of these paths reaches a raise site. Confirm each mutation fails its own test on the count.
    - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE once RED is observed — before committing if the test was green on arrival, before implementing if it was not** *(fires in the `review-before` gear, which is the default)*
  - **14.5 — The breaker and the meter see the same calls with and without the feature**
    - Test location: "tests/Paramore.Brighter.Core.Tests/OutboxNotifications"
    - Test file: `When_the_same_failures_run_with_and_without_notifications_should_make_identical_breaker_and_meter_calls.cs` (AC-20, first part; reuse `SpyOutboxMeter` and `NonConfirmingBulkProducerAsync` from task 9 for the failed batch; new double `CountingOutboxCircuitBreaker`, in `OutboxNotifications/TestDoubles`, that records each call with its topic; join the log collection)
    - **USE COMMAND**: `/tdd:test-first when the same failures run with and without notifications should make identical breaker and meter calls`
    - Test should verify:
      - The `TripTopic` and meter calls are identical over a failed sync send, a failed async send, a failed batch, a failed confirmation, a failed mark and a success.
      - The failed sync send makes no `TripTopic` call.
    - 🔁 Characterisation — green on first run: the raise touches neither the breaker nor the meter. RED mutation: at the `DispatchAsync` send site, call `RecordCleared` beside the raise, so the meter records a clear the run without notifications does not. Confirm the test fails on the meter comparison.
    - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE once RED is observed — before committing if the test was green on arrival, before implementing if it was not** *(fires in the `review-before` gear, which is the default)*
  - **14.6 — A breaker that throws runs after the raise, and a catch does not raise twice**
    - Test location: "tests/Paramore.Brighter.Core.Tests/OutboxNotifications"
    - Test files (AC-21; reuse `ThrowingOutboxCircuitBreaker` from `Confirmation/TestDoubles`, and `NonConfirmingBulkProducerAsync` from task 9 for the bulk file):
      - `When_trip_topic_throws_after_an_async_send_fails_should_propagate_and_still_notify.cs` — mutation: move the raise after `TripTopic`.
      - `When_trip_topic_throws_after_a_bulk_send_fails_should_propagate_and_notify_once_per_id.cs` — mutation: move the raise after `TripTopic`.
      - `When_trip_topic_throws_after_an_async_confirmation_fails_should_notify_once.cs` — mutation: drop the `result.Success` guard in the catch.
      - `When_trip_topic_throws_after_a_sync_confirmation_fails_should_notify_once.cs` — mutation: drop the `result.Success` guard in the catch.
    - **USE COMMAND**: `/tdd:test-first when trip topic throws after an async send fails should propagate and still notify` (and the matching phrase per file)
    - Test should verify: the breaker's exception propagates where it does today, and the count is exactly one per message.
    - 🔁 Characterisation — green on first run: every raise precedes `TripTopic`, and the catch guards on `result.Success`. Confirm each mutation fails on the count.
    - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE once RED is observed — before committing if the test was green on arrival, before implementing if it was not** *(fires in the `review-before` gear, which is the default)*
  - **14.7 — Success raises nothing, and `InMemoryOutbox` marks a `Call` without a notification**
    - Test location: "tests/Paramore.Brighter.Core.Tests/OutboxNotifications"
    - Test files:
      - `When_sends_and_a_call_succeed_should_raise_nothing.cs` (AC-43, first part; sync, async and `CallViaExternalBus`; reuse `NonConfirmingProducerSync`/`NonConfirmingProducerAsync`)
      - `When_a_confirming_producer_confirms_a_swept_message_should_raise_nothing.cs` (AC-43, second part)
      - `When_a_sweep_sends_two_messages_without_an_exception_should_raise_nothing.cs` (AC-44 direct path; a theory over `useBulk`, with `NonConfirmingBulkProducerAsync` from task 9, nothing scripted, for the bulk case)
      - `When_a_call_is_confirmed_on_in_memory_outbox_should_raise_nothing.cs` (AC-13, last part)
    - **USE COMMAND**: `/tdd:test-first when sends and a call succeed should raise nothing` (and the matching phrase per file)
    - Test should verify: no notification in any case.
    - 🔁 Characterisation — green on first run: each site raises only on a returned exception. RED mutation: invert the check at each site to raise when the helper returns null. Confirm each test fails on the count.
    - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE once RED is observed — before committing if the test was green on arrival, before implementing if it was not** *(fires in the `review-before` gear, which is the default)*
  - **14.8 — Every outbox shape gives the same notifications and no extra outbox calls**
    - Test location: "tests/Paramore.Brighter.Core.Tests/OutboxNotifications"
    - Test files (AC-33; new doubles `CallRecordingSyncOutbox` and `CallRecordingAsyncOutbox` over the Archiving single-role wrappers):
      - `When_failures_run_against_in_memory_outbox_should_notify_without_extra_outbox_calls.cs`
      - `When_failures_run_against_an_async_only_outbox_should_notify_without_extra_outbox_calls.cs`
      - `When_failures_run_against_a_sync_only_outbox_should_notify_without_extra_outbox_calls.cs`
    - **USE COMMAND**: `/tdd:test-first when failures run against in memory outbox should notify without extra outbox calls` (and the matching phrase per file)
    - Test should verify: the notifications match for the paths each shape supports, and each outbox receives the same calls as with the feature off.
    - 🔁 Characterisation — green on first run: a raise reads only what the site already holds. RED mutation: read the message by id from the outbox inside the raise. Confirm each test fails on the recorded calls.
    - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE once RED is observed — before committing if the test was green on arrival, before implementing if it was not** *(fires in the `review-before` gear, which is the default)*

## Phase 5: Delivery, the full buffer and shutdown
ADR: steps 5 (close) and 7; sections "How the drop warning stays limited" and "How FR-12 and the close hold"; test groups "Delivery, order, full buffer", "Never blocks, shutdown", "Content and documents" (AC-39); further test 5

- [ ] **TEST + IMPLEMENT: 15 — The stream ends cleanly on dispose and honours its token on every item**
  - Depends on: 11
  - Test location: "tests/Paramore.Brighter.Core.Tests/OutboxNotifications"
  - Test files, in order:
    1. `When_the_mediator_is_disposed_with_buffered_notifications_should_deliver_them_and_end_the_stream.cs` (AC-30)
       - **USE COMMAND**: `/tdd:test-first when the mediator is disposed with buffered notifications should deliver them and end the stream`
    2. `When_a_reader_is_cancelled_after_its_first_read_of_a_full_buffer_should_throw_before_the_next_notification.cs` (ADR further test 5)
       - **USE COMMAND**: `/tdd:test-first when a reader is cancelled after its first read of a full buffer should throw before the next notification`
  - Test should verify:
    - `Dispose` returns while a reader is blocked. Once released, the reader gets two notifications and the enumeration ends.
    - A reader that first arrives after `Dispose` reads the same way, and nothing throws.
    - Cancelling after the first item throws `OperationCanceledException` before a second item is taken.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - Call the registry's internal `Close` in a `finally` on the `try` around `CloseAll` in `Dispose(bool)`. `Close` is `TryComplete` per channel.
    - Make `ReadAllAsync` its own iterator over `WaitToReadAsync`/`TryRead` that calls `ThrowIfCancellationRequested` before each item.

- [ ] **TEST + IMPLEMENT: 16 — A full or closed channel drops and counts**
  - Depends on: 15
  - Test location: "tests/Paramore.Brighter.Core.Tests/OutboxNotifications"
  - Test files, in order:
    1. `When_a_channel_of_capacity_two_receives_three_notifications_should_keep_the_newest_two_and_count_one_dropped.cs` (AC-23)
       - **USE COMMAND**: `/tdd:test-first when a channel of capacity two receives three notifications should keep the newest two and count one dropped`
    2. `When_a_confirmation_fails_after_dispose_should_count_it_dropped_and_log_nothing.cs` (AC-31, second part; join the log collection)
       - **USE COMMAND**: `/tdd:test-first when a confirmation fails after dispose should count it dropped and log nothing`
  - Test should verify:
    - The reader gets n2 then n3, and `DroppedCount` is 1.
    - After `Dispose`, a raise throws nothing, `DroppedCount` goes up by 1, and the feature writes no log entry.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - Use `BoundedChannelFullMode.DropOldest`, with the `itemDropped` delegate doing `Interlocked.Increment`.
    - Count a `TryWrite` that returns false as dropped, with no log.
    - Make `DroppedCount` read through `Interlocked.Read`.

- [ ] **TEST + IMPLEMENT: 17 — Drops are warned about at most once a minute, and a fault in the feature never reaches the publish path**
  - Depends on: 8, 14, 16
  - Test location: "tests/Paramore.Brighter.Core.Tests/OutboxNotifications" (every file joins `[Collection(StaleMessageLoggingCollection.Name)]`)
  - Test files, in order:
    1. `When_drops_continue_across_the_log_interval_should_warn_once_per_interval.cs` (AC-24; `FakeTimeProvider` at T, T + 59 s and T + 60 s)
       - **USE COMMAND**: `/tdd:test-first when drops continue across the log interval should warn once per interval`
    2. `When_the_drop_warning_logger_throws_should_return_from_each_clear_and_keep_accepting_notifications.cs` (AC-35; `CapturingLoggerProvider.ThrowOnce`)
       - **USE COMMAND**: `/tdd:test-first when the drop warning logger throws should return from each clear and keep accepting notifications`
    3. `When_raising_a_notification_faults_should_log_only_the_exception_type.cs` (AC-39, first part; the fault is inside `Raise`: `CapturingLoggerProvider.ThrowOnce` makes the drop warning throw on a channel of capacity 1, as in test 2, and the test reads the entry `Raise`'s catch writes. Test 2's code may swallow the fault without logging it, so this test is RED on that entry)
       - **USE COMMAND**: `/tdd:test-first when raising a notification faults should log only the exception type`
    4. `When_a_batch_throws_while_its_ids_are_read_should_log_only_the_exception_type_and_return.cs` (AC-39, batch guard; new double `ThrowingIdsBatch`, whose `Ids()` throws on its first enumeration only and returns its ids after that, returned by the batch factory of task 9's `NonConfirmingBulkProducerAsync`, with `CountingOutboxCircuitBreaker` from 14.5 recording the trips, so the first enumeration is the guard's and the untrack loop after it still reads the ids. A producer that does not confirm fails the send without reading `batch.Ids()`. With no guard yet, the fault escapes `BulkDispatchAsync`, so this test is RED on the call returning until this task adds the guard)
       - **USE COMMAND**: `/tdd:test-first when a batch throws while its ids are read should log only the exception type and return`
    5. `When_a_second_mediator_with_another_clock_is_rejected_should_keep_warning_on_the_first_clock.cs` (ADR: `Attach` is atomic; two `FakeTimeProvider`s, capacity 1)
       - **USE COMMAND**: `/tdd:test-first when a second mediator with another clock is rejected should keep warning on the first clock`
  - Test 5 is RED if test 1's code stores the clock before the checks of task 8. If it is green on arrival, pin it with the RED mutation "store the clock before the checks" and confirm test 5 fails on the drop after the second clock advances, then revert it and stop at the gate.
  - Test should verify:
    - Exactly two `Warning` entries, at T and T + 60 s, each naming the key and the dropped count at that time. No such entry when nothing drops.
    - Each clear returns as with the feature off, and a later notification is read.
    - A fault entry names the exception type, has a null `Exception`, and contains no message body.
    - In test 4 the batch call returns, and the counting breaker records a `TripTopic` call for the batch's topic.
    - In test 5, both clocks start at the same instant. A drop at T writes the first `Warning`. A second mediator with the second clock is then rejected. A drop once only the second clock has advanced 60 s writes no new `Warning`, and a drop once the first clock has advanced 60 s writes one.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - Add `LoggerMessage` entries in `OutboxNotificationChannel`, with the logger from `ApplicationLogging.CreateLogger<OutboxNotificationChannel>()`.
    - Add the `TimeProvider` parameter to `Attach`, which hands the mediator's clock to every channel for the interval and the drop warnings. Test 1 needs it. Store the clock only after both checks of task 8 pass, so a rejected second mediator leaves the first one's clock in place. Test 5 needs it. A channel's clock field is null until `Attach`. Task 31 adds the default.
    - Make the `itemDropped` delegate compare the `TimeProvider` ticks with the next permitted time and claim the slot with `Interlocked.CompareExchange`. The interval is 60 s and the first drop always logs.
    - Catch every exception in `Raise` and swallow it, which is all test 2 needs. Test 3 then makes the catch log the exception type only.
    - Wrap the batch-raise method that task 9 added in its own guard, which catches every exception and logs the exception type only.

- [ ] **CHARACTERISE: 18 — Born-green tests for delivery, the full buffer and shutdown**
  - Depends on: 11, 15, 16
  - 13 items, in order. Each item, and each file of an item that lists several, goes through its own `/tdd:test-first`, its own RED mutation and its own approval gate, as the CHARACTERISE rules in the header say. The task ends in one `test:` commit.
  - **18.1 — A confirmation raised while producers are disposed is delivered**
    - Test location: "tests/Paramore.Brighter.Core.Tests/OutboxNotifications"
    - Test file: `When_a_producer_raises_a_failed_confirmation_from_dispose_should_deliver_it.cs` (AC-31, first part; new double `DisposeRaisingConfirmingProducer`)
    - **USE COMMAND**: `/tdd:test-first when a producer raises a failed confirmation from dispose should deliver it`
    - Test should verify: the notification raised during the mediator's `Dispose` is read.
    - 🔁 Characterisation — green on first run: `Close` runs after `CloseAll`. RED mutation: call `Close` before `CloseAll`. Confirm the test fails on the reader receiving nothing.
    - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE once RED is observed — before committing if the test was green on arrival, before implementing if it was not** *(fires in the `review-before` gear, which is the default)*
  - **18.2 — The default capacity is 1,024**
    - Test location: "tests/Paramore.Brighter.Core.Tests/OutboxNotifications"
    - Test file: `When_no_capacity_is_set_should_drop_one_after_the_default_capacity.cs` (AC-3, first part; the registry is built with `options: null`)
    - **USE COMMAND**: `/tdd:test-first when no capacity is set should drop one after the default capacity`
    - Test should verify: 1,025 undelivered notifications give `DroppedCount` 1.
    - 🔁 Characterisation — green on first run: options default to `DefaultCapacity`. RED mutation: build channels at `MaxCapacity` when the options are null. Confirm the test fails on `DroppedCount` 0.
    - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE once RED is observed — before committing if the test was green on arrival, before implementing if it was not** *(fires in the `review-before` gear, which is the default)*
  - **18.3 — The channel holds at most its capacity, and a notification holds no message**
    - Test location: "tests/Paramore.Brighter.Core.Tests/OutboxNotifications"
    - Test file: `When_a_thousand_notifications_reach_a_channel_of_ten_should_hold_ten_and_drop_the_rest.cs` (AC-36)
    - **USE COMMAND**: `/tdd:test-first when a thousand notifications reach a channel of ten should hold ten and drop the rest`
    - Test should verify:
      - A later read returns exactly 10, and `DroppedCount` is 990.
      - By reflection, `OutboxNotification` has no member of type `Message` or `MessageBody`.
    - 🔁 Characterisation — green on first run: the buffer is bounded. RED mutation: use `Channel.CreateUnbounded`. Confirm the test fails on the count read.
    - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE once RED is observed — before committing if the test was green on arrival, before implementing if it was not** *(fires in the `review-before` gear, which is the default)*
  - **18.4 — Each publication drops on its own**
    - Test location: "tests/Paramore.Brighter.Core.Tests/OutboxNotifications"
    - Test file: `When_two_publications_fill_their_channels_should_count_drops_per_publication.cs` (AC-57, first part)
    - **USE COMMAND**: `/tdd:test-first when two publications fill their channels should count drops per publication`
    - Test should verify:
      - The `invoices` channel holds only `m3`, and the `orders` channel holds one notification.
      - The dropped counts are 1 for `orders` and 0 for `invoices`.
    - 🔁 Characterisation — green on first run: each channel has its own counter. RED mutation: make the dropped counter a `static` field shared by every channel. Confirm the test fails on the `invoices` dropped count.
    - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE once RED is observed — before committing if the test was green on arrival, before implementing if it was not** *(fires in the `review-before` gear, which is the default)*
  - **18.5 — A read notification is consumed**
    - Test location: "tests/Paramore.Brighter.Core.Tests/OutboxNotifications"
    - Test file: `When_a_subscriber_reads_again_with_no_further_failure_should_not_receive_the_notification_again.cs` (AC-22)
    - **USE COMMAND**: `/tdd:test-first when a subscriber reads again with no further failure should not receive the notification again`
    - Test should verify: a second bounded read gets nothing, and `m1` is still returned by `OutstandingMessagesAsync`.
    - 🔁 Characterisation — green on first run: `TryRead` removes the item. RED mutation: use `TryPeek` in the iterator. Confirm the test fails on the second read.
    - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE once RED is observed — before committing if the test was green on arrival, before implementing if it was not** *(fires in the `review-before` gear, which is the default)*
  - **18.6 — Order follows raising**
    - Test location: "tests/Paramore.Brighter.Core.Tests/OutboxNotifications"
    - Test file: `When_three_messages_fail_in_turn_should_deliver_them_in_that_order.cs` (AC-25)
    - **USE COMMAND**: `/tdd:test-first when three messages fail in turn should deliver them in that order`
    - Test should verify: the reader gets `m1`, `m2`, `m3`.
    - 🔁 Characterisation — green on first run: the channel is first in, first out. RED mutation: yield the buffered items newest first. Confirm the test fails on the order.
    - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE once RED is observed — before committing if the test was green on arrival, before implementing if it was not** *(fires in the `review-before` gear, which is the default)*
  - **18.7 — A late subscriber receives what was buffered**
    - Test location: "tests/Paramore.Brighter.Core.Tests/OutboxNotifications"
    - Test file: `When_a_subscriber_starts_after_two_failures_should_receive_both.cs` (AC-26)
    - **USE COMMAND**: `/tdd:test-first when a subscriber starts after two failures should receive both`
    - Test should verify: both notifications are read.
    - 🔁 Characterisation — green on first run: the buffer holds items until read. RED mutation: drain the buffer when the first `ReadAllAsync` starts. Confirm the test fails on the count.
    - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE once RED is observed — before committing if the test was green on arrival, before implementing if it was not** *(fires in the `review-before` gear, which is the default)*
  - **18.8 — A subscriber that throws and ends leaves the channel as if absent**
    - Test location: "tests/Paramore.Brighter.Core.Tests/OutboxNotifications"
    - Test file: `When_a_subscribers_read_loop_throws_and_ends_should_leave_clears_unaffected.cs` (AC-27)
    - **USE COMMAND**: `/tdd:test-first when a subscriber's read loop throws and ends should leave clears unaffected`
    - Test should verify: a further failed clear returns, and a new reader gets it, with drops as in AC-23.
    - 🔁 Characterisation — green on first run: the iterator holds no state that ends the channel. RED mutation: complete the channel when an enumerator is disposed. Confirm the test fails on the new reader getting nothing.
    - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE once RED is observed — before committing if the test was green on arrival, before implementing if it was not** *(fires in the `review-before` gear, which is the default)*
  - **18.9 — Raising never blocks on a stuck subscriber**
    - Test location: "tests/Paramore.Brighter.Core.Tests/OutboxNotifications"
    - Test file: `When_a_thousand_clears_fail_with_a_blocked_subscriber_should_all_return_within_thirty_seconds.cs` (AC-28)
    - **USE COMMAND**: `/tdd:test-first when a thousand clears fail with a blocked subscriber should all return within thirty seconds`
    - Test should verify: with the channel at capacity 1, as AC-28 says, all 1,000 return within 30 s of wall-clock time, and `DroppedCount` is at least 998.
    - 🔁 Characterisation — green on first run: drop-oldest never waits. RED mutation: use `BoundedChannelFullMode.Wait` with a blocking `WriteAsync(...).AsTask().Wait()`. Confirm the test fails on its time bound.
    - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE once RED is observed — before committing if the test was green on arrival, before implementing if it was not** *(fires in the `review-before` gear, which is the default)*
  - **18.10 — Subscriber code never runs on the raising thread**
    - Test location: "tests/Paramore.Brighter.Core.Tests/OutboxNotifications"
    - Test files (AC-29):
      - `When_a_waiting_subscriber_blocks_after_its_read_should_let_the_clear_return.cs`
      - `When_a_waiting_subscriber_blocks_after_its_read_should_let_a_confirming_producer_regain_control.cs`
    - **USE COMMAND**: `/tdd:test-first when a waiting subscriber blocks after its read should let the clear return` (and the matching phrase for the second file)
    - Test should verify: the clear, or the producer's raising call, returns while the subscriber is still blocked.
    - 🔁 Characterisation — green on first run: `AllowSynchronousContinuations` is false. RED mutation: set it to true. Confirm each test fails on its bounded wait.
    - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE once RED is observed — before committing if the test was green on arrival, before implementing if it was not** *(fires in the `review-before` gear, which is the default)*
  - **18.11 — The channel is safe under concurrent raising**
    - Test location: "tests/Paramore.Brighter.Core.Tests/OutboxNotifications"
    - Test file: `When_eight_threads_each_raise_a_thousand_failures_should_deliver_eight_thousand_distinct_ids.cs` (AC-37)
    - **USE COMMAND**: `/tdd:test-first when eight threads each raise a thousand failures should deliver eight thousand distinct ids`
    - Test setup: `Capacity` 8,000, as AC-37 says. The reader starts only after all eight writers have finished.
    - Test should verify: 8,000 distinct ids are read, and `DroppedCount` is 0.
    - 🔁 Characterisation — green on first run: the bounded channel locks its writes. RED mutation: cap the buffer at 4,096 whatever `Capacity` is set to. Because nothing is read until the writers finish, 8,000 raises must drop. Confirm the test fails on the count of distinct ids. The mutation pins the "nothing lost" assertion. The concurrency itself is exercised by the eight writers on every run.
    - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE once RED is observed — before committing if the test was green on arrival, before implementing if it was not** *(fires in the `review-before` gear, which is the default)*
  - **18.12 — A notification is available without the clock advancing**
    - Test location: "tests/Paramore.Brighter.Core.Tests/OutboxNotifications"
    - Test file: `When_a_clear_fails_with_a_subscriber_waiting_should_deliver_without_the_clock_advancing.cs` (AC-38)
    - **USE COMMAND**: `/tdd:test-first when a clear fails with a subscriber waiting should deliver without the clock advancing`
    - Test should verify: the notification arrives within 5 s of wall-clock time, with `FakeTimeProvider` never advanced.
    - 🔁 Characterisation — green on first run: the reader wakes on the write. RED mutation: make the iterator poll with `Task.Delay` on the `TimeProvider`. Confirm the test fails on its time bound.
    - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE once RED is observed — before committing if the test was green on arrival, before implementing if it was not** *(fires in the `review-before` gear, which is the default)*
  - **18.13 — Two readers of one channel compete, and neither is rejected**
    - Test location: "tests/Paramore.Brighter.Core.Tests/OutboxNotifications"
    - Test file: `When_two_readers_read_one_channel_should_deliver_each_notification_to_one_of_them.cs` (P-5)
    - **USE COMMAND**: `/tdd:test-first when two readers read one channel should deliver each notification to one of them`
    - Test should verify: with two readers on `orders` and three failures, the readers receive three notifications between them, each id once, and neither read throws.
    - 🔁 Characterisation — green on first run: the buffer allows several readers. RED mutation: make `ReadAllAsync` throw `InvalidOperationException` while another enumeration of the channel is active. Confirm the test fails on the second reader throwing.
    - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE once RED is observed — before committing if the test was green on arrival, before implementing if it was not** *(fires in the `review-before` gear, which is the default)*

## Phase 6: Max age on the sweep and in `InMemoryOutbox`
ADR: steps 8, 9 and 10; Key Components `Publication.MaxAge` and `OutboxMaxAges`; test group "Max age on the sweep"; further test 8

- [ ] **TEST + IMPLEMENT: 19 — Max ages are grouped per topic, smallest first, and non-positive ages mean no limit**
  - Depends on: none
  - Test location: "tests/Paramore.Brighter.Core.Tests/OutboxMaxAge"
  - Test files, in order:
    1. `When_two_publications_on_a_topic_set_max_ages_should_take_the_smallest.cs` (FR-17)
       - **USE COMMAND**: `/tdd:test-first when two publications on a topic set max ages should take the smallest`
    2. `When_max_ages_are_null_zero_or_negative_should_be_empty.cs` (FR-17)
       - **USE COMMAND**: `/tdd:test-first when max ages are null zero or negative should be empty`
    3. `When_max_ages_are_built_from_null_publications_should_throw.cs`
       - **USE COMMAND**: `/tdd:test-first when max ages are built from null publications should throw`
  - Test 3 is expected green on arrival, because the grouping modelled on `BuildTopicAges` starts with a LINQ `Where`, which already throws `ArgumentNullException` for a null source. Pin it with the RED mutation "skip a null publications argument and return an empty `OutboxMaxAges`" and confirm test 3 fails on nothing being thrown, then revert it and stop at the gate.
  - Test should verify:
    - `OutboxMaxAges.From` gives one entry per `Publication.Topic`, with the smallest positive age.
    - Null, zero and negative ages give `IsEmpty`.
    - Null publications throw `ArgumentNullException`.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - Add `TimeSpan? MaxAge` beside `StaleMessageAge` in `Publication`.
    - Add `OutboxMaxAges` with:
      - `ExcludeExpiredArgsKey` and `OnlyExpiredArgsKey`;
      - `From`, `ByTopic` and `IsEmpty`.
    - Group as `TimedOutboxStaleMessageMonitor.BuildTopicAges` does.

- [ ] **TEST + IMPLEMENT: 20 — A sweep leaves out messages past their topic's max age**
  - Depends on: 19, 7
  - Test location: "tests/Paramore.Brighter.Core.Tests/OutboxMaxAge"
  - Test files, in order:
    1. `When_a_sweep_runs_after_a_messages_max_age_should_not_send_it_and_keep_counting_it_outstanding.cs` (AC-46: `useBulk` false at 12:10:01, then 12:10:06)
       - **USE COMMAND**: `/tdd:test-first when a sweep runs after a message's max age should not send it and keep counting it outstanding`
    2. `When_a_message_is_written_late_with_an_old_stamp_should_expire_by_its_stamp.cs` (FR-17)
       - **USE COMMAND**: `/tdd:test-first when a message is written late with an old stamp should expire by its stamp`
    3. `When_a_sweep_runs_without_a_registry_after_a_messages_max_age_should_not_send_it.cs` (FR-17, with the feature off)
       - **USE COMMAND**: `/tdd:test-first when a sweep runs without a registry after a message's max age should not send it`
  - Test 2 is expected green on arrival, because test 1's code already filters on `MessageHeader.TimeStamp`. Pin it with the RED mutation "filter on the time the outbox wrote the entry" and confirm test 2 fails on `m1` being sent, then revert it and stop at the gate.
  - Test 3 is RED if test 1's code reads the ages only when a registry is given. If it is green on arrival, pin it with the RED mutation "read the ages only when a registry is given" and confirm test 3 fails on `m1` being sent, then revert it and stop at the gate.
  - Test should verify:
    - `m1` is not sent, is still undispatched, and raises no notification.
    - The 12:10:06 sweep again does not send it, and the outbox's outstanding count still includes it.
    - Expiry follows `MessageHeader.TimeStamp`, not the time of the write.
    - With no registry, `m1` is still left out of the sweep.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - Have the mediator read `OutboxMaxAges.From` once, at construction, whether or not a registry is given, which test 3 needs.
    - In `BackgroundDispatchUsingAsync`, when the ages are not empty, copy `args` and add `ExcludeExpiredArgsKey` before the `OutstandingMessagesAsync` fetch.
    - In `InMemoryOutbox.OutstandingMessages`, read the key and filter on `MessageHeader.TimeStamp < now − age`, strictly. The async method keeps delegating to the sync one.

- [ ] **TEST + IMPLEMENT: 21 — `InMemoryOutbox` returns only one topic's expired messages on request**
  - Depends on: 20
  - Test location: "tests/Paramore.Brighter.Core.Tests/OutboxMaxAge"
  - Test files, in order:
    1. `When_in_memory_outbox_is_asked_for_only_expired_messages_should_return_only_that_topics_expired_messages.cs` (FR-19)
       - **USE COMMAND**: `/tdd:test-first when in memory outbox is asked for only expired messages should return only that topic's expired messages`
    2. `When_in_memory_outbox_is_asked_for_only_expired_messages_of_two_topics_should_throw.cs`
       - **USE COMMAND**: `/tdd:test-first when in memory outbox is asked for only expired messages of two topics should throw`
    3. `When_in_memory_outbox_is_asked_for_only_expired_messages_with_a_huge_age_should_return_an_empty_page.cs`
       - **USE COMMAND**: `/tdd:test-first when in memory outbox is asked for only expired messages with a huge age should return an empty page`
    4. `When_in_memory_outbox_excludes_expired_messages_with_a_huge_age_should_add_no_bound_and_not_throw.cs`
       - **USE COMMAND**: `/tdd:test-first when in memory outbox excludes expired messages with a huge age should add no bound and not throw`
  - Test 4 is expected green on arrival, because the cutoff guard from test 3 is shared. Pin it with the RED mutation "drop the guard from the exclude path only" and confirm test 4 fails on the exception, then revert it and stop at the gate.
  - Test should verify:
    - Only expired messages of the named topic come back.
    - More than one topic under `OnlyExpiredArgsKey` throws `ArgumentException`.
    - An age whose cutoff falls before `DateTimeOffset.MinValue` gives an empty page for only-expired, and no bound and no exception for exclude.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - Read `OnlyExpiredArgsKey` beside the exclude key in `InMemoryOutbox.OutstandingMessages`.
    - Compute the cutoff with a guard against underflow.

- [ ] **CHARACTERISE: 22 — Born-green tests for max age on the sweep and in `InMemoryOutbox`**
  - Depends on: 1, 14, 20
  - 9 items, in order. Each item, and each file of an item that lists several, goes through its own `/tdd:test-first`, its own RED mutation and its own approval gate, as the CHARACTERISE rules in the header say. The task ends in one `test:` commit.
  - **22.1 — A message exactly at its max age is still sent**
    - Test location: "tests/Paramore.Brighter.Core.Tests/OutboxMaxAge"
    - Test file: `When_a_sweep_runs_exactly_at_a_messages_max_age_should_send_it.cs` (AC-46, first part)
    - **USE COMMAND**: `/tdd:test-first when a sweep runs exactly at a message's max age should send it`
    - Test should verify: at 12:10:00, `m1` is sent.
    - 🔁 Characterisation — green on first run: the comparison is strict. RED mutation: use `<=`. Confirm the test fails on `m1` not sent.
    - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE once RED is observed — before committing if the test was green on arrival, before implementing if it was not** *(fires in the `review-before` gear, which is the default)*
  - **22.2 — A bulk sweep leaves out expired messages too**
    - Test location: "tests/Paramore.Brighter.Core.Tests/OutboxMaxAge"
    - Test file: `When_a_bulk_sweep_runs_after_a_messages_max_age_should_not_send_it.cs` (AC-46, `useBulk` true)
    - **USE COMMAND**: `/tdd:test-first when a bulk sweep runs after a message's max age should not send it`
    - Test should verify: `m1` is not sent and is still undispatched.
    - 🔁 Characterisation — green on first run: the key is added before the shared fetch. RED mutation: add the key only on the non-bulk path. Confirm the test fails on `m1` sent.
    - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE once RED is observed — before committing if the test was green on arrival, before implementing if it was not** *(fires in the `review-before` gear, which is the default)*
  - **22.3 — An expired backlog does not hide an unexpired message from the page**
    - Test location: "tests/Paramore.Brighter.Core.Tests/OutboxMaxAge"
    - Test file: `When_an_expired_backlog_fills_a_sweep_page_should_still_send_the_unexpired_message.cs` (AC-56, `InMemoryOutbox`)
    - **USE COMMAND**: `/tdd:test-first when an expired backlog fills a sweep page should still send the unexpired message`
    - Test should verify: with batch size 10, `m1` is sent and none of the 20 expired messages is.
    - 🔁 Characterisation — green on first run: the store filters before paging (D-22). RED mutation: filter the page in memory after the store returns it. Confirm the test fails on `m1` not sent.
    - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE once RED is observed — before committing if the test was green on arrival, before implementing if it was not** *(fires in the `review-before` gear, which is the default)*
  - **22.4 — A topic uses the smallest age of its publications**
    - Test location: "tests/Paramore.Brighter.Core.Tests/OutboxMaxAge"
    - Test file: `When_typed_publications_on_a_topic_have_different_max_ages_should_apply_the_smallest_to_both.cs` (AC-47, first part)
    - **USE COMMAND**: `/tdd:test-first when typed publications on a topic have different max ages should apply the smallest to both`
    - Test should verify: at 12:10:01, neither `m1` (type `A`) nor `m2` (type `B`) is sent.
    - 🔁 Characterisation — green on first run: ages are grouped by topic. RED mutation: key ages per publication key (topic and type). Confirm the test fails on `m2` sent.
    - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE once RED is observed — before committing if the test was green on arrival, before implementing if it was not** *(fires in the `review-before` gear, which is the default)*
  - **22.5 — A mapper's new topic decides the max age**
    - Test location: "tests/Paramore.Brighter.Core.Tests/OutboxMaxAge"
    - Test file: `When_a_mapper_moves_a_message_to_a_topic_with_no_max_age_should_still_sweep_it.cs` (AC-47, second part, sweep)
    - **USE COMMAND**: `/tdd:test-first when a mapper moves a message to a topic with no max age should still sweep it`
    - Test should verify: `m3` on `orders.eu` is sent through the producer of `orders`.
    - 🔁 Characterisation — green on first run: ages apply by stored topic. RED mutation: apply ages by `Message.ProducerLookupTopic()`. Confirm the test fails on `m3` not sent.
    - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE once RED is observed — before committing if the test was green on arrival, before implementing if it was not** *(fires in the `review-before` gear, which is the default)*
  - **22.6 — An explicit clear sends whatever the age**
    - Test location: "tests/Paramore.Brighter.Core.Tests/OutboxMaxAge"
    - Test files: `When_clearing_an_expired_message_explicitly_should_send_it.cs` and `When_clearing_an_expired_message_explicitly_async_should_send_it.cs` (AC-48)
    - **USE COMMAND**: `/tdd:test-first when clearing an expired message explicitly should send it` (once per file)
    - Test should verify: at 12:10:01, `m1` is sent each time.
    - 🔁 Characterisation — green on first run: only the sweep adds the key. RED mutation: filter expired ids out in the explicit clear. Confirm each test fails on `m1` not sent.
    - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE once RED is observed — before committing if the test was green on arrival, before implementing if it was not** *(fires in the `review-before` gear, which is the default)*
  - **22.7 — A sweep leaves the caller's arguments unchanged**
    - Test location: "tests/Paramore.Brighter.Core.Tests/OutboxMaxAge"
    - Test file: `When_a_sweep_is_given_caller_args_should_leave_the_callers_dictionary_unchanged.cs` (ADR further test 8)
    - **USE COMMAND**: `/tdd:test-first when a sweep is given caller args should leave the caller's dictionary unchanged`
    - Test should verify: the caller's dictionary has the same keys and values after the sweep.
    - 🔁 Characterisation — green on first run: the sweep copies `args`. RED mutation: add the key to the caller's dictionary in place. Confirm the test fails on the extra key.
    - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE once RED is observed — before committing if the test was green on arrival, before implementing if it was not** *(fires in the `review-before` gear, which is the default)*
  - **22.8 — A replayed causation message keeps its stamp, so an old one is left out of sweeps**
    - Test location: "tests/Paramore.Brighter.Core.Tests/OutboxMaxAge"
    - Test file: `When_a_causation_replay_restores_a_message_older_than_its_max_age_should_leave_it_out_of_the_sweep.cs` (release-notes behaviour line; `InMemoryOutbox` implements `IAmACausationTrackingOutbox`)
    - **USE COMMAND**: `/tdd:test-first when a causation replay restores a message older than its max age should leave it out of the sweep`
    - Test should verify: after `ReplayCausation`, the replayed message keeps its `MessageHeader.TimeStamp`, and the sweep does not send it.
    - 🔁 Characterisation — green on first run: replay does not touch the stamp. RED mutation: in `InMemoryOutbox.ReplayCausation`, replace the replayed entry with a new `OutboxEntry` over a message whose header has `TimeStamp = now`, keeping the entry's `WriteTime`. The header is `init`-only, so it cannot be assigned. Confirm the test fails on the stamp assertion.
    - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE once RED is observed — before committing if the test was green on arrival, before implementing if it was not** *(fires in the `review-before` gear, which is the default)*
  - **22.9 — A max age of zero changes nothing on the publish path**
    - Test location: "tests/Paramore.Brighter.Core.Tests/OutboxMaxAge"
    - Test file: `When_a_max_age_of_zero_is_set_should_make_identical_breaker_and_meter_calls.cs` (AC-20, last part; join the log collection)
    - **USE COMMAND**: `/tdd:test-first when a max age of zero is set should make identical breaker and meter calls`
    - Test should verify: the breaker and meter comparison of task 14.5, with a zero age on `orders`. The five runs of 14.5 that can be swept (async send, batch, confirmation, mark and success) run as sweeps with a `minimumAge` of zero: the batch run as a bulk sweep, the others as non-bulk sweeps. The failed sync send stays an explicit `ClearOutbox`, because there is no sync sweep and a max age never applies to an explicit clear (22.6). The outbox is an `InMemoryOutbox` on the same `FakeTimeProvider` as the mediator, and each message carries an explicit `MessageHeader.TimeStamp` one second before its now, because the header defaults to `UtcNow`.
    - 🔁 Characterisation — green on first run: only positive ages count. RED mutation: count any age with `HasValue`. The sweeps then skip the stamped messages as expired. Confirm the test fails on the breaker and meter calls of the zero-age run differing from those of a run with no max age.
    - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE once RED is observed — before committing if the test was green on arrival, before implementing if it was not** *(fires in the `review-before` gear, which is the default)*

## Phase 7: Store queries
ADR: steps 11, 12 and 13; Key Component `IRelationalDatabaseOutboxExpiryQueries`; test group "Store queries"; P-7; FR-18 departure on DynamoDB

- [ ] **GENERATE: 23 — Two outbox templates for the max-age bounds, seen failing on every store**
  - Depends on: 21
  - GENERATED TEST — do NOT use `/tdd:test-first`.
  - This is the first task that touches a real backend:
    - every store suite runs against its real backend in Docker, through the repository's compose files (`docker-compose-mssql.yaml`, `-mysql`, `-postgres`, `-mongodb`, `-dynamodb`, `-spanner`);
    - Sqlite runs on a local file;
    - Firestore runs against the GCP project the CI uses.
  - Template location: `tools/Paramore.Brighter.Test.Generator/Templates/Outbox/Sync` and `Async`, beside `When_Retrieving_Outstanding_Messages_It_Should_Filter_By_Age`
  - Template files (they follow the generator's existing naming):
    - `When_Retrieving_Outstanding_Messages_It_Should_Exclude_Expired_Messages.cs.liquid` and `_Async.cs.liquid` (FR-18, AC-56)
    - `When_Retrieving_Outstanding_Messages_For_Expiry_It_Should_Return_Only_A_Topics_Expired_Messages.cs.liquid` and `_Async.cs.liquid` (FR-19, AC-50)
  - Cases each template holds:
    - Two topics of different ages. An expired backlog on one must not hide the other's unexpired message from a page (AC-56).
    - A huge age.
    - In the only-expired template, two topics under `OnlyExpiredArgsKey` throw `ArgumentException` (ADR, `OnlyExpiredArgsKey` row).
    - In the exclude template, with `"Topic"`, every topic seeded for the case has a max age shorter than the minimum age, and its messages are older than the minimum age. The page holds none of the case's messages, and nothing throws. On DynamoDB the cutoff is then not earlier than now minus the minimum age, so the store must answer without a query (ADR, DynamoDB per-topic path).
    - An expired message whose partition key hashes to a negative shard at the default three shards.
    - The only-expired template passes `"Topic"` as the archiver does. The exclude template has a case with `"Topic"`, so DynamoDB and MongoDb run their per-topic path.
    - Stamps are relative to the time the test runs, with the criteria's intervals.
  - Then:
    - Build the generator and regenerate from each test-project directory, or run `./generate-test.sh`.
    - Check the base classes in `tests/Paramore.Brighter.Base.Test/Outbox/OutboxTest.cs` and `OutboxAsyncTest.cs`.
    - Keep the generated-tree audit green.
    - Run each store suite. Every new generated test fails because no store reads the keys yet. Each template holds one test method that asserts all its cases together, as the existing Sync and Async outbox templates do, so the huge-age case of the exclude template cannot pass alone.
    - Commit the templates and the regenerated files as `test:`.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*

- [ ] **TEST + IMPLEMENT: 24 — Relational outboxes compose the expiry role, and Sqlite applies it**
  - Depends on: 23
  - Runs against: Sqlite on a local file.
  - Test location: "tests/Paramore.Brighter.Extensions.Tests/Outbox" for the hand-written test (reuse `StubSqlOutbox` and `StubRelationDatabaseOutboxQueries` from `TestDoubles`; the stub queries lack the role)
  - Test files, in order:
    1. `When_a_relational_dialect_lacks_the_expiry_role_should_throw_not_supported_for_an_expiry_key.cs` (release-notes behaviour line, third-party dialect)
       - **USE COMMAND**: `/tdd:test-first when a relational dialect lacks the expiry role should throw not supported for an expiry key`
    2. Sqlite generated files from task 23, in `tests/Paramore.Brighter.Sqlite.Tests/Outbox/Binary` and `Text` (AC-50, AC-56). GENERATED — no `/tdd:test-first`; RED was seen in task 23.
  - Test should verify:
    - With either key and no role, `OutstandingMessages` and `OutstandingMessagesAsync` throw `NotSupportedException` before touching the store.
    - The generated Sqlite tests pass.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - Add `IRelationalDatabaseOutboxExpiryQueries` (`ExpiredMessageFragment`, `EarliestTimestamp`), following `IRelationalDatabaseOutboxTopicCountQueries`.
    - In `RelationDatabaseOutbox`, compose the fragments next to `GeneratePagedOutstandingCommandInStatementAndAddParameters`, into the same `{1}` slot:
      - join with OR as `AND NOT(...)` for exclude and `AND (...)` for only-expired, NULL-safe;
      - take the cutoff from `DateTimeOffset.UtcNow`, as `CreatePagedOutstandingParameters` does;
      - create the cutoff with `CreateSqlParameter(name, DbType.DateTimeOffset, cutoff)` and the topic with `DbType.String`;
      - a cutoff before `EarliestTimestamp` adds no fragment for exclude and gives an empty page for only-expired;
      - with no fragments the command gains no clause;
      - throw `ArgumentException` when `OnlyExpiredArgsKey` holds more than one topic, after the role check.
    - Throw `NotSupportedException` without the role, as `GetOutstandingMessageCountsByTopicAsync` does. Check the role at the top of `OutstandingMessages` and `OutstandingMessagesAsync`, before `ReadFromStore`, so the stub's connection provider and `CreateSqlParameter` are never called. Check it only when `args` carries either expiry key (the ADR's "given a bound"), so callers without a key are unchanged.
    - Implement the role in `SqliteQueries`.

- [ ] **TEST + IMPLEMENT: 25 — MsSql, MySql, PostgreSql and Spanner apply the expiry role**
  - Depends on: 24
  - Runs against: the real backends in Docker (`docker-compose-mssql.yaml`, `docker-compose-mysql.yaml`, `docker-compose-postgres.yaml`, `docker-compose-spanner.yaml`).
  - Test files: the generated files from task 23 (AC-50, AC-56) in:
    - `tests/Paramore.Brighter.MSSQL.Tests/Outbox/Binary` and `Text`;
    - `tests/Paramore.Brighter.MySQL.Tests/Outbox/Binary` and `Text`;
    - `tests/Paramore.Brighter.PostgresSQL.Tests/Outbox/Binary` and `Text`;
    - `tests/Paramore.Brighter.Gcp.Tests/Outbox/SpannerBinary` and `SpannerText`.

    GENERATED — no `/tdd:test-first`; RED was seen in task 23. Make them pass one dialect at a time, MsSql first.
  - Test should verify: every generated test passes on each dialect.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - Implement `IRelationalDatabaseOutboxExpiryQueries` in `MsSqlQueries`, `MySqlQueries`, `PostgreSqlQueries` and `SpannerQueries`.
    - MsSql's paged command holds `{1}` twice, so its fragment appears twice and one cutoff parameter serves both.

- [ ] **TEST + IMPLEMENT: 26 — DynamoDB and DynamoDB.V4 apply both bounds**
  - Depends on: 23
  - Runs against: DynamoDB Local in Docker (`docker-compose-dynamodb.yaml`).
  - Test files: the generated files from task 23 in `tests/Paramore.Brighter.DynamoDB.Tests/Outbox/Generated` and the matching folder of `tests/Paramore.Brighter.DynamoDB.V4.Tests` (AC-50, AC-56, the negative-shard case). GENERATED — no `/tdd:test-first`; RED was seen in task 23.
  - Test should verify: every generated test passes on both packages. The existing `When_retrieving_outstanding_messages_should_exclude_tripped_topics` stays green.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - Read both keys beside the `"Topic"` read in `OutstandingMessagesAsync` of each `DynamoDbOutbox`.
    - On the per-topic path (`OutstandingMessagesForTopicAsync`), put the bound in the key condition: a `BETWEEN` beside `KeyTopicOutstandingCreatedTimeExpression`, from the cutoff to one tick below now minus the minimum age, so the upper bound stays strict. With `OnlyExpiredArgsKey` the upper bound is one tick below the cutoff.
    - With `OnlyExpiredArgsKey`, read the shards from `-(NumberOfShards - 1)`.
    - Throw `ArgumentException` when `OnlyExpiredArgsKey` holds more than one topic.
    - With `ExcludeExpiredArgsKey` on the per-topic path, when the cutoff is not earlier than now minus the minimum age, return an empty page for the topic without a query.
    - On the scan (`ScanOutstandingIndexSegmentForMessages`), apply the bound in the same `Where` as the tripped-topic filter. The short page while a topic is tripped is the accepted departure from FR-18.

- [ ] **TEST + IMPLEMENT: 27 — MongoDb applies both bounds**
  - Depends on: 23
  - Runs against: MongoDb in Docker (`docker-compose-mongodb.yaml`).
  - Test files: the generated files from task 23 in `tests/Paramore.Brighter.MongoDb.Tests/Outbox/Generated` (AC-50, AC-56). GENERATED — no `/tdd:test-first`; RED was seen in task 23.
  - Test should verify: every generated test passes, sync and async.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should: read both keys beside the `"Topic"` read in `OutstandingMessages` and `OutstandingMessagesAsync` of `MongoDbOutbox`, add the bound to the server-side filter, and throw `ArgumentException` when `OnlyExpiredArgsKey` holds more than one topic.

- [ ] **PROJECT: 28 — Create the Firestore composite index in the CI's GCP project**
  - Depends on: 23
  - Needed by: 29
  - Ask the maintainer to create the composite index (IsDispatched, Topic, TimeStamp) on the outbox collection in the GCP project the CI uses. Record in the PR description that it exists.
  - Mitigates: the only-expired query failing in CI for a missing index, and not for a defect.

- [ ] **TEST + IMPLEMENT: 29 — Firestore applies both bounds**
  - Depends on: 23, 28
  - Runs against: the GCP project the CI uses.
  - Test files: the generated files from task 23 in `tests/Paramore.Brighter.Gcp.Tests/Outbox/Firestore` (AC-50, AC-56). GENERATED — no `/tdd:test-first`; RED was seen in task 23.
  - Test should verify: every generated test passes.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - In `FirestoreOutbox.OutstandingMessagesAsync`, query by topic and stamp for `OnlyExpiredArgsKey`, and throw `ArgumentException` when it holds more than one topic.
    - For `ExcludeExpiredArgsKey`, filter the page in memory and read further pages until it is full or the store is exhausted (P-7 as changed).

## Phase 8: The archiver moves expired messages and raises `Expired`
ADR: step 14; Key Component `Message.ExpiredAtHeaderName`; section "Fallback channel"; test group "Archiver"; further tests 9 and 10

- [ ] **TEST + IMPLEMENT: 30 — The archiver marks and moves expired messages after the dispatched pass**
  - Depends on: 3, 21
  - Test location: "tests/Paramore.Brighter.Core.Tests/Archiving"
  - Test files, in order:
    1. `When_archiving_with_a_max_age_async_should_move_expired_messages_marked_as_expired.cs` (AC-49, async; reuse `InMemoryArchiveProvider`, which keeps each archived `Message`, bag included, in `ArchivedMessages`)
       - **USE COMMAND**: `/tdd:test-first when archiving with a max age async should move expired messages marked as expired`
    2. `When_archiving_with_a_max_age_should_move_expired_messages_marked_as_expired.cs` (AC-49, sync)
       - **USE COMMAND**: `/tdd:test-first when archiving with a max age should move expired messages marked as expired`
    3. `When_a_store_ignores_the_only_expired_key_should_move_and_delete_nothing_that_is_not_expired.cs` (ADR further test 9; new double `ExpiryIgnoringOutbox`)
       - **USE COMMAND**: `/tdd:test-first when a store ignores the only expired key should move and delete nothing that is not expired`
  - Test should verify:
    - The provider receives `m1` with `Message.ExpiredAtHeaderName` in the bag, formatted "O", and `d1` without it.
    - `m1` and `d1` leave the outbox, and `m2`, `a1` and `b1` stay undispatched.
    - With a store that ignores the key, nothing unexpired is moved or deleted.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - Add `Message.ExpiredAtHeaderName`.
    - Add two trailing optional parameters after `instrumentationOptions`: `IAmAProducerRegistry? producerRegistry` and `TimeProvider? timeProvider`. The ages come from the producer registry's publications. Task 31 adds `notifications` between them, so the tests of both tasks pass these parameters by name.
    - Add the expired pass after the dispatched pass. Per topic it queries with `OnlyExpiredArgsKey`, `dispatchedSince` zero, page 1 and `"Topic"` = `topic.Value`, then sets the bag entry and archives each message.
    - Write the first test's code to trust the page. The in-memory check of stored topic and age is the third test's code.
    - Delete the archived ids in one call.

- [ ] **TEST + IMPLEMENT: 31 — The archiver raises `Expired` on the channel that sent the message, with a fallback**
  - Depends on: 1, 17, 30
  - Test location: "tests/Paramore.Brighter.Core.Tests/Archiving"
  - Test files, in order:
    1. `When_archiving_an_expired_message_async_should_raise_expired_on_its_publications_channel.cs` (AC-49 notification, FR-20)
       - **USE COMMAND**: `/tdd:test-first when archiving an expired message async should raise expired on its publication's channel`
    2. `When_archiving_an_expired_message_should_raise_expired_on_its_publications_channel.cs` (sync)
       - **USE COMMAND**: `/tdd:test-first when archiving an expired message should raise expired on its publication's channel`
    3. `When_archiving_an_expired_message_of_a_typed_publication_should_raise_expired_on_that_types_channel.cs` (AC-58, second part)
       - **USE COMMAND**: `/tdd:test-first when archiving an expired message of a typed publication should raise expired on that type's channel`
    4. `When_an_expired_messages_key_has_no_publication_should_raise_on_the_publication_with_the_applied_age.cs` (fallback rule)
       - **USE COMMAND**: `/tdd:test-first when an expired message's key has no publication should raise on the publication with the applied age`
    5. `When_an_unattached_registry_drops_expired_notifications_should_still_warn.cs` (ADR: drop warnings use `TimeProvider.System` before `Attach`; join the log collection; the async archiver over a registry that no mediator has attached, capacity 1, two expired messages. With no clock on the channel, the drop delegate faults inside `Raise`, whose catch logs the exception type only, so this test is RED on the drop warning)
       - **USE COMMAND**: `/tdd:test-first when an unattached registry drops expired notifications should still warn`
    6. `When_the_archivers_producer_registry_has_no_publication_for_an_expired_message_should_raise_nothing.cs` (ADR: with another producer registry, step 2 finds no channel; the notification registry built from a producer registry whose only publication is on `payments`, the archiver given another whose only publication on `orders` sets the age; the test reads every channel from `Keys`)
       - **USE COMMAND**: `/tdd:test-first when the archiver's producer registry has no publication for an expired message should raise nothing`
  - Test 3 may be green on arrival, because test 1's code looks the channel up by topic and type. Pin it with the RED mutation "look the channel up with an empty type, the key `orders:`, and raise nothing when that key has no publication" and confirm test 3 fails on `A`'s channel receiving nothing, then revert it and stop at the gate.
  - Test 6 may be green on arrival, because test 4's code already raises nothing when the fallback finds no channel. Pin it with the RED mutation "when neither step finds a channel, raise on the channel of the registry's first key" and confirm test 6 fails on the count, then revert it and stop at the gate.
  - Test should verify:
    - The only notification is (`m1`, `orders`, `Expired`, null, 12:30:00), from the archiver's `TimeProvider`.
    - A typed message reaches `A`'s channel.
    - A message whose key matches no publication reaches the first publication, in ordinal `topic:type` order, on its stored topic whose own age equals the applied age.
    - With an unattached registry, the second `Expired` drops the first, and one drop `Warning` names the key and a dropped count of 1.
    - With another producer registry, `m1` is moved and no notification is raised on any channel.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - Add the optional `OutboxNotificationRegistry? notifications` parameter between `producerRegistry` and `timeProvider`, as the ADR's constructor orders them, and store it.
    - Raise after each message the provider accepts.
    - Route by (`Message.ProducerLookupTopic()`, `MessageHeader.Type`) first, then by the fallback, which test 4 needs. When neither finds a channel, raise nothing.
    - Start each channel with `TimeProvider.System`, which `Attach` replaces. Test 5 needs it.

- [ ] **TEST + IMPLEMENT: 32 — The archiver bounds each run and rotates its starting topic**
  - Depends on: 31
  - Test location: "tests/Paramore.Brighter.Core.Tests/Archiving"
  - Test files, in order:
    1. `When_more_expired_messages_than_the_batch_size_exist_should_move_a_batch_per_run_and_leave_other_topics.cs` (AC-50, `InMemoryOutbox`)
       - **USE COMMAND**: `/tdd:test-first when more expired messages than the batch size exist should move a batch per run and leave other topics`
    2. `When_several_topics_have_expired_messages_should_start_the_next_run_at_the_next_topic.cs` (ADR further test 10)
       - **USE COMMAND**: `/tdd:test-first when several topics have expired messages should start the next run at the next topic`
  - Test should verify:
    - With batch size 10, `m1` is moved and no `audit` message is. With 15 expired, the first run moves 10 and the next run moves 5.
    - With several expired topics, the second run starts at the next topic in ordinal order.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - Bound the expired messages moved per run by the batch size, across topics.
    - Keep a rotating start index over the ordinal-sorted topics.

- [ ] **CHARACTERISE: 33 — Born-green tests for the archiver's expired pass**
  - Depends on: 10-12, 16, 30, 31
  - 8 items, in order. Each item, and each file of an item that lists several, goes through its own `/tdd:test-first`, its own RED mutation and its own approval gate, as the CHARACTERISE rules in the header say. The task ends in one `test:` commit.
  - **33.1 — No positive max age means the archiver works as today**
    - Test location: "tests/Paramore.Brighter.Core.Tests/Archiving"
    - Test file: `When_no_positive_max_age_is_set_should_archive_as_today_and_let_the_sweep_send.cs` (AC-45; a theory over none, zero and -1 minute; reuse the public `CountingOutbox` in `tests/Paramore.Brighter.Core.Tests/Observability/TestDoubles`, which counts the outbox calls)
    - **USE COMMAND**: `/tdd:test-first when no positive max age is set should archive as today and let the sweep send`
    - Setup: `m1` carries a `MessageHeader.TimeStamp` one second before the fake now, and the outbox, the archiver and the sweep share one `FakeTimeProvider`, so an age of zero would make `m1` expired.
    - Test should verify: `m1` stays, the `OutstandingMessages` and `MarkDispatched` counts match a run with no `MaxAge` set, the sweep sends `m1`, and no `Expired` is raised.
    - 🔁 Characterisation — green on first run: `OutboxMaxAges.From` drops zero and negative ages, so the expired pass has no topic to query. RED mutation: keep zero and negative ages as entries in `OutboxMaxAges.From`. Confirm the zero and -1 rows fail on `m1` being moved. The "none" row has no age to keep, so it gets its own RED mutation, applied after the first is reverted: in `OutboxMaxAges.From`, give a topic whose publication has no `MaxAge` an entry with an age of zero. Confirm the "none" row fails on `m1` being moved.
    - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE once RED is observed — before committing if the test was green on arrival, before implementing if it was not** *(fires in the `review-before` gear, which is the default)*
  - **33.2 — The archiver judges by stored topic**
    - Test location: "tests/Paramore.Brighter.Core.Tests/Archiving"
    - Test file: `When_a_mapper_moves_a_message_to_a_topic_with_no_max_age_should_not_archive_it.cs` (AC-47, second part, archiver)
    - **USE COMMAND**: `/tdd:test-first when a mapper moves a message to a topic with no max age should not archive it`
    - Test should verify: `m3` on `orders.eu` stays in the outbox.
    - 🔁 Characterisation — green on first run: the pass groups ages by `Publication.Topic`. RED mutation: apply ages by `Message.ProducerLookupTopic()`. Confirm the test fails on `m3` moved.
    - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE once RED is observed — before committing if the test was green on arrival, before implementing if it was not** *(fires in the `review-before` gear, which is the default)*
  - **33.3 — `Expired` is raised for each accepted message, even if a later one fails**
    - Test location: "tests/Paramore.Brighter.Core.Tests/Archiving"
    - Test file: `When_the_archive_provider_fails_on_the_second_message_should_raise_expired_for_the_first_in_both_runs.cs` (AC-51; new double `ScriptedFailingArchiveProvider`, which keeps each message it accepts in a list, throws on a chosen message until the test turns the failure off, and accepts a message it already holds. `InMemoryArchiveProvider` does not fit, because its dictionary throws when the second run archives the first message again)
    - **USE COMMAND**: `/tdd:test-first when the archive provider fails on the second message should raise expired for the first in both runs`
    - Test should verify: `Expired` for the first in both runs and for the second once, and neither is in the outbox after the second run.
    - 🔁 Characterisation — green on first run: the raise follows each accepted archive. RED mutation: raise after the delete. Confirm the test fails on the first run's notifications.
    - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE once RED is observed — before committing if the test was green on arrival, before implementing if it was not** *(fires in the `review-before` gear, which is the default)*
  - **33.4 — With the feature off, or `Expired` not chosen, the archiver still moves expired messages**
    - Test location: "tests/Paramore.Brighter.Core.Tests/Archiving"
    - Test files:
      - `When_notifications_are_off_should_still_move_expired_messages.cs` (AC-52, first part) — mutation: skip the pass when `notifications` is null.
      - `When_expired_is_not_chosen_should_still_move_expired_messages_and_raise_nothing.cs` (FR-20, FR-22; `Kinds = SendFailed`, capacity 1, two expired messages) — mutation: skip the pass when `Kinds` leaves out `Expired`.
    - **USE COMMAND**: `/tdd:test-first when notifications are off should still move expired messages` (and the matching phrase for the second file)
    - Test should verify: `m1` is moved and nothing is raised. In the second file `DroppedCount` stays 0, which an archiver that raised the unchosen kind would break at capacity 1.
    - 🔁 Characterisation — green on first run: the pass depends on ages, not on the registry or the kinds. Confirm each mutation fails its test on `m1` still in the outbox.
    - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE once RED is observed — before committing if the test was green on arrival, before implementing if it was not** *(fires in the `review-before` gear, which is the default)*
  - **33.5 — A subscriber that does not read never holds up the archiver**
    - Test location: "tests/Paramore.Brighter.Core.Tests/Archiving"
    - Test file: `When_a_subscriber_does_not_read_should_complete_the_archive_run_and_count_drops.cs` (AC-52, second part)
    - **USE COMMAND**: `/tdd:test-first when a subscriber does not read should complete the archive run and count drops`
    - Test should verify: with capacity 1, a run moving 3 completes within a bounded wait, and `DroppedCount` is 2.
    - 🔁 Characterisation — green on first run: `Raise` never waits. RED mutation: await `WriteAsync` with `BoundedChannelFullMode.Wait` in the archiver's raise. Confirm the test fails on its time bound.
    - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE once RED is observed — before committing if the test was green on arrival, before implementing if it was not** *(fires in the `review-before` gear, which is the default)*
  - **33.6 — A sweep racing the archiver notifies `Expired`, then `MarkDispatchedFailed`**
    - Test location: "tests/Paramore.Brighter.Core.Tests/Archiving"
    - Test file: `When_the_archiver_moves_a_message_while_its_send_waits_should_raise_expired_then_mark_dispatched_failed.cs` (AC-53; reuse `FailingOutbox`, set to throw `E` from `MarkDispatchedAsync` as DynamoDB and Firestore do for a missing row, because `InMemoryOutbox` does not throw; new double `GatedProducerAsync`, whose `SendAsync` waits until the test releases it and then succeeds, which `TokenWaitingProducerAsync` cannot do)
    - **USE COMMAND**: `/tdd:test-first when the archiver moves a message while its send waits should raise expired then mark dispatched failed`
    - Test should verify: `Expired` then `MarkDispatchedFailed`, both for `m1`, and the producer received `m1`.
    - 🔁 Characterisation — green on first run: the mark site raises on any returned exception. RED mutation: skip the mark-site raise when the outbox no longer holds the id. Confirm the test fails on the second notification.
    - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE once RED is observed — before committing if the test was green on arrival, before implementing if it was not** *(fires in the `review-before` gear, which is the default)*
  - **33.7 — Only chosen kinds are raised, the archiver's included**
    - Test location: "tests/Paramore.Brighter.Core.Tests/Archiving"
    - Test file: `When_only_expired_is_chosen_should_raise_only_expired_and_drop_nothing.cs` (AC-55, first part)
    - **USE COMMAND**: `/tdd:test-first when only expired is chosen should raise only expired and drop nothing`
    - Test should verify: the only notification is `Expired` for `m2`, and `DroppedCount` is 0.
    - 🔁 Characterisation — green on first run: `Raise` checks `Kinds`. RED mutation: remove the kinds check from `Raise`. Confirm the test fails on a `SendFailed` read.
    - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE once RED is observed — before committing if the test was green on arrival, before implementing if it was not** *(fires in the `review-before` gear, which is the default)*
  - **33.8 — The default kinds raise one notification of each kind**
    - Test location: "tests/Paramore.Brighter.Core.Tests/Archiving"
    - Test file: `When_the_default_kinds_apply_should_raise_one_notification_of_each_kind.cs` (AC-55, third part)
    - **USE COMMAND**: `/tdd:test-first when the default kinds apply should raise one notification of each kind`
    - Test should verify: one each of `SendFailed`, `ConfirmationFailed`, `MarkDispatchedFailed` and `Expired`.
    - 🔁 Characterisation — green on first run: `Kinds` defaults to `All`. RED mutation: default `Kinds` to `All & ~Expired`. Confirm the test fails on the missing `Expired`.
    - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE once RED is observed — before committing if the test was green on arrival, before implementing if it was not** *(fires in the `review-before` gear, which is the default)*

## Phase 9: Dependency injection and hosting
ADR: steps 15 and 16; Key Component `UseOutboxNotifications` and the `OutboxNotifications(IServiceProvider)` helper; test groups "Off, options and kinds" (DI forms), "Hosted", "Success raises nothing" (DI); further tests 6, 7 and 11

- [ ] **TEST + IMPLEMENT: 34 — The step turns the feature on, validates first, and keeps its first options**
  - Depends on: 12
  - Test location: "tests/Paramore.Brighter.Extensions.AspNetCore.Tests/OutboxNotifications"
  - Test files, in order:
    1. `When_the_step_is_used_and_a_post_fails_should_notify_a_hosted_subscriber.cs` (AC-34, first part)
       - **USE COMMAND**: `/tdd:test-first when the step is used and a post fails should notify a hosted subscriber`
    2. `When_the_step_is_given_a_capacity_of_zero_should_throw_before_registering_anything.cs` (AC-2, DI path)
       - **USE COMMAND**: `/tdd:test-first when the step is given a capacity of zero should throw before registering anything`
    3. `When_the_step_is_called_twice_should_keep_the_first_options.cs` (ADR further test 11)
       - **USE COMMAND**: `/tdd:test-first when the step is called twice should keep the first options`
    4. `When_another_registry_implementation_is_registered_should_make_the_helper_throw.cs`
       - **USE COMMAND**: `/tdd:test-first when another registry implementation is registered should make the helper throw`
  - Test 2 is RED on arrival, because test 1's code registers without validating. Test 3 is RED on arrival, because test 1's code registers with `AddSingleton`, so the second call's capacity wins. Test 4 is RED on arrival, because test 1's code makes the helper return null for another implementation.
  - Test should verify:
    - A `BackgroundService` reader receives one notification after `PostAsync` fails.
    - A capacity of 0 throws `ConfigurationException` from the step, and nothing is registered.
    - A second call's capacity is ignored.
    - `OutboxNotifications(provider)` throws `ConfigurationException` naming the other implementation when another `IAmAnOutboxNotificationRegistry` is registered.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - Add `UseOutboxNotifications(this IBrighterBuilder, Action<OutboxNotificationOptions>? = null)` beside `TransformFactory`. It registers only the interface, with `AddSingleton`, as a singleton factory over `IAmAProducerRegistry`. Test 2's code validates the options before anything is registered. Test 3's code switches the registration to `TryAdd`.
    - Add the public helper `OutboxNotifications(IServiceProvider)`. Test 1's code returns the registered `IAmAnOutboxNotificationRegistry` cast with `as` to the concrete registry, so it gives null when none is registered or another implementation is. Test 4's code throws `ConfigurationException` naming the other implementation.
    - In `BuildOutBoxProducerMediator`, replace the `null` that task 7 passes after `OutboxMeter(serviceProvider)` with the registry.

- [ ] **TEST + IMPLEMENT: 35 — A max age without an archiver is warned about, and the archiver is wired to the registry**
  - Depends on: 34, 31
  - Test location: "tests/Paramore.Brighter.Extensions.AspNetCore.Tests/OutboxNotifications" (reuse this project's `CapturingLoggerProvider`)
  - Test files, in order:
    1. `When_a_max_age_is_set_without_the_archiver_should_warn_once_naming_the_topic.cs` (AC-54, first part)
       - **USE COMMAND**: `/tdd:test-first when a max age is set without the archiver should warn once naming the topic`
    2. `When_the_archiver_is_registered_with_notifications_should_not_warn_and_raise_expired_on_that_containers_channel.cs` (AC-54, second part; `NullOutboxArchiveProvider`)
       - **USE COMMAND**: `/tdd:test-first when the archiver is registered with notifications should not warn and raise expired on that container's channel`
  - Test should verify:
    - Resolving the mediator throws nothing and writes one `Warning` naming `orders`.
    - With `UseOutboxArchiver`, no warning is written. A run removes expired `m1` and raises `Expired` on that container's `orders` channel.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - After the mediator's construction in `BuildOutBoxProducerMediator`, ask `IServiceProviderIsService` whether `OutboxArchiver<Message, TTransaction>` is registered. If not, write one `Warning` per topic with a max age.
    - In `UseOutboxArchiver`, pass `GetService<IAmAProducerRegistry>()`, `OutboxNotifications(provider)` and `TimeProvider.System`.

- [ ] **CHARACTERISE: 36 — Born-green tests for dependency injection and hosting**
  - Depends on: 34, 35
  - 8 items, in order. Each item, and each file of an item that lists several, goes through its own `/tdd:test-first`, its own RED mutation and its own approval gate, as the CHARACTERISE rules in the header say. The task ends in one `test:` commit.
  - **36.1 — Off by default on the DI path**
    - Test location: "tests/Paramore.Brighter.Extensions.AspNetCore.Tests/OutboxNotifications"
    - Test file: `When_the_step_is_not_used_should_register_no_registry_and_clear_as_today.cs` (AC-1)
    - **USE COMMAND**: `/tdd:test-first when the step is not used should register no registry and clear as today`
    - Test should verify:
      - With `AddProducers`, `UseOutboxSweeper`, `UseOutboxStaleMessageMonitor` and `AddBrighterInstrumentation`, `ClearOutboxAsync(["m1"])` returns, `m1` is undispatched and nothing throws.
      - `GetService<IAmAnOutboxNotificationRegistry>()` is null.
    - 🔁 Characterisation — green on first run: only the step registers. RED mutation: register the registry unconditionally in `AddProducers`. Confirm the test fails on `GetService` not being null.
    - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE once RED is observed — before committing if the test was green on arrival, before implementing if it was not** *(fires in the `review-before` gear, which is the default)*
  - **36.2 — A reader that starts before the mediator is resolved still receives**
    - Test location: "tests/Paramore.Brighter.Extensions.AspNetCore.Tests/OutboxNotifications"
    - Test file: `When_a_subscriber_reads_before_the_mediator_is_resolved_should_receive_a_later_failure.cs` (AC-34, second part)
    - **USE COMMAND**: `/tdd:test-first when a subscriber reads before the mediator is resolved should receive a later failure`
    - Test should verify: the waiting reader receives the notification once the mediator is resolved and a clear fails.
    - 🔁 Characterisation — green on first run: the mediator takes the container's singleton. RED mutation: have `BuildOutBoxProducerMediator` build its own registry. Confirm the test fails on the reader's bounded wait.
    - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE once RED is observed — before committing if the test was green on arrival, before implementing if it was not** *(fires in the `review-before` gear, which is the default)*
  - **36.3 — An application's own producers configuration receives notifications**
    - Test location: "tests/Paramore.Brighter.Extensions.AspNetCore.Tests/OutboxNotifications"
    - Test file: `When_an_application_registers_its_own_producers_configuration_should_receive_notifications.cs` (ADR further test 6)
    - **USE COMMAND**: `/tdd:test-first when an application registers its own producers configuration should receive notifications`
    - Test should verify: one notification after a failed clear.
    - 🔁 Characterisation — green on first run: `BuildOutBoxProducerMediator` reads the registry on both paths. RED mutation: read the registry only in the `AddProducers` overload. Confirm the test fails on the bounded read.
    - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE once RED is observed — before committing if the test was green on arrival, before implementing if it was not** *(fires in the `review-before` gear, which is the default)*
  - **36.4 — Two containers get two registries**
    - Test location: "tests/Paramore.Brighter.Extensions.AspNetCore.Tests/OutboxNotifications"
    - Test file: `When_two_containers_are_built_from_one_collection_should_give_each_its_own_registry.cs` (ADR further test 7)
    - **USE COMMAND**: `/tdd:test-first when two containers are built from one collection should give each its own registry`
    - Test should verify: the two registries are different objects, and each mediator attaches without throwing.
    - 🔁 Characterisation — green on first run: the registration is a factory. RED mutation: register one instance. Confirm the test fails on the second mediator's `Attach` throwing.
    - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE once RED is observed — before committing if the test was green on arrival, before implementing if it was not** *(fires in the `review-before` gear, which is the default)*
  - **36.5 — Invalid options stop the host under `ValidatePipelines()`, with no new finding**
    - Test location: "tests/Paramore.Brighter.Extensions.AspNetCore.Tests/OutboxNotifications"
    - Test file: `When_validate_pipelines_runs_with_invalid_notification_options_should_not_start_the_host.cs` (AC-59; a theory over capacity 0 and no kinds, and `throwOnError` true and false)
    - **USE COMMAND**: `/tdd:test-first when validate pipelines runs with invalid notification options should not start the host`
    - Test should verify: in all four cases the host does not start, and the exception is the `ConfigurationException` of AC-2 or AC-55, or has it as an inner exception.
    - 🔁 Characterisation — green on first run: the step and the constructor validate. RED mutation: remove `Validate()` from both the step and the registry constructor. Confirm the capacity rows fail on the exception type, an `ArgumentOutOfRangeException` from the channel instead of the `ConfigurationException`, and the kinds rows fail on the host starting.
    - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE once RED is observed — before committing if the test was green on arrival, before implementing if it was not** *(fires in the `review-before` gear, which is the default)*
  - **36.6 — Success raises nothing on the DI path**
    - Test location: "tests/Paramore.Brighter.Extensions.AspNetCore.Tests/OutboxNotifications"
    - Test file: `When_a_sweep_succeeds_on_the_di_path_should_raise_nothing.cs` (AC-44, DI path; a theory over `useBulk`)
    - **USE COMMAND**: `/tdd:test-first when a sweep succeeds on the di path should raise nothing`
    - Test should verify: no notification.
    - 🔁 Characterisation — green on first run: the sites raise only on an exception. RED mutation: at the async send site and at the bulk send site, raise when the helper returns no exception, with a null exception. Confirm both rows fail on the count.
    - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE once RED is observed — before committing if the test was green on arrival, before implementing if it was not** *(fires in the `review-before` gear, which is the default)*
  - **36.7 — Disposing the container ends a hosted reader and throws nothing (risk)**
    - Test location: "tests/Paramore.Brighter.Extensions.AspNetCore.Tests/OutboxNotifications"
    - Test file: `When_the_container_disposes_the_mediator_and_the_registry_should_end_the_reader_and_throw_nothing.cs`
    - **USE COMMAND**: `/tdd:test-first when the container disposes the mediator and the registry should end the reader and throw nothing`
    - Test should verify: whatever order the container disposes the two singletons in, `Dispose` throws nothing and the reader's enumeration ends.
    - 🔁 Characterisation — green on first run: closing is `TryComplete` and can repeat. RED mutation: use `Writer.Complete()` in `Close`, which throws on a second call. Confirm the test fails on the dispose throwing.
    - Mitigates: a double close between the mediator's `Dispose` and the container's disposal of the registry.
    - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE once RED is observed — before committing if the test was green on arrival, before implementing if it was not** *(fires in the `review-before` gear, which is the default)*
  - **36.8 — Existing public interfaces keep every member (risk)**
    - Test location: "tests/Paramore.Brighter.Extensions.AspNetCore.Tests", beside `When_comparing_public_members_with_the_baseline_should_keep_every_existing_member.cs`
    - Test file: `When_comparing_notification_related_interfaces_with_the_baseline_should_add_no_member.cs` (NFR-8)
    - **USE COMMAND**: `/tdd:test-first when comparing notification related interfaces with the baseline should add no member`
    - Test should verify: each interface NFR-8 names has exactly its recorded members:
      - `IAmAnOutbox`, `IAmAnOutboxSync<,>` and `IAmAnOutboxAsync<,>`;
      - `IAmAnOutboxProducerMediator` and `IAmAnOutboxProducerMediator<,>`;
      - `IAmAMessageProducer` and its sync, async and bulk roles;
      - `ISupportPublishConfirmation` and `ISupportPublishConfirmationAsync`;
      - `IAmAnOutboxCircuitBreaker`, `IAmABrighterOutboxMeter` and `IAmABrighterTracer`;
      - `IAmProducersConfiguration`.
    - The public properties of `PublishConfirmationResult` are exactly its recorded ones.
    - `IAmAProducerRegistry` and `IAmAnArchiveProvider` are also checked, because the ADR lists them as unchanged.
    - 🔁 Characterisation — green on first run: the feature adds only new types and trailing optional parameters. RED mutation: add a member to `IAmAnOutboxProducerMediator`, with stub implementations on `OutboxProducerMediator` and `SweepOnlyOutboxProducerMediator` so the build still passes. Confirm the test fails on the member list.
    - Mitigates: a source-breaking interface change, which C-2 rules out.
    - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE once RED is observed — before committing if the test was green on arrival, before implementing if it was not** *(fires in the `review-before` gear, which is the default)*

## Phase 10: Documentation
ADR: step 17; NFR-11; AC-40

- [ ] **DOC: 37 — XML documentation, release notes and the documentation points**
  - Depends on: 1-36
  - XML documentation, following `.agent_instructions/documentation.md`, on every new public type and member:
    - `OutboxNotification`, `OutboxNotificationKind` and its members, `OutboxNotificationOptions`;
    - `IAmAnOutboxNotificationChannel`, `OutboxNotificationChannel`, `IAmAnOutboxNotificationRegistry`, `OutboxNotificationRegistry`;
    - `UseOutboxNotifications` and `OutboxNotifications(IServiceProvider)`;
    - `Publication.MaxAge`, `OutboxMaxAges` and its keys, `IRelationalDatabaseOutboxExpiryQueries`, `Message.ExpiredAtHeaderName`;
    - the new constructor parameters of the mediator and `OutboxArchiver`.
  - `release_notes.md`, under `## Master`:
    - A `###` section for the feature.
    - The change of behaviour a max age brings: sweeps leave expired messages out, and the archiver moves them and raises `Expired`.
    - Under `#### Breaking changes`:
      - *(binary)* the mediator's constructor gains a trailing optional parameter;
      - *(binary)* `OutboxArchiver`'s constructor gains three trailing optional parameters;
      - behaviour: once a max age is set, a third-party relational dialect without `IRelationalDatabaseOutboxExpiryQueries` makes every sweep throw `NotSupportedException`, and every archiver run fail in its expired pass, after the dispatched pass;
      - behaviour: a third-party outbox that ignores `ExcludeExpiredArgsKey` keeps sending expired messages from sweeps;
      - behaviour: a replayed causation message older than its max age is left out of sweeps and archived as expired.
    - Name the Firestore composite index (IsDispatched, Topic, TimeStamp).
  - The documentation points for the BrighterCommand/Docs page: write each NFR-11 bullet as a checklist in the PR description, for the Docs PR to cover. Each covers one point:
    - turning the feature on, and a publication's own channel, capacity and dropped count;
    - the meaning of each kind;
    - that a notification is not final;
    - removing duplicates by id;
    - a clear on every notification repeating without end;
    - what is lost;
    - the Firestore index;
    - passing the stopping token;
    - an exception escaping a `BackgroundService` reader stopping the host;
    - what FR-4 leaves out;
    - reading the dropped count;
    - a batch's notifications sharing one exception;
    - `Call` confirmations and `MarkDispatchedFailed` on stores that throw for a missing row;
    - an empty id or topic;
    - running the archiver as well as the sweeper (also on the archiver page and in the sweeper section);
    - setting a max age, `TimeStamp`, and `NullOutboxArchiveProvider`;
    - explicit clears and `Post`;
    - `Expired` being at least once;
    - the `ExpiredAt` mark;
    - per-topic smallest age and D-20;
    - the batch size bound;
    - choosing kinds;
    - `ValidatePipelines()` stopping the host;
    - a consumer comparing `TimeStamp` with the age.
  - Also add to the checklist the ADR's own documentation commitments beyond NFR-11, each noted with its ADR section:
    - `AzureBlobArchiveProvider` with a `TagsFunc` that copies the `ExpiredAt` entry into a tag (Decision, and the risks table);
    - a store TTL longer than the largest max age on DynamoDB, Firestore and MongoDb (risks table);
    - the backlog drain rate, about 400 a minute at the defaults, and how to raise the batch size (risks table);
    - a changed max age takes effect per instance at restart (risks table);
    - a max age well above the send time, retries included (risks table);
    - an archiver built without a producer registry moves no expired message (Consequences);
    - the `InMemoryOutbox` clean-up that it calls "expired" is not the `Expired` notification (Decision).
  - Why no test of its own: AC-40 is checked by a person reading the PR and the Docs page. The Docs page lives outside this repository.

---

## Coverage cross-reference

### Functional requirements → tasks

| FR | Tasks |
| --- | --- |
| FR-1 | 36.1, 34 |
| FR-2 | 4, 6.1, 18.2, 34, 36.5 |
| FR-3.1 | 7, 9, 13.1, 13.2, 13.3 |
| FR-3.2 | 11, 13.5, 13.6 |
| FR-3.3 | 10, 12, 13.8, 33.6 |
| FR-4 | 14.1, 14.2, 14.3, 14.4, 14.7 |
| FR-5 | 7, 9, 11, 12 |
| FR-6 | 9, 13.3, 13.4 |
| FR-7 | 14.1, 14.2, 14.5, 14.6, 22.9 |
| FR-8 | 18.5 |
| FR-9 | 16, 18.2, 18.4, 17 |
| FR-10 | 18.6 |
| FR-11 | 16, 18.7, 18.8, 18.9, 18.13, 34 |
| FR-12 | 18.9, 18.10 |
| FR-13 | 15, 18.1, 16 |
| FR-14 | 5, 7, 8, 13.6, 13.7, 13.9, 13.11, 14.8, 18.4, 31, 35 |
| FR-15 | 34, 35, 36.2, 36.3 |
| FR-16 | Withdrawn |
| FR-17 | 19, 20, 22.4, 22.5, 33.1, 33.2 |
| FR-18 | 20, 22.1, 22.2, 22.3, 22.4, 22.5, 22.6, 23-29 |
| FR-19 | 21, 23-30, 31, 32, 33.2 |
| FR-20 | 31, 33.3, 33.4, 33.5, 33.6 |
| FR-21 | 35, 37 |
| FR-22 | 4, 11, 33.4, 33.7, 33.8, 36.5 |

### Non-functional requirements → tasks

| NFR | Tasks |
| --- | --- |
| NFR-1 | 18.9, 33.5 |
| NFR-2 | 17 |
| NFR-3 | 14.4, 14.7, 33.1, 36.6 |
| NFR-4 | 36.1 |
| NFR-5 | 18.3 |
| NFR-6 | 18.11 |
| NFR-7 | 18.12 |
| NFR-8 | 36.8, and the build and existing suite throughout |
| NFR-9 | 17, 20, 22.1 (`FakeTimeProvider`) |
| NFR-10 | 17 |
| NFR-11 | 37 |

### Acceptance criteria → tasks

| AC | Tasks | AC | Tasks |
| --- | --- | --- | --- |
| AC-1 | 36.1 | AC-31 | 18.1, 16 |
| AC-2 | 4, 34 | AC-32 | 13.9 |
| AC-3 | 18.2, 6.1 | AC-33 | 14.8 |
| AC-4 | 7 | AC-34 | 34, 36.2 |
| AC-5 | 9 | AC-35 | 17 |
| AC-6 | 9 | AC-36 | 18.3 |
| AC-7 | 9 | AC-37 | 18.11 |
| AC-8 | 13.1 | AC-38 | 18.12 |
| AC-9 | 13.2 | AC-39 | 17 |
| AC-10 | 11 | AC-40 | 37 |
| AC-11 | 11, 13.5 | AC-41, AC-42 | Withdrawn |
| AC-12 | 10 | AC-43 | 14.7 |
| AC-13 | 12, 13.8, 14.7 | AC-44 | 14.7, 36.6 |
| AC-14 | 14.1 | AC-45 | 33.1 |
| AC-15 | 14.2 | AC-46 | 20, 22.1, 22.2 |
| AC-16 | 14.3 | AC-47 | 22.4, 22.5, 33.2 |
| AC-17 | 14.4 | AC-48 | 22.6 |
| AC-18 | 13.3 | AC-49 | 30, 31 |
| AC-19 | 13.4 | AC-50 | 32, 23-29 |
| AC-20 | 14.5, 22.9 | AC-51 | 33.3 |
| AC-21 | 14.6 | AC-52 | 33.4, 33.5 |
| AC-22 | 18.5 | AC-53 | 33.6 |
| AC-23 | 16 | AC-54 | 35 |
| AC-24 | 17 | AC-55 | 4, 33.7, 33.8 |
| AC-25 | 18.6 | AC-56 | 22.3, 23-29 |
| AC-26 | 18.7 | AC-57 | 18.4, 13.6 |
| AC-27 | 18.8 | AC-58 | 13.7, 31 |
| AC-28 | 18.9 | AC-59 | 36.5 |
| AC-29 | 18.10 | | |
| AC-30 | 15 | | |

### ADR decisions → tasks

**Implementation Approach steps**

| Step | Tasks |
| --- | --- |
| 1 `Message.ProducerLookupTopic()` | 1 (used by 31) |
| 2 `TryExecute` helpers | 2 |
| 3 Archiver dispatched-pass extract | 3 |
| 4 Types, validation, keys, lookups, reads, close | 4, 5, 7, 15, 16 |
| 5 Mediator parameter, `Attach` before `ConfigureCallbacks`, captured channels, `Close` in `finally` | 7, 8, 11, 15, 17 |
| 6 Raise at send, mark and confirmation sites | 7, 9, 10, 11, 12 |
| 7 `itemDropped` count and rate-limited warning | 16, 17, 31 |
| 8 `Publication.MaxAge` and `OutboxMaxAges` | 19 |
| 9 Sweep copies `args` and adds the exclude key | 20, 22.7 |
| 10 `InMemoryOutbox` reads both keys on `TimeStamp` | 20, 21 |
| 11 Generated templates | 23 |
| 12 `IRelationalDatabaseOutboxExpiryQueries` and five dialects | 24, 25 |
| 13 DynamoDB, DynamoDB.V4, MongoDb, Firestore | 26, 27, 28, 29 |
| 14 Archiver parameters and expired pass | 30, 31, 32 |
| 15 `UseOutboxNotifications`, helper, FR-21 check | 34, 35 |
| 16 `UseOutboxArchiver` wiring | 35 |
| 17 XML docs and release notes | 37 |

**Raise-site table**

| Row | Tasks |
| --- | --- |
| `Dispatch`, confirming send | 9 |
| `Dispatch`, send | 9 |
| `Dispatch`, mark | 10 |
| `DispatchAsync`, send | 7, 14.6 |
| `DispatchAsync`, mark | 10 |
| `BulkDispatchAsync`, send per id | 9, 14.6, 17 |
| `BulkDispatchAsync`, mark per id | 10 |
| `HandleAsyncPublishConfirmation`, failure | 11, 14.6 |
| `HandleAsyncPublishConfirmation`, mark | 12 |
| `HandleAsyncPublishConfirmation`, catch | 12, 14.6 |
| `ConfigurePublisherCallbackMaybe`, failure | 11, 14.6 |
| `ConfigurePublisherCallbackMaybe`, mark | 12 |
| `ConfigurePublisherCallbackMaybe`, catch | 12, 14.6 |

Reading notes:
- raise before `TripTopic`: 14.6;
- missing pipeline: 12, 14.4;
- `CallViaExternalBus` and outbox writes raise nothing: 14.4;
- batch guard: 17;
- channel check first: holds by construction, and the feature-off runs of 14.5 compare the calls;
- `Attach` is atomic, the clock included (ADR, "Why two mediators cannot share a registry"): 8, 17.

**Test-group table**

| Group | Tasks |
| --- | --- |
| Off, options and kinds | 4, 6.1, 18.2, 33.7, 33.8, 34, 36.1, 36.5 |
| Each site raises once | 7, 9, 13.1, 13.2, 13.3, 13.4, 10, 11, 13.5, 13.6, 13.7, 12, 13.8 |
| Nothing else raises | 14.1, 14.2, 14.3, 14.4 |
| Behaviour unchanged | 14.5, 14.6, 22.9 |
| Delivery, order, full buffer | 16, 18.3, 17, 18.5, 18.6, 18.7, 18.8, 18.12, 18.13 |
| Never blocks, shutdown | 15, 18.1, 16, 18.9, 18.10 |
| Scope and routing | 13.6, 13.7, 13.9, 13.11, 14.8, 18.4, 18.11, 31 |
| Success raises nothing | 14.7, 36.6 |
| Hosted | 34, 35, 36.2 |
| Max age on the sweep | 20, 22.1, 22.2, 22.3, 22.4, 22.5, 22.6, 33.1 |
| Archiver | 30-32, 33.1, 33.2, 33.3, 33.4, 33.5, 33.6, 33.7, 33.8 |
| Store queries | 23-29 |
| Content and documents | 18.3 (a notification holds no message), 17, 37 |

**Further tests**

| Further test | Task |
| --- | --- |
| Unknown key throws; `LookupBy` after mediator `Dispose` | 5, 13.10 |
| Second mediator throws, adds no handler | 8 |
| Registry from another producer registry makes `Attach` throw | 8 |
| `Dispose` on an unattached registry ends a waiting read | 5 |
| Cancelled after first read of a full buffer | 15 |
| Own `IAmProducersConfiguration` | 36.3 |
| Two containers, two registries | 36.4 |
| Caller `args` unchanged | 22.7 |
| Store ignoring `OnlyExpiredArgsKey` | 30 |
| Rotating start topic | 32 |
| Second step call keeps first options | 34 |

**Key Components (public names)**

| Component | Tasks |
| --- | --- |
| `OutboxNotificationKind`, `OutboxNotificationOptions` | 4, 6.1, 11, 18.2, 33.8 |
| `OutboxNotification` | 5, 7, 18.3 |
| `IAmAnOutboxNotificationRegistry`, `OutboxNotificationRegistry` | 5, 7, 8, 13.9, 13.10, 15, 17, 31 |
| `IAmAnOutboxNotificationChannel`, `OutboxNotificationChannel` | 5, 15, 16, 17 |
| `UseOutboxNotifications`, `OutboxNotifications(IServiceProvider)` | 34, 36.4 |
| `Publication.MaxAge`, `OutboxMaxAges` | 19, 20, 21, 23-27, 29 |
| `IRelationalDatabaseOutboxExpiryQueries` | 24, 25 |
| `Message.ExpiredAtHeaderName` | 30 |

**Open-item outcomes this list depends on**
- P-3 drop-oldest: 16.
- P-4 one warning per 60 s: 17.
- P-5 competing readers: 18.13.
- P-7 as changed: 26-29.
- P-8: 30.
- P-9: 30.
- Fallback channel: 31.
- No `Error` finding: 36.5.
- Q-6: header and 11.
- Q-8: 13.5, 13.8.
- FR-18 DynamoDB departure: 26.
- Documentation commitments beyond NFR-11 (the `TagsFunc` tag, store TTL, drain rate, restart, send time, an archiver without a registry, `InMemoryOutbox` clean-up): 37.

### Scope creep

These tasks go beyond the criteria, each as risk mitigation:
- 36.7, disposal order: pins `TryComplete` against a double close between the mediator and the container.
- 36.8, NFR-8 baseline: NFR-8 maps to no AC. This makes C-2's ban on interface members executable.
- 22.8, causation replay keeps its stamp: pins the release-notes behaviour line on `InMemoryOutbox`, which implements causation tracking.
- 28, PROJECT for the Firestore index: needs the maintainer, as C-4 notes.
- 5 (constructor probe, test 4), 11 (kind filter, test 3), 13.11 (shared producer), 17 (atomic clock, test 5), 18.13 (competing readers), 20 (stamp not write time; no registry, test 3), 21 (four query edge cases), 23 (several topics throw; a max age shorter than the minimum age), 24 (`NotSupportedException`), 26 (an empty page without a query), 31 (another producer registry, test 6), 33.4 (Expired not chosen, second file): each pins an ADR decision with no AC of its own.

No other ADR amendment is needed. Every decision maps to a task.

### Slice count

24 TEST + IMPLEMENT slices: 4, 5, 7, 8, 9, 10, 11, 12, 15, 16, 17, 19, 20, 21, 24, 25, 26, 27, 29, 30, 31, 32, 34, 35.

The rest of the list:
- 3 STRUCTURAL;
- 7 CHARACTERISE, one per phase that has born-green tests (6, 13, 14, 18, 22, 33 and 36), holding 58 items. Nine more tests that may be green on arrival (task 5 test 2, task 7 test 2, task 17 test 5, task 19 test 3, task 20 tests 2 and 3, task 21 test 4, and task 31 tests 3 and 6) stay in their slices;
- 1 GENERATE;
- 1 PROJECT;
- 1 DOC.

The store slices 25-27 and 29 hold only generated tests, which were seen failing in task 23.
