# Tasks: 0038 Outbox Stale Message Telemetry

**Spec:** `specs/0038-outbox-stale-message-telemetry/requirements.md` (approved): FR-1 to FR-15 plus FR-3.1, FR-12.1 and FR-14.1, NFR-1 to NFR-7, AC-1 to AC-40
**Issue:** issue #4489 (outbox stale message telemetry, the first of its two parts)
**ADRs (Accepted):** `docs/adr/0085-outbox-stale-message-telemetry.md`, `docs/adr/0086-outbox-per-topic-stale-count.md`
**Delivery:** one PR. Phase headings are stable so `/spec:gear` can scope to them.

## How to use this list

- **TDD gear.** The approval gate is on by default (`review-before`). Change it only with `/spec:gear`. This file does not record the gear.
- **ADR names.** ADR 0085 in this list means `docs/adr/0085-outbox-stale-message-telemetry.md`, and ADR 0086 means `docs/adr/0086-outbox-per-topic-stale-count.md`. Another ADR, `docs/adr/0085-gcp-purge-server-boundary.md`, also has the number 0085; this list never means it. "Row 1" to "Row 6d" mean the rows of the decision ladder in ADR 0085 :251-262.
- **Task kinds.** The kind is the bold tag that opens each task. `/spec:ralph-implement` dispatches on it (`.claude/commands/spec/ralph-implement.md:181-192`), and `/spec:implement` follows the `CHARACTERISE` and `GENERATE` shapes (`.claude/commands/spec/implement.md:154-162`). The ralph table has no `PROJECT` row, so it marks a `PROJECT` task `[!]` (`:192`). Run those with `/spec:implement`.
  - `TEST + IMPLEMENT`: the default kind for a behaviour. Use `/test-first`. The ⛔ gate applies. Each task is one behaviour, or one case of an AC, and the test and its implementation stay in the same task.
  - `CHARACTERISE`: a behaviour that earlier work may already deliver, so its test may be green on arrival. Use `/test-first`. The task names a 🔁 RED mutation, and the ⛔ gate fires once RED is observed. See "Green on arrival and named mutations" below.
  - `GENERATE`: tests that the test generator renders from templates. Do not use `/test-first`, and never write or edit a generated file by hand. The ⛔ gate fires before the generated files are committed. See "Conformance templates" below.
  - `STRUCTURAL`: use `/tidy-first`. No behaviour changes, and existing tests stay green.
  - `PROJECT`: work that is not a behaviour of the code: a GitHub issue, a suite run, a code review or a recording. Each produces the output the task names, in the place the task names. It does not use `/test-first`.
  - `DOC`: documentation. It does not use `/test-first`.
  - Counts: 148 tasks. 51 `TEST + IMPLEMENT`, 62 `CHARACTERISE`, 7 `GENERATE`, 4 `STRUCTURAL`, 17 `PROJECT`, 7 `DOC`. `/spec:show-me` counts the `CHARACTERISE` and `GENERATE` tasks as untagged, because its tag pattern knows only the other four (`.claude/commands/spec/show_me_facts.cs:1443`). The tasks template (`.claude/commands/spec/tasks.md:133-143`) lists four tags. `CHARACTERISE` and `GENERATE` are the labels that `.agent_instructions/testing.md:67` and ADR 0071-tdd-review-gear :236-255 define for these shapes, so this file uses them.
- **Publishing.** Any text that goes to GitHub (an issue, a pull request in another repository, or this pull request's description) is produced first in the task's output. Posting it is a separate manual step after that text is reviewed, and the task then records the number or link. Nothing in this list posts to GitHub by itself.
- **Order.** ADR 0085 steps 1 and 2 (Phase 1). Then ADR 0086 steps 1 and 2 (Phase 2), because step 6 of ADR 0085 needs the capability interface and step 8 needs `InMemoryOutbox` to implement it. Then ADR 0085 steps 3 to 14 (Phases 3 to 14). ADR 0086 step 3, the follow-up issues, is in Phase 14 with the documentation. Phase 15 is the final verification.
- **Line numbers.** Lines are cited at HEAD 942cb8f56. The ADRs cite commit 1ce331eb2. Where a line has moved, the task gives the HEAD line and then the ADR's line in parentheses, as in `:713-720` (ADR: `:716-723`).
- **Determinism rules (every monitor test).** The loop resumes on a pool thread, so a fake clock alone does not make a test deterministic (ADR 0085 :831-834).
  1. After each `FakeTimeProvider.Advance`, wait for every call and outcome due at that instant. Bound the wait by wall-clock time (5 seconds, the AC setup), polling with `SpinWait.SpinUntil` as `tests/Paramore.Brighter.Core.Tests/Sweeping/TestDoubles/RecordedSweeperMeasurements.cs:46-47` does. The test fails rather than hangs.
  2. Assert that a token was cancelled by reading `IsCancellationRequested`, never by the timestamp of a callback, because `CancelAsync` runs callbacks on a pool thread.
  3. Before advancing fake time past the instant a call is released, returns, throws or honours a cancellation, wait for that call to have ended, bounded as in rule 1. Row 4 skips every due time at or before the end a count's task records (ADR 0085 :249, :256), so an end recorded after the clock has moved skips a due time the test expects to be measured. For a call that the test releases, or that honours a cancellation, wait on the counting double's end signal; "Test doubles", later in this section, says how `StaleMessageCountingOutbox` completes that call and when it signals. For a call that throws or returns a chosen result as soon as it is called, the outcome that rule 1 waits for shows the end. If a test cannot observe the end before it advances, stop and ask.
- **Green on arrival and named mutations.** A `TEST + IMPLEMENT` task expects RED first. If its test is green on arrival, stop and ask.
  - A `CHARACTERISE` task names a 🔁 RED mutation, because earlier work may already deliver its behaviour. It runs in the `CHARACTERISE` shape of `.claude/commands/spec/implement.md:154-159`, `.claude/commands/spec/ralph-implement.md:187` and ADR 0071-tdd-review-gear :241-248. The source is `.agent_instructions/testing.md:46-73`. If the test is green on arrival:
    1. Do not weaken, rewrite or delete the test to get a failure.
    2. Apply the named mutation. It is a temporary change to **production code, never the test**. Apply one mutation at a time. A named mutation may change more than one place when one defect needs it, as in T10.5, and it still counts as one.
    3. The test must fail **on the assertion it is about**. A compile error, or an unrelated exception thrown before that assertion, does not count.
    4. Revert the mutation and confirm green. `git status` shows no production file modified.
    5. Run the full suite for the affected project.
    6. Commit the test alone as `test:`, naming the mutation in the message. Never commit the mutation.
  - If a `CHARACTERISE` test is RED on arrival, the behaviour is missing: follow the task's "Implementation should" as for a `TEST + IMPLEMENT` task.
  - Never mark a `CHARACTERISE` task `ALREADY_COMPLETE`.
  - Phases 11 and 12, T8.10, T8.18, T9.11, T9.12, T13.1 and T13.2 expect no production change. In those tasks a test that is RED on arrival is the surprise, so stop and ask.
- **Commit shape.** Follow Tidy First and Conventional Commits.
  - A structural task is one `refactor:` commit.
  - A `TEST + IMPLEMENT` task is one `feat:` commit. A `CHARACTERISE` task is one `test:` commit if its test was green on arrival, or one `feat:` commit if it needed an implementation. A `GENERATE` task is one `test:` commit followed by one `docs:` commit for its FR-14 record. A `DOC` task, and a `PROJECT` task that writes its output into a file, is one `docs:` commit. T13.17 is the one `PROJECT` task with its own commits (see the task).
  - Each of those commits is followed by a separate `docs:` commit that ticks the task in this file.
  - Never mix a structural change and a behavioural change in one commit.
- **Conformance templates (Phase 13).**
  - Edit the Liquid templates under `tools/Paramore.Brighter.Test.Generator/Templates/Outbox/`. **Never hand-edit a file under `Generated/`.**
  - To regenerate one project, follow `.agent_instructions/generated_tests.md:410-440`: build the generator, then run it from that test project's directory. `./generate-test.sh` (or `.\generate-test.ps1`) regenerates every project.
  - The generator writes files and never deletes one. Remove a stale generated file by hand only when the generated-tree audit reports it as an orphan.
  - Run the generated-tree audit with `dotnet test tests/Paramore.Brighter.Test.Generator.Tests/Paramore.Brighter.Test.Generator.Tests.csproj -f net10.0`.
  - Between T13.3 and T13.13, the audit reports the OutstandingCount suite's files (the fourth outbox suite) as Missing for each outbox project not yet regenerated. That is expected, and T13.13 ends with the audit green. Any other audit finding means stop and ask.
  - T13.7 to T13.13 run in the `GENERATE` shape of `.claude/commands/spec/implement.md:160-162` and `.claude/commands/spec/ralph-implement.md:188`. Write the provider by hand, then regenerate the project: `dotnet build tools/Paramore.Brighter.Test.Generator`, then, from the test project's directory, `dotnet run --no-build --project ../../tools/Paramore.Brighter.Test.Generator`. Run the generated tests with `dotnet test tests/<project> --filter "FullyQualifiedName~OutstandingCount"` from the repository root, and record each result in the FR-14 table. The gate is over the generated files, the provider and the recorded results. Then commit the provider, `test-configuration.json` and the generated files as `test:`, and the FR-14 record as a separate `docs:` commit before the tick.
  - A generated fact on a Meets cell must pass. If a fact on a "To verify" cell fails, stop and ask: the cell becomes a Gap for T13.15. T13.12 and T13.13 also run facts on Gap cells, and each says how.
  - Run T13.3 to T13.13 with `/spec:implement`, not `/spec:ralph-implement`. Until T13.13 the generated-tree audit inside `Paramore.Brighter.Test.Generator.Tests` reports Missing, which ralph treats as FAILED (`.claude/commands/spec/ralph-implement.md:303`); the FR-14 record in `requirements.md` is outside the files ralph may change (`:309-310`, `:349-356`); and T13.12 and T13.13 expect red Gap facts.
  - CI window. From T13.3 to T13.13, the build job fails at "Test Generator Tests" (`.github/workflows/ci.yml:70-74`) because the audit reports Missing, and every job that needs it, dynamo-ci included, is skipped. From T13.13 to T13.17 the build job passes again, but each job that runs a project with an unmarked Gap fact fails, and the dynamo-ci job (`:530-559`) is one of them. Do not push between T13.3 and T13.17.
- **Test doubles.** They live in `tests/Paramore.Brighter.Core.Tests/Observability/StaleMessages/TestDoubles/`. The first task that needs a double creates it, and later tasks extend it.
  - `StaleMessageCountingOutbox`: the base counting double (ADR 0085 :829).
    - It implements `IAmAnOutboxSync<Message, CommittableTransaction>` and `IAmAnOutboxAsync<Message, CommittableTransaction>`, and **not** `IAmATopicCountingOutbox`.
    - It must not derive from `InMemoryOutbox`, because that type implements the capability after T2.2. That is unlike `tests/Paramore.Brighter.Core.Tests/CommandProcessors/TestDoubles/CountingOutbox.cs:18-21`.
    - It records each call of either async count and of the sync count, with its arguments and token, when the call is made and before it blocks, throws or returns. It holds no lock of its own while a call is blocked, so a blocked count never delays any of its other members, such as `Add`, `AddAsync`, `Get`, `GetAsync` or `MarkDispatchedAsync`.
    - On request, for either count, it can: block asynchronously until released with a result or an exception; block the calling thread on a wait handle before it returns a task; throw; return a chosen result; ignore cancellation; or register a cancellation callback that blocks. It can also block its sync count, as `CountingOutbox.cs:58` and `:89-94` hold counts. For T11.5, which watches the mediator's check rather than the monitor, its sync count can also return the number of messages added to it so far, and it counts each of its returns just before the call returns. That count of returns is separate from the end signal that the next bullet describes, and only T11.5 reads it.
    - It completes the task of a released or cancelled call through a `TaskCompletionSource` created without `RunContinuationsAsynchronously`, always inside `Task.Run`, and it signals that the call has ended only after that completion returns. On a pool thread with no `SynchronizationContext`, .NET runs the rest of the count's task inline, so that task has recorded its end before the signal. If the double completed the task on a thread where xunit's `SynchronizationContext` is current, such as the test's thread before its first `await`, .NET would queue the rest of the count's task, and the signal could come first. A call that throws or returns a chosen result as soon as it is called needs no signal: its task ends at once on the count's thread, and the outcome that rule 1 waits for shows that end. A test waits on the signal for rule 3, and T9.20 waits on it before it advances to 12:02:00 and runs the GC. The double's `///` summary says why it leaves out `RunContinuationsAsynchronously`, which the other gating doubles in this project use, such as `tests/Paramore.Brighter.Core.Tests/Confirmation/TestDoubles/GatedAsyncOutbox.cs:37-39`.
  - `StaleMessageTopicCountingOutbox`: derives from the base double and adds `IAmATopicCountingOutbox`. "The capability left out" means using the base double.
  - `SyncOnlyOutbox` (only `IAmAnOutboxSync`) and `NeitherInterfaceOutbox` (only `IAmAnOutbox`): used in Phase 7.
  - `CapturingLogger`: a capturing `ILogger` and its `ILoggerProvider`, added as a provider to the existing `ApplicationLogging.LoggerFactory`, not by replacing that factory. The monitor's logger may be a static field created before the test runs, as `src/Paramore.Brighter.Outbox.Hosting/TimedOutboxSweeper.cs:59` creates the sweeper's, and `LoggerFactory.AddProvider` also reaches loggers the factory has already created. The factory calls the provider's `CreateLogger` only once for each category, when the provider is added or when that category's first logger is created, and keeps that logger for the life of the factory. So the provider creates a logger for every category in the process, those of other components and of background work left by other tests included, and it cannot know then which categories a later test will capture. Every logger it returns therefore decides on each call. When the current test captures that logger's category, the logger is enabled for every level, Debug included, records each entry, and blocks or throws if the test has set that behaviour. For any other category it is not enabled and records nothing. The monitor's category is captured by default, and a test can capture others as well. The provider keeps its entries in a thread-safe collection, because the monitor logs from pool threads; `tests/Paramore.Brighter.Core.Tests/Validation/TestDoubles/SpyLogger.cs` is the pattern for the entries, but its `List` is not thread-safe. Pool threads also read the captured categories and any blocking or throwing behaviour, so the provider stores them in a way that is safe to read from a pool thread. `LoggerFactory` cannot remove a provider, so the collection fixture of `StaleMessageLoggingCollection` adds one shared provider once. Each test, before it acts, resets the provider: it clears the entries, sets the captured categories back to the monitor's alone and removes any blocking or throwing behaviour. Clearing only the entries is a separate call, for a test that clears them between runs and keeps its captured categories, as T11.5 does. `ApplicationLogging` is static and set at `tests/Paramore.Brighter.Core.Tests/Initializer.cs:16`, and the provider is shared, so every test that asserts a log entry belongs to that new non-parallel xunit collection. Patterns: `Sweeping/ThreadPoolStarvationCollection.cs:8` and `MessageSerialisation/FinalizerTestCollection.cs:35`.
  - `StaleMessageMetricsFixture`: the shared-container listener setup of ADR 0085 :821-825, created in T4.2. It builds a `ServiceCollection` with `AddMetrics()` and `AddOpenTelemetry().WithMetrics(m => m.AddBrighterInstrumentation())`, as `tests/Paramore.Brighter.Core.Tests/Observability/TestDoubles/InMemoryMetricsFromTraces.cs` does. It resolves the `MeterProvider`, then attaches a `MeterListener` over the `Paramore.Brighter` instruments. Only Phase 3 constructs the meter directly.
  - `RecordingDistributedLock`: an `IDistributedLock` that records every call, for AC-13. The pattern is `Sweeping/TestDoubles/AmbientRecordingLock.cs`.
- **AC setup.** Unless a task says otherwise, the setup is the one at requirements :333-342: clock at 2026-01-01T12:00:00Z, A 5 min, C 100, I 1 min, T 10 s, an empty `ProducerRegistry`, and a listener collecting every 10 s of fake time. That setup measures no topic (requirements :336). A task that names a per-topic call, result or token but lists no publications of its own uses `StaleMessageTopicCountingOutbox` and the publications of the AC it cites, or, if it cites no AC, publications on "orders" and "payments" without an A_p, as AC-33 has.
- **Out of scope. Do not task or fix:**
  - the channel for failed clears (OOS-1);
  - implementing `IAmATopicCountingOutbox` on any outbox but `InMemoryOutbox` (OOS-15);
  - closing an FR-14 gap in columns (a) to (c) (Q-9: each gap is tracked);
  - anything else in the requirements' "Out of Scope" section.

---

## Phase 1: Names and the publication age (ADR 0085 steps 1-2)

*Depends on no earlier phase.*

- [x] **STRUCTURAL: T1.1 Add the six stale-message constants to `BrighterSemanticConventions`**
  - **USE COMMAND**: `/tidy-first add the six outbox stale-message constants to BrighterSemanticConventions`
  - In `src/Paramore.Brighter/Observability/BrighterSemanticConventions.cs`, add these beside the ADR 0084-outbox-metrics outbox constants at `:100-104` (ADR: `:104-108`):
    - `OutboxStaleMessages` = `paramore.brighter.outbox.stale.messages`
    - `OutboxStaleTopicMessages` = `paramore.brighter.outbox.stale.topic.messages`
    - `OutboxStaleMeasurements` = `paramore.brighter.outbox.stale.measurements`
    - `OutboxStaleCount` = `paramore.brighter.outbox.stale.count`
    - `OutboxStaleOutcome` = `paramore.brighter.outbox.stale.outcome`
    - `OutboxImplementation` = `paramore.brighter.outbox.implementation`
  - Each constant has `///` documentation. Leave `OutboxType` unchanged.
  - Verification: nothing reads the constants; the solution builds; existing tests stay green.

- [x] **STRUCTURAL: T1.2 Add the nullable `Publication.StaleMessageAge`**
  - **USE COMMAND**: `/tidy-first add the nullable StaleMessageAge property to Publication`
  - Add `public TimeSpan? StaleMessageAge { get; set; }` beside `Topic` (`src/Paramore.Brighter/Publication.cs:96`).
  - Its `///` documentation states, in prose:
    - null means the monitor's A;
    - the value must be greater than zero and at most 365 days;
    - it is checked when measurement starts, never by the setter (FR-1, AC-35);
    - it is read once at start, so a later change needs a restart.
  - Verification: nothing reads the property; existing tests stay green; `Publication` gains exactly this one public member (NFR-3).

## Phase 2: The per-topic capability on `InMemoryOutbox` (ADR 0086 steps 1-2)

*Depends on no earlier phase.*

- [x] **STRUCTURAL: T2.1 Add the `IAmATopicCountingOutbox` capability interface**
  - **USE COMMAND**: `/tidy-first add the IAmATopicCountingOutbox capability interface`
  - Add `src/Paramore.Brighter/IAmATopicCountingOutbox.cs`, beside `IAmACausationTrackingOutbox` (`src/Paramore.Brighter/IAmACausationTrackingOutbox.cs:41`). It has one member, `Task<IReadOnlyDictionary<RoutingKey, int>> GetOutstandingMessageCountsByTopicAsync(IReadOnlyDictionary<RoutingKey, TimeSpan> dispatchedSinceByTopic, RequestContext? requestContext, int maxCountPerTopic, Dictionary<string, object>? args = null, CancellationToken cancellationToken = default)`.
  - Its `///` documentation states every contract bullet:
    - exactly the requested topics are answered, with 0 for a topic with no stale message;
    - each topic is counted by the message's header topic;
    - `args` never narrows the topics;
    - each count is at most the ceiling, per topic;
    - no message body is transferred;
    - circuit-breaker tripped topics are not excluded;
    - the call is safe at the same time as posting-path calls on the same instance;
    - it honours the token;
    - the errors are `ArgumentNullException`, `ArgumentException` and `ArgumentOutOfRangeException`, and the 365-day bound is stated in prose.
  - Verification: nothing implements or calls it; the solution builds.

- [x] **TEST + IMPLEMENT: T2.2 The per-topic count returns each requested topic's stale count, capped at the ceiling (AC-34)**
  - **USE COMMAND**: `/test-first InMemoryOutbox counts stale messages per requested topic at each topic's age, capped at the ceiling per topic`
  - Test location: "tests/Paramore.Brighter.Base.Test/Outbox/" (base) and "tests/Paramore.Brighter.InMemory.Tests/Outbox/" (derived)
  - Test file: `TopicCountingOutboxBaseTests.cs` with the case `When_counting_by_topic_should_return_each_topic_stale_count_capped_at_the_ceiling`, run through `InMemoryTopicCountingOutboxTests.cs`
  - Test should verify:
    - AC-34's store: on "orders", 5 undispatched messages 10 min old, 2 dispatched messages 10 min old and 2 undispatched messages 1 min old; on "payments", 1 undispatched message 3 min old; on "audit", 4 undispatched messages 10 min old;
    - the request is "orders" at 5 min, "payments" at 2 min and "refunds" at 5 min;
    - with C 100 the result is 5, 1 and 0; with C 3 it is 3, 1 and 0;
    - with "orders" at 30 s and C 100, "orders" is 7.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - add `TopicCountingOutboxBaseTests<TTransaction>`, modelled on `tests/Paramore.Brighter.Base.Test/Outbox/CausationTrackingOutboxBaseTests.cs:16-64`, with the members of ADR 0086 :248-256: `Outbox`, its store hooks, its `ReadConsistencyTimeout` (the retry a follow-up on an eventually consistent index needs, per the ADR 0086 risk at :417), `AddMessageWithAgeAsync` and `BeforeCount`. Each case adds its messages oldest first, all before its first count, and calls `BeforeCount` immediately before each call to the capability (ADR 0086 :263, :269). The `///` summary of `AddMessageWithAgeAsync` gives the contract of ADR 0086 :262, and says that a store class, whose clock keeps moving, needs each message's age kept well away from every requested topic age (ADR 0086 :271);
    - add `InMemoryTopicCountingOutboxTests`, deriving the base as `tests/Paramore.Brighter.InMemory.Tests/Outbox/InMemoryCausationTrackingOutboxTests.cs:34-39` does, on a `FakeTimeProvider` with one fixed instant later than the clock's start by more than any age a case uses. `AddMessageWithAgeAsync` sets the clock to that instant minus the age with `SetUtcNow` and then adds the message, and `BeforeCount` advances the clock to the fixed instant (ADR 0086 :269);
    - add `IAmATopicCountingOutbox` to `InMemoryOutbox` (`src/Paramore.Brighter/InMemoryOutbox.cs:83`);
    - count in one pass over the entries, by header topic, with the overall count's boundary `WriteTime <= now - age` (`:634`), capping each topic at the ceiling;
    - return a completed task, as the overall count does (`:646-655`).

- [x] **CHARACTERISE: T2.3 The per-topic result holds exactly the requested topics**
  - **USE COMMAND**: `/test-first the per-topic result has an entry for every requested topic, 0 when it has none, and no entry for an unrequested topic`
  - Test location: "tests/Paramore.Brighter.Base.Test/Outbox/"
  - Test file: `TopicCountingOutboxBaseTests.cs` with the case `When_counting_by_topic_should_answer_exactly_the_requested_topics`, run through `tests/Paramore.Brighter.InMemory.Tests/Outbox/InMemoryTopicCountingOutboxTests.cs`
  - Test should verify:
    - with AC-34's store, the result's keys are exactly "orders", "payments" and "refunds";
    - "refunds" is present with 0;
    - "audit" has no entry.
    - 🔁 RED mutation: in `InMemoryOutbox`, seed the result with every header topic found in the store.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE once RED is observed — before committing if the test was green on arrival, before implementing if it was not** *(fires in the `review-before` gear, which is the default)*
  - Implementation should (only if the test is RED on arrival):
    - build the result from the requested map's keys only, in `src/Paramore.Brighter/InMemoryOutbox.cs`.

- [x] **CHARACTERISE: T2.4 `args` does not narrow the requested topics and is not changed**
  - **USE COMMAND**: `/test-first args with a Topic entry does not narrow the per-topic count and holds the same entries after the call`
  - Test location: "tests/Paramore.Brighter.Base.Test/Outbox/"
  - Test file: `TopicCountingOutboxBaseTests.cs` with the case `When_counting_by_topic_with_a_topic_in_args_should_not_narrow_the_topics`, run through `tests/Paramore.Brighter.InMemory.Tests/Outbox/InMemoryTopicCountingOutboxTests.cs`
  - Test should verify:
    - with `args` of `{"Topic": "orders"}` and C 100, the result is still 5, 1 and 0 (AC-34's last clause);
    - `args` holds the same entries after the call.
    - 🔁 RED mutation: in `InMemoryOutbox`, narrow the requested topics to `args["Topic"]` when it is set.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE once RED is observed — before committing if the test was green on arrival, before implementing if it was not** *(fires in the `review-before` gear, which is the default)*
  - Implementation should (only if the test is RED on arrival):
    - ignore `args` in the capability, as the overall count does (`src/Paramore.Brighter/InMemoryOutbox.cs:617-643`).

- [x] **CHARACTERISE: T2.5 A message counts toward its header topic, not its `ProducerTopic` bag entry**
  - **USE COMMAND**: `/test-first a message whose header topic differs from its ProducerTopic bag entry counts toward its header topic`
  - Test location: "tests/Paramore.Brighter.Base.Test/Outbox/"
  - Test file: `TopicCountingOutboxBaseTests.cs` with the case `When_counting_by_topic_should_count_a_message_toward_its_header_topic`, run through `tests/Paramore.Brighter.InMemory.Tests/Outbox/InMemoryTopicCountingOutboxTests.cs`
  - Test should verify:
    - a stale message with header topic "orders.v2" and bag entry `Message.ProducerTopicHeaderName` = "orders" gives 0 for "orders" and 1 for "orders.v2" when both are requested.
    - 🔁 RED mutation: in `InMemoryOutbox`, key the count by the `ProducerTopic` bag entry when it is present.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE once RED is observed — before committing if the test was green on arrival, before implementing if it was not** *(fires in the `review-before` gear, which is the default)*
  - Implementation should (only if the test is RED on arrival):
    - key each entry by `Message.Header.Topic` in `src/Paramore.Brighter/InMemoryOutbox.cs`.

- [x] **CHARACTERISE: T2.6 An empty request returns an empty result**
  - **USE COMMAND**: `/test-first an empty topic map returns an empty per-topic result`
  - Test location: "tests/Paramore.Brighter.Base.Test/Outbox/"
  - Test file: `TopicCountingOutboxBaseTests.cs` with the case `When_counting_by_topic_with_no_topics_should_return_an_empty_result`, run through `tests/Paramore.Brighter.InMemory.Tests/Outbox/InMemoryTopicCountingOutboxTests.cs`
  - Test should verify:
    - an empty `dispatchedSinceByTopic` with AC-34's store returns a non-null, empty dictionary.
    - 🔁 RED mutation: in `InMemoryOutbox`, when the map is empty, return every header topic in the store with its count.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE once RED is observed — before committing if the test was green on arrival, before implementing if it was not** *(fires in the `review-before` gear, which is the default)*
  - Implementation should (only if the test is RED on arrival):
    - return an empty dictionary without reading the store, in `src/Paramore.Brighter/InMemoryOutbox.cs`.

- [x] **TEST + IMPLEMENT: T2.7 A null topic map throws `ArgumentNullException`**
  - **USE COMMAND**: `/test-first a null topic map throws ArgumentNullException`
  - Test location: "tests/Paramore.Brighter.Base.Test/Outbox/"
  - Test file: `TopicCountingOutboxBaseTests.cs` with the case `When_counting_by_topic_with_a_null_map_should_throw_argument_null`, run through `tests/Paramore.Brighter.InMemory.Tests/Outbox/InMemoryTopicCountingOutboxTests.cs`
  - Test should verify:
    - `GetOutstandingMessageCountsByTopicAsync(null!, ...)` throws `ArgumentNullException` naming `dispatchedSinceByTopic`.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - guard the map at entry in `src/Paramore.Brighter/InMemoryOutbox.cs`.

- [x] **TEST + IMPLEMENT: T2.8 An empty topic throws `ArgumentException`**
  - **USE COMMAND**: `/test-first a request that holds RoutingKey.Empty throws ArgumentException`
  - Test location: "tests/Paramore.Brighter.Base.Test/Outbox/"
  - Test file: `TopicCountingOutboxBaseTests.cs` with the case `When_counting_by_topic_with_an_empty_topic_should_throw_argument`, run through `tests/Paramore.Brighter.InMemory.Tests/Outbox/InMemoryTopicCountingOutboxTests.cs`
  - Test should verify:
    - a map holding `RoutingKey.Empty` at 5 min throws `ArgumentException`.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - reject a topic for which `RoutingKey.IsNullOrEmpty` is true, in `src/Paramore.Brighter/InMemoryOutbox.cs`;
    - review only, with no test: a null topic cannot be a `Dictionary` key (ADR 0086 :379). Note that in the PR description.

- [x] **TEST + IMPLEMENT: T2.9 An age outside (0, 365 days] throws `ArgumentException`**
  - **USE COMMAND**: `/test-first per-topic ages of zero, minus one second and 365 days plus one tick throw ArgumentException, and 365 days is accepted`
  - Test location: "tests/Paramore.Brighter.Base.Test/Outbox/"
  - Test file: `TopicCountingOutboxBaseTests.cs` with the cases `When_counting_by_topic_with_an_age_out_of_range_should_throw_argument` (theory: 0, -1 s, 365 d + 1 tick) and `When_counting_by_topic_with_an_age_of_365_days_should_count`, run through `tests/Paramore.Brighter.InMemory.Tests/Outbox/InMemoryTopicCountingOutboxTests.cs`
  - Test should verify:
    - each out-of-range age throws `ArgumentException` naming the topic;
    - 365 days returns a result without an exception.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - validate each age before the pass, in `src/Paramore.Brighter/InMemoryOutbox.cs`.

- [x] **TEST + IMPLEMENT: T2.10 A ceiling of 0 throws `ArgumentOutOfRangeException`**
  - **USE COMMAND**: `/test-first a per-topic ceiling below one throws ArgumentOutOfRangeException`
  - Test location: "tests/Paramore.Brighter.Base.Test/Outbox/"
  - Test file: `TopicCountingOutboxBaseTests.cs` with the case `When_counting_by_topic_with_a_ceiling_of_zero_should_throw_out_of_range`, run through `tests/Paramore.Brighter.InMemory.Tests/Outbox/InMemoryTopicCountingOutboxTests.cs`
  - Test should verify:
    - `maxCountPerTopic` of 0 throws `ArgumentOutOfRangeException` naming `maxCountPerTopic`.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - guard the ceiling at entry in `src/Paramore.Brighter/InMemoryOutbox.cs`.

- [x] **CHARACTERISE: T2.11 The per-topic count is safe beside adds, marks and the overall count (ADR 0086 risk :418)**
  - **USE COMMAND**: `/test-first the per-topic count runs safely while other tasks add, mark dispatched and run the overall count on the same outbox`
  - Test location: "tests/Paramore.Brighter.Base.Test/Outbox/"
  - Test file: `TopicCountingOutboxBaseTests.cs` with the case `When_counting_by_topic_beside_posting_path_calls_should_answer_every_requested_topic`, run through `tests/Paramore.Brighter.InMemory.Tests/Outbox/InMemoryTopicCountingOutboxTests.cs`
  - Test should verify:
    - while other tasks call the outbox's own `AddAsync`, `MarkDispatchedAsync` and `GetOutstandingMessageCountAsync` (never `AddMessageWithAgeAsync`), repeated per-topic calls never throw;
    - each result has exactly one entry per requested topic. The request includes one topic that no task writes to.
    - 🔁 RED mutation: in `InMemoryOutbox`, leave out of the result each requested topic whose count is 0. It trips the exactness clause only. `Requests` is a `ConcurrentDictionary` (`src/Paramore.Brighter/InMemoryBox.cs:52`), whose enumerator is safe beside writes, so no compiling mutation of the per-topic count makes the "never throw" clause fail reliably; that clause guards a later change of the collection.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE once RED is observed — before committing if the test was green on arrival, before implementing if it was not** *(fires in the `review-before` gear, which is the default)*
  - Implementation should (only if the test is RED on arrival):
    - iterate a snapshot of the entries, or the concurrent collection's safe enumerator, in `src/Paramore.Brighter/InMemoryOutbox.cs`.

## Phase 3: The readings and the meter (ADR 0085 step 3)

*Depends on Phase 1.*

Tests construct the meter directly over an `IMeterFactory` from `new ServiceCollection().AddMetrics()`, as `tests/Paramore.Brighter.Core.Tests/Tasks/When_confirmations_are_waiting_should_report_the_queue_depth.cs:21` constructs `PublishConfirmationMeter`. A `MeterListener` records the measurements.

- [x] **TEST + IMPLEMENT: T3.1 Started readings with topics report eight zero counter series and no gauge**
  - **USE COMMAND**: `/test-first a meter over readings started with topics reports eight zero measurement-counter series and no gauge value`
  - Test location: "tests/Paramore.Brighter.Core.Tests/Observability/StaleMessages/"
  - Test file: `When_readings_are_started_with_topics_should_report_eight_zero_counter_series.cs`
  - Test should verify:
    - after `Start("InMemoryOutbox", measuresTopics: true)`, `paramore.brighter.outbox.stale.measurements` reports 8 series of 0;
    - the series are {overall, per_topic} × {completed, failed, abandoned, skipped}, on `paramore.brighter.outbox.stale.count` and `paramore.brighter.outbox.stale.outcome`, each with `paramore.brighter.outbox.implementation` = "InMemoryOutbox";
    - the unit is `{measurement}`;
    - neither gauge reports a value.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - add `src/Paramore.Brighter/Observability/StaleMessageCount.cs` (`Overall`, `PerTopic`) and `StaleMeasurementOutcome.cs` (`Completed`, `Failed`, `Abandoned`, `Skipped`), whose remarks say they are public for Brighter's own use;
    - add the sealed `OutboxStaleMessageReadings` (`Start`, `IsStarted`, `OutboxType`, `MeasuresTopics`, `GetTotal`), thread-safe;
    - add the sealed `OutboxStaleMessageMeter(IMeterFactory meterFactory, OutboxStaleMessageReadings? readings = null)` in `src/Paramore.Brighter/Observability/`, modelled on `PublishConfirmationMeter.cs:37-52`, with the meter on `BrighterSemanticConventions.MeterName`;
    - create the two `ObservableGauge<long>` instruments (`{message}`) and the `ObservableCounter<long>` (`{measurement}`) with the T1.1 names.

- [x] **TEST + IMPLEMENT: T3.2 Started readings without topics report the four overall counter series only**
  - **USE COMMAND**: `/test-first readings started without topics report only the four overall measurement-counter series`
  - Test location: "tests/Paramore.Brighter.Core.Tests/Observability/StaleMessages/"
  - Test file: `When_readings_are_started_without_topics_should_report_only_the_overall_counter_series.cs`
  - Test should verify:
    - after `Start(..., measuresTopics: false)` there are exactly 4 counter series, all `overall`, each 0;
    - no `per_topic` series exists, and neither gauge reports (FR-12.1).
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - yield the `per_topic` series only when `MeasuresTopics` is true, in `src/Paramore.Brighter/Observability/OutboxStaleMessageMeter.cs`.

- [x] **TEST + IMPLEMENT: T3.3 Readings that are not started report no series**
  - **USE COMMAND**: `/test-first a meter over readings that were never started reports no series on any stale instrument`
  - Test location: "tests/Paramore.Brighter.Core.Tests/Observability/StaleMessages/"
  - Test file: `When_readings_are_not_started_should_report_no_series.cs`
  - Test should verify:
    - with readings constructed and never started, a collection yields no measurement on any of the three instruments, which is how "disabled" looks.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - return no measurements from each callback while `IsStarted` is false, in `src/Paramore.Brighter/Observability/OutboxStaleMessageMeter.cs`.

- [x] **CHARACTERISE: T3.4 A meter without readings creates no instrument**
  - **USE COMMAND**: `/test-first a stale-message meter constructed without readings creates no instrument`
  - Test location: "tests/Paramore.Brighter.Core.Tests/Observability/StaleMessages/"
  - Test file: `When_the_meter_has_no_readings_should_create_no_instruments.cs`
  - Test should verify:
    - `new OutboxStaleMessageMeter(factory)` publishes none of the three instrument names to a `MeterListener`'s `InstrumentPublished`.
    - 🔁 RED mutation: create the instruments whatever the readings.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE once RED is observed — before committing if the test was green on arrival, before implementing if it was not** *(fires in the `review-before` gear, which is the default)*
  - Implementation should (only if the test is RED on arrival):
    - return from the constructor before creating instruments when `readings` is null, in `src/Paramore.Brighter/Observability/OutboxStaleMessageMeter.cs`.

- [x] **TEST + IMPLEMENT: T3.5 Completed counts appear on their gauges, one series per topic**
  - **USE COMMAND**: `/test-first CompleteOverall and CompletePerTopic report on the overall gauge and on one topic-gauge series per topic`
  - Test location: "tests/Paramore.Brighter.Core.Tests/Observability/StaleMessages/"
  - Test file: `When_both_counts_complete_should_report_each_gauge.cs`
  - Test should verify:
    - after `CompleteOverall(2)` and `CompletePerTopic({orders: 2, payments: 0})`, the overall gauge reports 2 with only the implementation attribute;
    - the topic gauge reports 2 and 0, each with the implementation attribute plus `messaging.destination.name` (as `OutboxMeter.cs:135` tags);
    - a value of 0 is reported as 0;
    - the `completed` total is 1 for each count.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - add `CompleteOverall(int)`, `CompletePerTopic(IReadOnlyDictionary<RoutingKey,int>)`, `TryGetLatestOverall` and `TryGetLatestPerTopic` to `src/Paramore.Brighter/Observability/OutboxStaleMessageReadings.cs`;
    - read them from the gauge callbacks.

- [x] **TEST + IMPLEMENT: T3.6 A failed per-topic count clears only the topic gauge**
  - **USE COMMAND**: `/test-first Fail of the per-topic count after both completes clears the topic gauge, keeps the overall value and counts one per_topic failure`
  - Test location: "tests/Paramore.Brighter.Core.Tests/Observability/StaleMessages/"
  - Test file: `When_the_per_topic_count_fails_should_clear_only_the_topic_gauge.cs`
  - Test should verify:
    - after both completes and then `Fail(PerTopic)`, the topic gauge has no series;
    - the overall gauge still reports its V;
    - the `per_topic` `failed` total is 1 (ADR 0085 step 3).
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - add `Fail(StaleMessageCount)`, which clears that count's latest values and increments its total, in `src/Paramore.Brighter/Observability/OutboxStaleMessageReadings.cs`.

- [x] **TEST + IMPLEMENT: T3.7 Skipped due times add to the skipped total**
  - **USE COMMAND**: `/test-first Skip of the overall count by three adds three to the overall skipped total`
  - Test location: "tests/Paramore.Brighter.Core.Tests/Observability/StaleMessages/"
  - Test file: `When_the_overall_count_skips_three_due_times_should_count_three_skips.cs`
  - Test should verify:
    - `Skip(Overall, 3)` makes the `overall` `skipped` total 3;
    - the overall gauge has no series afterwards.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - add `Skip(StaleMessageCount, int)`, which clears the count's latest value and adds to its total, in `src/Paramore.Brighter/Observability/OutboxStaleMessageReadings.cs`.

- [x] **TEST + IMPLEMENT: T3.8 Stopping clears both gauges and keeps the totals**
  - **USE COMMAND**: `/test-first Stop after both completes clears both gauges and leaves every outcome total unchanged`
  - Test location: "tests/Paramore.Brighter.Core.Tests/Observability/StaleMessages/"
  - Test file: `When_readings_are_stopped_should_clear_both_gauges_and_keep_the_totals.cs`
  - Test should verify:
    - after both completes and then `Stop()`, neither gauge has a series;
    - the eight counter series keep their totals.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - add `Stop()` to `src/Paramore.Brighter/Observability/OutboxStaleMessageReadings.cs`.

- [x] **TEST + IMPLEMENT: T3.9 The readings reject invalid writes**
  - **USE COMMAND**: `/test-first the readings reject a negative value, a skip below one and a per-topic write when topics are not measured`
  - Test location: "tests/Paramore.Brighter.Core.Tests/Observability/StaleMessages/"
  - Test file: `When_readings_receive_an_invalid_write_should_throw.cs`
  - Test should verify:
    - `CompleteOverall(-1)` and a per-topic map with a negative value throw `ArgumentOutOfRangeException`;
    - `Skip(Overall, 0)` throws `ArgumentOutOfRangeException`;
    - `CompletePerTopic` after `Start(..., measuresTopics: false)` throws `InvalidOperationException`;
    - no total or latest value changes after any of these throws.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - guard each writer, per the readings contract at ADR 0085 :480-520, in `src/Paramore.Brighter/Observability/OutboxStaleMessageReadings.cs`.

- [x] **TEST + IMPLEMENT: T3.10 An abandoned count clears that count's value and counts one abandonment**
  - **USE COMMAND**: `/test-first Abandon of the overall count clears the overall gauge and adds one to its abandoned total`
  - Test location: "tests/Paramore.Brighter.Core.Tests/Observability/StaleMessages/"
  - Test file: `When_the_overall_count_is_abandoned_should_clear_the_overall_gauge.cs`
  - Test should verify:
    - after `CompleteOverall(3)` and then `Abandon(Overall)`, the overall gauge has no series and the `overall` `abandoned` total is 1;
    - the topic gauge keeps its values.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - add `Abandon(StaleMessageCount)` to `src/Paramore.Brighter/Observability/OutboxStaleMessageReadings.cs`.

## Phase 4: The meter in `AddBrighterInstrumentation` (ADR 0085 step 4)

*Depends on Phase 3.*

- [x] **STRUCTURAL: T4.1 Extract the deferred `Configure` block into a named private method**
  - **USE COMMAND**: `/tidy-first extract the deferred Configure block of AddBrighterInstrumentation into a named private method`
  - In `src/Paramore.Brighter.Extensions.Diagnostics/BrighterMetricsBuilderExtensions.cs`, move the body of the deferred `Configure` at `:49-50` into a private method named for its intent, for example `CreateMetersNothingDependsOn(IServiceProvider)`.
  - Remove the in-body comment at `:48`. The method's name now carries that intent (CONTRIBUTING.md, "Comments in Code").
  - Verification: `PublishConfirmationMeter` is still created when the provider is built; existing `Observability/` and `Tasks/` tests stay green.

- [x] **TEST + IMPLEMENT: T4.2 With started readings registered, the stale instruments report on the shared container**
  - **USE COMMAND**: `/test-first AddBrighterInstrumentation creates the stale-message meter when the meter provider is built, and its instruments report from registered started readings`
  - Test location: "tests/Paramore.Brighter.Core.Tests/Observability/StaleMessages/"
  - Test file: `When_brighter_instrumentation_is_added_with_started_readings_should_report_the_stale_instruments.cs`
  - Test should verify:
    - on `StaleMessageMetricsFixture`, with an `OutboxStaleMessageReadings` singleton registered and started with topics, resolving the `MeterProvider` makes all three instruments publish;
    - a collection yields the eight zero counter series.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - add `services.TryAddSingleton<OutboxStaleMessageMeter>()` beside `:42` of `src/Paramore.Brighter.Extensions.Diagnostics/BrighterMetricsBuilderExtensions.cs`;
    - resolve the meter in the method extracted in T4.1;
    - add `TestDoubles/StaleMessageMetricsFixture.cs`, the listener fixture for every later listener-based test.

- [x] **CHARACTERISE: T4.3 Without readings registered, no stale instrument exists**
  - **USE COMMAND**: `/test-first AddBrighterInstrumentation without registered readings publishes no stale-message instrument`
  - Test location: "tests/Paramore.Brighter.Core.Tests/Observability/StaleMessages/"
  - Test file: `When_brighter_instrumentation_is_added_without_readings_should_create_no_stale_instruments.cs`
  - Test should verify:
    - on the fixture with no readings registered, none of the three names is published after the `MeterProvider` is resolved.
    - 🔁 RED mutation: add `services.TryAddSingleton<OutboxStaleMessageReadings>()` inside `AddBrighterInstrumentation`.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE once RED is observed — before committing if the test was green on arrival, before implementing if it was not** *(fires in the `review-before` gear, which is the default)*
  - Implementation should (only if the test is RED on arrival):
    - leave the readings unregistered by `AddBrighterInstrumentation`. Only `UseOutboxStaleMessageMonitor` registers them (Phase 5).

## Phase 5: Options and registration (ADR 0085 step 5)

*Depends on Phase 3.*

- [x] **TEST + IMPLEMENT: T5.1 Given only A, C, I and T take their defaults (AC-3 first clause)**
  - **USE COMMAND**: `/test-first options built from only the stale age default to a ceiling of 100, an interval of one minute and a timeout of ten seconds`
  - Test location: "tests/Paramore.Brighter.Core.Tests/Observability/StaleMessages/"
  - Test file: `When_only_the_stale_age_is_given_should_use_the_default_settings.cs`
  - Test should verify:
    - `new TimedOutboxStaleMessageMonitorOptions(TimeSpan.FromMinutes(5))` has C 100, I 1 min and T 10 s, with T < I;
    - `MaxStaleMessageAge` is 365 d, `MaxCountCeiling` 1000 and `MaxMeasurementInterval` 4,294,967,294 ms.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - add `src/Paramore.Brighter.Outbox.Hosting/TimedOutboxStaleMessageMonitorOptions.cs`;
    - give the default C its own value of 100. It equals `DefaultOutstandingCountCeiling` (`src/Paramore.Brighter/OutboxProducerMediator.cs:80`), but that constant is private and the two may diverge.

- [x] **TEST + IMPLEMENT: T5.2 An invalid A throws `ConfigurationException` and registers nothing (AC-2)**
  - **USE COMMAND**: `/test-first UseOutboxStaleMessageMonitor with an age of zero, minus one second or TimeSpan.MaxValue throws ConfigurationException naming the setting and value`
  - Test location: "tests/Paramore.Brighter.Core.Tests/Observability/StaleMessages/"
  - Test file: `When_the_stale_age_is_invalid_should_throw_a_configuration_exception.cs`
  - Test should verify:
    - for each of 0, -1 s and `TimeSpan.MaxValue`, the extension throws `ConfigurationException`, whose message names the stale age setting and the value;
    - the service collection has the same registrations as before the call.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - add `UseOutboxStaleMessageMonitor(this IBrighterBuilder, TimeSpan staleMessageAge, Action<TimedOutboxStaleMessageMonitorOptions>? configure = null)` beside `UseOutboxArchiver` in `src/Paramore.Brighter.Outbox.Hosting/HostedServiceCollectionExtensions.cs`;
    - validate after `configure` runs and before any registration.

- [x] **TEST + IMPLEMENT: T5.3 An invalid C throws `ConfigurationException` (AC-2)**
  - **USE COMMAND**: `/test-first a count ceiling of zero or int.MaxValue throws ConfigurationException naming the setting and value`
  - Test location: "tests/Paramore.Brighter.Core.Tests/Observability/StaleMessages/"
  - Test file: `When_the_count_ceiling_is_invalid_should_throw_a_configuration_exception.cs`
  - Test should verify:
    - C of 0 and of `int.MaxValue` each throw, naming the ceiling setting and the value, and nothing is registered.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - check 1 ≤ C ≤ `MaxCountCeiling` in the options validation.

- [x] **TEST + IMPLEMENT: T5.4 An invalid I throws `ConfigurationException` (AC-2)**
  - **USE COMMAND**: `/test-first a measurement interval of zero or fifty days throws ConfigurationException naming the setting and value`
  - Test location: "tests/Paramore.Brighter.Core.Tests/Observability/StaleMessages/"
  - Test file: `When_the_measurement_interval_is_invalid_should_throw_a_configuration_exception.cs`
  - Test should verify:
    - I of 0 and of 50 days each throw, naming the interval setting and the value, and nothing is registered.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - check 0 < I ≤ `MaxMeasurementInterval` in the options validation.

- [x] **TEST + IMPLEMENT: T5.5 An invalid T throws `ConfigurationException` (AC-2)**
  - **USE COMMAND**: `/test-first a measurement timeout of zero, equal to the interval or above it throws ConfigurationException naming the setting and value`
  - Test location: "tests/Paramore.Brighter.Core.Tests/Observability/StaleMessages/"
  - Test file: `When_the_measurement_timeout_is_invalid_should_throw_a_configuration_exception.cs`
  - Test should verify:
    - T of 0, T equal to I (1 min each), and T of 2 min with I of 1 min each throw, naming the timeout setting and the value, and nothing is registered.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - check 0 < T < I in the options validation.

- [x] **CHARACTERISE: T5.6 Boundary settings are accepted (AC-3 second and third clauses, at registration)**
  - **USE COMMAND**: `/test-first the smallest valid settings and the maximum age, ceiling and interval are accepted at registration`
  - Test location: "tests/Paramore.Brighter.Core.Tests/Observability/StaleMessages/"
  - Test file: `When_settings_are_at_their_bounds_should_register_the_monitor.cs`
  - Test should verify:
    - A 1 ms, C 1, I 1 min and T 59 s register without an exception;
    - A 365 d with C 1000 registers;
    - I of 4,294,967,294 ms registers.
    - 🔁 RED mutation: reject an age equal to the maximum (`>=` in place of `>`).
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE once RED is observed — before committing if the test was green on arrival, before implementing if it was not** *(fires in the `review-before` gear, which is the default)*
  - Implementation should (only if the test is RED on arrival):
    - make every upper bound inclusive and every lower bound exclusive, as FR-2 states.

- [x] **CHARACTERISE: T5.7 The options and the readings are registered once, and the first call wins**
  - **USE COMMAND**: `/test-first a second UseOutboxStaleMessageMonitor call with valid settings leaves the first call's options and readings registered`
  - Test location: "tests/Paramore.Brighter.Core.Tests/Observability/StaleMessages/"
  - Test file: `When_the_monitor_is_added_twice_should_keep_the_first_settings.cs`
  - Test should verify:
    - after a call with A 5 min and then a call with A 10 min, the container holds one options registration and one readings registration;
    - the resolved options have A of 5 min.
    - 🔁 RED mutation: use `AddSingleton` in place of `TryAddSingleton` for the options.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE once RED is observed — before committing if the test was green on arrival, before implementing if it was not** *(fires in the `review-before` gear, which is the default)*
  - Implementation should (only if the test is RED on arrival):
    - register both with `TryAddSingleton`, as `UseOutboxArchiver` registers its options (`src/Paramore.Brighter.Outbox.Hosting/HostedServiceCollectionExtensions.cs:59`).

- [x] **TEST + IMPLEMENT: T5.8 A null builder throws `ArgumentNullException`**
  - **USE COMMAND**: `/test-first UseOutboxStaleMessageMonitor on a null builder throws ArgumentNullException`
  - Test location: "tests/Paramore.Brighter.Core.Tests/Observability/StaleMessages/"
  - Test file: `When_the_builder_is_null_should_throw_argument_null.cs`
  - Test should verify:
    - `((IBrighterBuilder)null!).UseOutboxStaleMessageMonitor(TimeSpan.FromMinutes(5))` throws `ArgumentNullException`.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - guard the builder first in `src/Paramore.Brighter.Outbox.Hosting/HostedServiceCollectionExtensions.cs`.

## Phase 6: The monitor and what it resolves at start (ADR 0085 step 6)

*Depends on Phases 1, 2 and 5.*

- [x] **TEST + IMPLEMENT: T6.1 Two calls register one hosted monitor**
  - **USE COMMAND**: `/test-first after two UseOutboxStaleMessageMonitor calls the container holds one hosted-service registration for the monitor`
  - Test location: "tests/Paramore.Brighter.Core.Tests/Observability/StaleMessages/"
  - Test file: `When_the_monitor_is_added_twice_should_register_one_hosted_service.cs`
  - Test should verify:
    - exactly one `IHostedService` descriptor has `TimedOutboxStaleMessageMonitor` as its implementation type.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - add `src/Paramore.Brighter.Outbox.Hosting/TimedOutboxStaleMessageMonitor.cs`: `IHostedService` and `IDisposable`, with constructor `(IServiceScopeFactory, OutboxStaleMessageReadings, TimedOutboxStaleMessageMonitorOptions, TimeProvider? timeProvider = null)`;
    - register it with `AddHostedService`, as `HostedServiceCollectionExtensions.cs:68` does. `AddHostedService` uses `TryAddEnumerable`, so a second call adds nothing.

- [x] **TEST + IMPLEMENT: T6.2 The monitor validates its options again when it is constructed**
  - **USE COMMAND**: `/test-first constructing the monitor with options made invalid after registration throws ConfigurationException`
  - Test location: "tests/Paramore.Brighter.Core.Tests/Observability/StaleMessages/"
  - Test file: `When_the_monitor_is_constructed_with_invalid_options_should_throw_a_configuration_exception.cs`
  - Test should verify:
    - options whose T was set equal to I after construction make the monitor's constructor throw `ConfigurationException` naming the timeout and its value.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - call the same validation from the constructor in `src/Paramore.Brighter.Outbox.Hosting/TimedOutboxStaleMessageMonitor.cs`.

- [x] **TEST + IMPLEMENT: T6.3 Starting without `AddProducers` fails with a message that names it**
  - **USE COMMAND**: `/test-first StartAsync without AddProducers, with either the outbox or the producers' configuration missing, throws ConfigurationException naming AddProducers and makes no count call`
  - Test location: "tests/Paramore.Brighter.Core.Tests/Observability/StaleMessages/"
  - Test file: `When_the_monitor_starts_without_producers_should_throw_a_configuration_exception.cs`
  - Test should verify:
    - with `UseOutboxStaleMessageMonitor` and no `AddProducers`, as a theory over two registrations, because ADR 0085 :453 names both: `StaleMessageCountingOutbox` registered on its own as the `IAmAnOutbox` singleton with no `IAmProducersConfiguration`, and a `ProducersConfiguration` registered on its own as `IAmProducersConfiguration` with no `IAmAnOutbox`. In each case `StartAsync` throws `ConfigurationException` whose message contains "AddProducers";
    - `readings.IsStarted` is false;
    - in the first case, after the throw, fake time advances by I, and the double records no call of either count. The first due time is start + I (ADR 0085 :449), so the clause can fail once Phase 8 starts the loop.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - in `StartAsync`, open a scope and resolve `IAmAnOutbox`, `IAmProducersConfiguration` and `IAmABoxTransactionProvider` with `GetService`. `AddEventBus` resolves the transaction provider in a scope, at `src/Paramore.Brighter.Extensions.DependencyInjection/ServiceCollectionExtensions.cs:713-720` (ADR: `:716-723`). Leave the scope open here; disposing it is T6.4;
    - throw when the outbox or the producers' configuration is missing.

- [x] **TEST + IMPLEMENT: T6.4 The start scope is disposed before `StartAsync` returns**
  - **USE COMMAND**: `/test-first the scope the monitor opens at start is disposed before StartAsync returns`
  - Test location: "tests/Paramore.Brighter.Core.Tests/Observability/StaleMessages/"
  - Test file: `When_the_monitor_starts_should_dispose_its_scope_before_returning.cs`
  - Test should verify:
    - a scoped, disposable `IAmABoxTransactionProvider<CommittableTransaction>` double, registered as `IAmABoxTransactionProvider` after `AddProducers` through a factory that keeps each instance it creates. Both registrations stay, and `GetService` returns the last one, so the monitor resolves the double rather than the provider `AddProducers` adds (`src/Paramore.Brighter.Extensions.DependencyInjection/ServiceCollectionExtensions.cs:355`). The monitor reads the transaction type from the double through `CommandProcessor.GetTransactionTypeFromTransactionProvider` (`src/Paramore.Brighter/CommandProcessor.cs:286-297`, ADR 0085 :455), so the transaction type is `CommittableTransaction`, which the default outbox implements (`src/Paramore.Brighter/InMemoryOutbox.cs:83`). By the time `StartAsync` returns, the instance the monitor resolved has recorded `Dispose`. The test resolves no command processor, so that is the only instance created.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - open the start scope in a `using` block in `src/Paramore.Brighter.Outbox.Hosting/TimedOutboxStaleMessageMonitor.cs`, so it is disposed before `StartAsync` returns, as `AddEventBus` disposes its scope at `ServiceCollectionExtensions.cs:713-720` (ADR: `:716-723`) and as ADR 0085 step 6 requires (:851);
    - keep only the resolved values, never the scope or the provider.

- [x] **TEST + IMPLEMENT: T6.5 An invalid `StaleMessageAge` on a topic fails start, naming the topic and value (AC-32)**
  - **USE COMMAND**: `/test-first an invalid StaleMessageAge on the orders publication fails StartAsync with a ConfigurationException naming orders and the value`
  - Test location: "tests/Paramore.Brighter.Core.Tests/Observability/StaleMessages/"
  - Test file: `When_a_publication_has_an_invalid_stale_age_should_fail_to_start.cs`
  - Test should verify:
    - for A_p of 0, -1 s, 365 d + 1 tick and `TimeSpan.MaxValue` on "orders", `StartAsync` throws `ConfigurationException` naming "orders" and the value;
    - the exception surfaces from `StartAsync` and is not swallowed;
    - after the throw, fake time advances by I, and `StaleMessageCountingOutbox`, set as `ProducersConfiguration.Outbox` so that `AddProducers` registers it as the `IAmAnOutbox` singleton (`src/Paramore.Brighter.Extensions.DependencyInjection/ServiceCollectionExtensions.cs:383`), records no call of either count. Registered beside `AddProducers` instead, it would compete with the outbox that `:383` registers, the default one from `:361` when `Outbox` is not set.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - read `IAmProducersConfiguration.ProducerRegistry` (`src/Paramore.Brighter/ProducersConfiguration.cs:109`), its `Producers` (`IAmAProducerRegistry.cs:43`) and each `Publication` (`IAmAMessageProducer.cs:36`) once;
    - validate every `StaleMessageAge` before building anything.

- [x] **TEST + IMPLEMENT: T6.6 An invalid `StaleMessageAge` on an empty topic fails start, naming the value (AC-32)**
  - **USE COMMAND**: `/test-first a StaleMessageAge of zero on a publication with an empty topic fails StartAsync naming the value`
  - Test location: "tests/Paramore.Brighter.Core.Tests/Observability/StaleMessages/"
  - Test file: `When_a_publication_with_an_empty_topic_has_an_invalid_stale_age_should_fail_to_start.cs`
  - Test should verify:
    - a publication with `RoutingKey.Empty` and A_p 0 makes `StartAsync` throw `ConfigurationException` naming the value;
    - after the throw, fake time advances by I, and `StaleMessageCountingOutbox`, set as `ProducersConfiguration.Outbox` as in T6.5, records no call of either count.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - validate a publication's age before skipping it for an empty topic, in `src/Paramore.Brighter.Outbox.Hosting/TimedOutboxStaleMessageMonitor.cs`.

- [x] **CHARACTERISE: T6.7 Valid publication ages, smaller or greater than A, are accepted (AC-32)**
  - **USE COMMAND**: `/test-first StaleMessageAge of one millisecond, of the maximum, or of ten minutes with A of five minutes starts without an exception`
  - Test location: "tests/Paramore.Brighter.Core.Tests/Observability/StaleMessages/"
  - Test file: `When_a_publication_has_a_valid_stale_age_should_start.cs`
  - Test should verify:
    - for each of 1 ms, 365 d and 10 min, `StartAsync` completes and `readings.IsStarted` is true.
    - 🔁 RED mutation: reject an age equal to the maximum.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE once RED is observed — before committing if the test was green on arrival, before implementing if it was not** *(fires in the `review-before` gear, which is the default)*
  - Implementation should (only if the test is RED on arrival):
    - share the age bound with the options' `MaxStaleMessageAge`.

- [x] **TEST + IMPLEMENT: T6.8 An outbox without the capability logs one Information entry at start (AC-29)**
  - **USE COMMAND**: `/test-first an outbox without the per-topic capability logs one Information entry at start naming the outbox type and no count per topic`
  - Test location: "tests/Paramore.Brighter.Core.Tests/Observability/StaleMessages/" (in `StaleMessageLoggingCollection`)
  - Test file: `When_the_outbox_has_no_topic_count_should_log_it_once_at_start.cs`
  - Test should verify:
    - with `StaleMessageCountingOutbox` and publications on "orders" and "payments", exactly one Information entry names `StaleMessageCountingOutbox` and the reason "no capability".
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - check `outbox is IAmATopicCountingOutbox`;
    - log through a `[LoggerMessage]` method on the `ApplicationLogging` logger, as the sweeper does.

- [x] **TEST + IMPLEMENT: T6.9 No measured topic logs the "no measured topic" reason (AC-29)**
  - **USE COMMAND**: `/test-first with the capability and no registry, an empty registry or only an empty-topic publication, start logs no measured topic`
  - Test location: "tests/Paramore.Brighter.Core.Tests/Observability/StaleMessages/" (in `StaleMessageLoggingCollection`)
  - Test file: `When_no_topic_is_measured_should_log_it_once_at_start.cs`
  - Test should verify, with `StaleMessageTopicCountingOutbox`:
    - an `IAmProducersConfiguration` double whose `ProducerRegistry` is null, an empty `ProducerRegistry`, and a registry whose only publication has `RoutingKey.Empty` each log one Information entry giving "no measured topic".
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - skip publications whose topic is `RoutingKey.IsNullOrEmpty`;
    - build the distinct non-empty topics into a read-only map of A_t (the per-topic call that uses the map lands in T8.7).

- [x] **CHARACTERISE: T6.10 When both reasons apply, one entry gives "no capability" (AC-29)**
  - **USE COMMAND**: `/test-first an outbox without the capability and an empty registry logs exactly one entry giving no capability`
  - Test location: "tests/Paramore.Brighter.Core.Tests/Observability/StaleMessages/" (in `StaleMessageLoggingCollection`)
  - Test file: `When_both_reasons_apply_should_log_no_capability_once.cs`
  - Test should verify:
    - `StaleMessageCountingOutbox` with an empty `ProducerRegistry` logs exactly one Information entry, with the reason "no capability".
    - 🔁 RED mutation: check the number of topics before the capability.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE once RED is observed — before committing if the test was green on arrival, before implementing if it was not** *(fires in the `review-before` gear, which is the default)*
  - Implementation should (only if the test is RED on arrival):
    - give the capability check precedence in `src/Paramore.Brighter.Outbox.Hosting/TimedOutboxStaleMessageMonitor.cs`.

- [x] **TEST + IMPLEMENT: T6.11 Start records the outbox type and whether topics are measured**
  - **USE COMMAND**: `/test-first StartAsync starts the readings with the outbox type name and whether topics are measured`
  - Test location: "tests/Paramore.Brighter.Core.Tests/Observability/StaleMessages/"
  - Test file: `When_the_monitor_starts_should_start_the_readings.cs`
  - Test should verify:
    - with `StaleMessageTopicCountingOutbox` and "orders", `IsStarted` is true, `OutboxType` is "StaleMessageTopicCountingOutbox" and `MeasuresTopics` is true;
    - with `StaleMessageCountingOutbox`, `MeasuresTopics` is false.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - call `readings.Start(outbox.GetType().Name, measuresTopics)` as the last step of `StartAsync`.

## Phase 7: Rejecting an outbox without the async count (ADR 0085 step 7)

*Depends on Phase 6.*

- [x] **TEST + IMPLEMENT: T7.1 A sync-only outbox fails start (Q-3)**
  - **USE COMMAND**: `/test-first a sync-only outbox fails StartAsync with a ConfigurationException saying it is sync-only and no count is called`
  - Test location: "tests/Paramore.Brighter.Core.Tests/Observability/StaleMessages/"
  - Test file: `When_the_outbox_is_sync_only_should_fail_to_start.cs`
  - Test should verify:
    - with `SyncOnlyOutbox`, `StartAsync` throws `ConfigurationException` naming the outbox type and saying it is sync-only;
    - no call of either count is recorded, and the readings are not started.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - find the transaction type with `CommandProcessor.GetTransactionTypeFromTransactionProvider`, unchanged;
    - check the instance against `IAmAnOutboxAsync<Message, TTransaction>` built with `MakeGenericType`, as `AddProducers` does (`ServiceCollectionExtensions.cs:365-372` (ADR: `:368-375`) and `:510-515` (ADR: `:513-518`));
    - check the instance rather than resolve the closed interface, because the deferred overload registers only `IAmAnOutbox` (`:498` (ADR: `:501`)).

- [x] **TEST + IMPLEMENT: T7.2 An outbox with neither count interface fails start**
  - **USE COMMAND**: `/test-first an outbox that implements neither outbox count interface fails StartAsync naming that`
  - Test location: "tests/Paramore.Brighter.Core.Tests/Observability/StaleMessages/"
  - Test file: `When_the_outbox_implements_neither_interface_should_fail_to_start.cs`
  - Test should verify:
    - with `NeitherInterfaceOutbox`, `StartAsync` throws `ConfigurationException` naming the type and saying it implements neither interface;
    - the readings are not started.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - distinguish the two messages by whether the instance implements `IAmAnOutboxSync<Message, TTransaction>`.

## Phase 8: The loop and the schedule (ADR 0085 step 8)

*Depends on Phases 2, 4 and 7.*

Every task here uses the determinism rules and `StaleMessageMetricsFixture`. Phase 8 builds the happy path. Deadlines, failures and single flight are Phase 9, and stopping is Phase 10.

- [x] **TEST + IMPLEMENT: T8.1 The first value appears after the first due time, on the Brighter meter (AC-6)**
  - **USE COMMAND**: `/test-first with no ActivityListener the overall gauge has no value at 12:00:30 and reports the 12:01:00 count at 12:01:30`
  - Test location: "tests/Paramore.Brighter.Core.Tests/Observability/StaleMessages/"
  - Test file: `When_the_first_due_time_passes_should_report_the_overall_count.cs`
  - Test should verify:
    - no `ActivityListener` is registered, and no value has been received at 12:00:30;
    - at 12:01:30 a value has been received on meter `Paramore.Brighter`, with a name starting `paramore.brighter.outbox.` and unit `{message}`, equal to the 12:01:00 V (0 against an empty `InMemoryOutbox`).
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should, in `src/Paramore.Brighter.Outbox.Hosting/TimedOutboxStaleMessageMonitor.cs`:
    - start the loop with `Task.Run` under `ExecutionContext.SuppressFlow`, as `src/Paramore.Brighter.Outbox.Hosting/TimedOutboxSweeper.cs:103-105` starts its thread;
    - measure elapsed time from a `TimeProvider` timestamp (`:161-163`), and compute grid points start + k×I in whole ticks;
    - on each wake, compute for each count the latest grid point that has passed. Row 2 (woke early): wait again. Row 3 (M > 1 passed): `Skip(count, M - 1)` with one Warning carrying M - 1, then measure the latest. Never rely on a periodic timer's own catch-up. A fake clock cannot make row 3, so no test drives it, and T8.25 reviews it;
    - wait through one helper over `TimeProvider.CreateTimer`, whose completion source uses `RunContinuationsAsynchronously`, and check the clock again after the timer exists (`:192-195`);
    - build the stop task as a `TaskCompletionSource` with `RunContinuationsAsynchronously`, completed from the stop token's registration;
    - after each `Task.WhenAny`, yield with `Task.Yield` on netstandard2.0, or `ConfigureAwaitOptions.ForceYielding` on net8.0 and later;
    - start the overall count with its own `Task.Run`: A, C, `requestContext` null, a fresh `CancellationTokenSource` per call, and `args` an empty dictionary for now (T8.5 adds the bag);
    - record `CompleteOverall` and log at Debug through `[LoggerMessage]`.

- [x] **CHARACTERISE: T8.2 The overall count follows the store's stale messages (AC-4)**
  - **USE COMMAND**: `/test-first the overall gauge records 1, then 0 after m1 is dispatched, then 1 when m3 becomes stale`
  - Test location: "tests/Paramore.Brighter.Core.Tests/Observability/StaleMessages/"
  - Test file: `When_stale_messages_change_should_record_the_overall_count.cs`
  - Test should verify:
    - `InMemoryOutbox` on the fake clock holds m1 (12:00:00, undispatched), m2 (12:00:00, dispatched at 12:00:01) and m3 (12:04:00, undispatched);
    - at 12:06:30, V for 12:06:00 is 1;
    - after m1 is dispatched at 12:06:40, at 12:07:30 V is 0, received as a value;
    - at 12:10:30 V is 1;
    - the container registers no `TimedOutboxSweeper` (the first sentence of AC-13).
    - 🔁 RED mutation: pass I in place of A as `dispatchedSince`.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE once RED is observed — before committing if the test was green on arrival, before implementing if it was not** *(fires in the `review-before` gear, which is the default)*
  - Implementation should (only if the test is RED on arrival):
    - keep each due time's call independent of earlier values. The gauge shows only the latest completed reading.

- [x] **CHARACTERISE: T8.3 The overall count stops at the ceiling (AC-5)**
  - **USE COMMAND**: `/test-first with a ceiling of three the overall value is 0, 2, 3 and 3 for 0, 2, 3 and 50 stale messages`
  - Test location: "tests/Paramore.Brighter.Core.Tests/Observability/StaleMessages/"
  - Test file: `When_stale_messages_exceed_the_ceiling_should_record_the_ceiling.cs`
  - Test should verify:
    - with C 3 and S undispatched messages added more than 5 min before the count, for S of 0, 2, 3 and 50 (a theory), V is 0, 2, 3 and 3.
    - 🔁 RED mutation: pass C + 1 as `maxCount`.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE once RED is observed — before committing if the test was green on arrival, before implementing if it was not** *(fires in the `review-before` gear, which is the default)*
  - Implementation should (only if the test is RED on arrival):
    - pass C as `maxCount` on every overall call.

- [x] **CHARACTERISE: T8.4 One overall call per due time, however often metrics are collected (AC-8, overall)**
  - **USE COMMAND**: `/test-first advancing in one-second steps to 12:03:30 makes exactly three overall calls at 12:01, 12:02 and 12:03 whether the listener collects every ten seconds or every second`
  - Test location: "tests/Paramore.Brighter.Core.Tests/Observability/StaleMessages/"
  - Test file: `When_time_advances_three_intervals_should_make_one_overall_call_per_due_time.cs`
  - Test should verify:
    - `StaleMessageCountingOutbox` returns 0 at once;
    - exactly 3 async overall calls are recorded, at 12:01:00, 12:02:00 and 12:03:00 fake time;
    - a theory over collection periods of 10 s and 1 s.
    - 🔁 RED mutation: give `OutboxStaleMessageReadings` a callback that the monitor sets to start an overall count, and invoke it from the overall gauge's observe callback. Each collection then adds a call, so the 1 s run records more than 3.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE once RED is observed — before committing if the test was green on arrival, before implementing if it was not** *(fires in the `review-before` gear, which is the default)*
  - Implementation should (only if the test is RED on arrival):
    - start each count only from the loop at its due time, never from a gauge callback.

- [x] **TEST + IMPLEMENT: T8.5 The overall call passes the producers' bag as `args` (AC-14, bag)**
  - **USE COMMAND**: `/test-first with an OutBoxBag of Topic orders each of three overall calls passes A, C, the bag entries, a null request context and a cancellable token, and the sync count is never called`
  - Test location: "tests/Paramore.Brighter.Core.Tests/Observability/StaleMessages/"
  - Test file: `When_the_producers_have_a_bag_should_pass_it_to_the_overall_count.cs`
  - Test should verify:
    - 3 async overall calls, each with `dispatchedSince` 5 min, `maxCount` 100, `args` with exactly the bag's entries, `requestContext` null and a token where `CanBeCanceled` is true;
    - zero sync count calls.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - read the bag at start the way the mediator's registration does (`ServiceCollectionExtensions.cs:565` (ADR: `:568`) and `:847` (ADR: `:850`)), so both pass the same dictionary even when `IAmProducersConfiguration` is not a `ProducersConfiguration`.

- [x] **CHARACTERISE: T8.6 With no bag, the overall call passes an empty dictionary (AC-14, no bag)**
  - **USE COMMAND**: `/test-first with no OutBoxBag configured the overall call passes an empty dictionary as args`
  - Test location: "tests/Paramore.Brighter.Core.Tests/Observability/StaleMessages/"
  - Test file: `When_the_producers_have_no_bag_should_pass_an_empty_dictionary.cs`
  - Test should verify:
    - each overall call's `args` is non-null and empty.
    - 🔁 RED mutation: pass null when no bag is configured.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE once RED is observed — before committing if the test was green on arrival, before implementing if it was not** *(fires in the `review-before` gear, which is the default)*
  - Implementation should (only if the test is RED on arrival):
    - fall back to a new empty dictionary, as `src/Paramore.Brighter/OutboxProducerMediator.cs:200` does.

- [x] **TEST + IMPLEMENT: T8.7 The per-topic call passes every measured topic with its age, C, the bag and a token (AC-14, per topic)**
  - **USE COMMAND**: `/test-first with publications on orders at two minutes and payments with no age the per-topic count is called once per due time with exactly those topics and ages, C, the bag, a null request context and a cancellable token`
  - Test location: "tests/Paramore.Brighter.Core.Tests/Observability/StaleMessages/"
  - Test file: `When_topics_are_measured_should_pass_them_to_the_per_topic_count.cs`
  - Test should verify:
    - `StaleMessageTopicCountingOutbox`, the bag `{"Topic": "orders"}`, and 3 due times;
    - 3 per-topic calls, each with exactly {orders: 2 min, payments: 5 min}, `maxCountPerTopic` 100, `args` with the bag's entries, `requestContext` null and a cancellable token.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - when `MeasuresTopics`, start the per-topic count with its own `Task.Run` after the overall one, both before the first wait;
    - record `CompletePerTopic` and log at Debug naming the count;
    - pass the read-only topic map built at start, with A for a publication without an age.

- [x] **CHARACTERISE: T8.8 One per-topic call per due time (AC-8, per topic)**
  - **USE COMMAND**: `/test-first with publications on orders and payments exactly three per-topic calls are made at 12:01, 12:02 and 12:03`
  - Test location: "tests/Paramore.Brighter.Core.Tests/Observability/StaleMessages/"
  - Test file: `When_time_advances_three_intervals_should_make_one_per_topic_call_per_due_time.cs`
  - Test should verify:
    - in 1 s steps to 12:03:30, exactly 3 per-topic calls at the same instants as the 3 overall calls.
    - 🔁 RED mutation: start the per-topic count only when the overall count returned a value above 0.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE once RED is observed — before committing if the test was green on arrival, before implementing if it was not** *(fires in the `review-before` gear, which is the default)*
  - Implementation should (only if the test is RED on arrival):
    - decide the per-topic due time on its own grid state, independent of the overall result.

- [x] **CHARACTERISE: T8.9 A topic uses the smallest age of its publications (AC-28, counting outbox)**
  - **USE COMMAND**: `/test-first three publications on orders at two minutes, four minutes and none, plus an empty-topic publication, give one per-topic topic orders at two minutes`
  - Test location: "tests/Paramore.Brighter.Core.Tests/Observability/StaleMessages/"
  - Test file: `When_publications_share_a_topic_should_measure_it_at_their_smallest_age.cs`
  - Test should verify:
    - P1, P2 and P3 on "orders" (distinct CloudEvents types) with A_p 2 min, 4 min and none, and P5 on `RoutingKey.Empty` with 1 min;
    - the 12:01:00 per-topic call carries exactly {orders: 2 min}.
    - 🔁 RED mutation: take the largest age per topic; and, applied separately, keep the empty topic.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE once RED is observed — before committing if the test was green on arrival, before implementing if it was not** *(fires in the `review-before` gear, which is the default)*
  - Implementation should (only if the test is RED on arrival):
    - take, per topic, the minimum of each publication's A_p or A (Q-14).

- [x] **CHARACTERISE: T8.10 A topic's smallest age applies against the store (AC-28, `InMemoryOutbox`)**
  - **USE COMMAND**: `/test-first an undispatched orders message added at 12:03:30 gives V_t of 1 for orders and V of 0 at 12:06:00`
  - Test location: "tests/Paramore.Brighter.Core.Tests/Observability/StaleMessages/"
  - Test file: `When_a_topic_age_is_smaller_than_the_stale_age_should_count_the_topic_sooner.cs`
  - Test should verify:
    - the same publications as T8.9 with `InMemoryOutbox`;
    - at 12:06:30, V_t for "orders" is 1 and V is 0 for the 12:06:00 due time.
    - 🔁 RED mutation: pass A in place of A_t on the per-topic call.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE once RED is observed — before committing if the test was green on arrival, before implementing if it was not** *(fires in the `review-before` gear, which is the default)*
  - Implementation should (only if the test is RED on arrival):
    - none is expected beyond T8.9, so stop and ask. A RED here points at the T2.2 implementation.

- [x] **CHARACTERISE: T8.11 A publication changed after start does not change the measured topics (AC-39)**
  - **USE COMMAND**: `/test-first setting the orders publication's topic to payments and its age to four minutes at 12:01:30 leaves the 12:02 and 12:03 per-topic calls on orders at two minutes`
  - Test location: "tests/Paramore.Brighter.Core.Tests/Observability/StaleMessages/"
  - Test file: `When_a_publication_changes_after_start_should_keep_the_topics_read_at_start.cs`
  - Test should verify:
    - the 12:02:00 and 12:03:00 per-topic calls each carry exactly {orders: 2 min}, as the 12:01:00 call did;
    - nothing throws.
    - 🔁 RED mutation: read the publications again at each due time.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE once RED is observed — before committing if the test was green on arrival, before implementing if it was not** *(fires in the `review-before` gear, which is the default)*
  - Implementation should (only if the test is RED on arrival):
    - hold a read-only copy built once in `StartAsync`.

- [x] **CHARACTERISE: T8.12 A publication without an age is measured at A (AC-32, null clause)**
  - **USE COMMAND**: `/test-first a null StaleMessageAge on orders makes the per-topic call carry orders at five minutes`
  - Test location: "tests/Paramore.Brighter.Core.Tests/Observability/StaleMessages/"
  - Test file: `When_a_publication_has_no_stale_age_should_measure_its_topic_at_the_stale_age.cs`
  - Test should verify:
    - the 12:01:00 per-topic call carries {orders: 5 min}.
    - 🔁 RED mutation: use I in place of A for a null age.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE once RED is observed — before committing if the test was green on arrival, before implementing if it was not** *(fires in the `review-before` gear, which is the default)*
  - Implementation should (only if the test is RED on arrival):
    - fall back to the options' A.

- [x] **CHARACTERISE: T8.13 Without the capability or any measured topic, only the overall count runs (AC-29, loop)**
  - **USE COMMAND**: `/test-first without the capability or with no measured topic the overall gauge reports each due time, the per-topic gauge and its signal have no series, the capability is never called and nothing more is logged`
  - Test location: "tests/Paramore.Brighter.Core.Tests/Observability/StaleMessages/" (in `StaleMessageLoggingCollection`)
  - Test file: `When_the_per_topic_count_is_left_out_should_run_only_the_overall_count.cs`
  - Test should verify:
    - with `StaleMessageCountingOutbox` and "orders" and "payments", to 12:05:30, the overall gauge records a value for each due time from 12:01 to 12:05;
    - at every collection, neither the topic gauge nor any `per_topic` counter series exists;
    - the start entry is the only one logged about it;
    - with `StaleMessageTopicCountingOutbox` and each of the three no-topic setups of T6.9, the capability is never called.
    - 🔁 RED mutation: start the per-topic count whenever the outbox has the capability.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE once RED is observed — before committing if the test was green on arrival, before implementing if it was not** *(fires in the `review-before` gear, which is the default)*
  - Implementation should (only if the test is RED on arrival):
    - gate the per-topic `Task.Run` on `MeasuresTopics`, decided once at start.

- [x] **CHARACTERISE: T8.14 Each due time makes one bounded call per count and nothing else (AC-23)**
  - **USE COMMAND**: `/test-first with C of 25 five due times make five overall calls with maxCount 25 and five per-topic calls with both topics at 25 each, and no other outbox call`
  - Test location: "tests/Paramore.Brighter.Core.Tests/Observability/StaleMessages/"
  - Test file: `When_five_due_times_complete_should_make_one_bounded_call_per_count_each.cs`
  - Test should verify:
    - 5 overall calls with `maxCount` 25 and 5 per-topic calls with {orders, payments} and a ceiling of 25;
    - `OutstandingMessagesAsync` and every other member of the double are never called;
    - with `StaleMessageCountingOutbox`, exactly 5 calls, all overall.
    - 🔁 RED mutation: also call `OutstandingMessagesAsync` once per due time.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE once RED is observed — before committing if the test was green on arrival, before implementing if it was not** *(fires in the `review-before` gear, which is the default)*
  - Implementation should (only if the test is RED on arrival):
    - make no outbox call other than the two counts.

- [x] **CHARACTERISE: T8.15 Both gauges report the worked example (AC-27)**
  - **USE COMMAND**: `/test-first with orders at two minutes and payments at A the 12:06 measurements record V of 2 and V_t of 2 for orders and 1 for payments, the 12:01 ones record zeros, and neither gauge has a value at 12:00:30`
  - Test location: "tests/Paramore.Brighter.Core.Tests/Observability/StaleMessages/"
  - Test file: `When_topics_hold_stale_messages_at_their_own_ages_should_report_both_gauges.cs`
  - Test should verify:
    - `InMemoryOutbox` with o1, o2, o3, p1 and p2 as AC-27 lists them;
    - at 12:06:30, V is 2, V_t is orders 2 and payments 1;
    - the 12:01:00 values are 0 overall and 0 for each topic, as values;
    - at 12:00:30 neither gauge has a value.
    - 🔁 RED mutation: record V as the sum of the V_t values.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE once RED is observed — before committing if the test was green on arrival, before implementing if it was not** *(fires in the `review-before` gear, which is the default)*
  - Implementation should (only if the test is RED on arrival):
    - keep the two counts' results separate in the readings.

- [x] **CHARACTERISE: T8.16 A topic with no publication is in the overall count only (AC-30)**
  - **USE COMMAND**: `/test-first a stale audit message with no audit publication counts in the overall gauge only and has no topic series`
  - Test location: "tests/Paramore.Brighter.Core.Tests/Observability/StaleMessages/"
  - Test file: `When_a_topic_has_no_publication_should_count_it_in_the_overall_gauge_only.cs`
  - Test should verify:
    - a publication on "orders" only, with a1 on "audit" and o1 on "orders" added at 12:00:00;
    - at 12:06:30, V is 2, and the topic gauge has exactly one series, "orders", with 1;
    - no series carries "audit".
    - 🔁 RED mutation: record V as the sum of the V_t values.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE once RED is observed — before committing if the test was green on arrival, before implementing if it was not** *(fires in the `review-before` gear, which is the default)*
  - Implementation should (only if the test is RED on arrival):
    - measure topics only from the publications.

- [x] **CHARACTERISE: T8.17 The ceiling applies to each topic, not across topics (AC-31)**
  - **USE COMMAND**: `/test-first with C of three, 50 orders, 2 payments and no refunds the topic values are 3, 2 and 0 and the overall value is 3`
  - Test location: "tests/Paramore.Brighter.Core.Tests/Observability/StaleMessages/"
  - Test file: `When_a_topic_exceeds_the_ceiling_should_cap_each_topic_on_its_own.cs`
  - Test should verify:
    - at 12:06:30, V_t is orders 3, payments 2 and refunds 0 (as a value), and V is 3.
    - 🔁 RED mutation: divide C by the number of topics on the per-topic call.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE once RED is observed — before committing if the test was green on arrival, before implementing if it was not** *(fires in the `review-before` gear, which is the default)*
  - Implementation should (only if the test is RED on arrival):
    - pass C itself as `maxCountPerTopic`.

- [ ] **CHARACTERISE: T8.18 A message counts toward its header topic (AC-38)**
  - **USE COMMAND**: `/test-first a message with header topic orders.v2 and ProducerTopic orders is not counted for orders, and is counted for orders.v2 once it has a publication`
  - Test location: "tests/Paramore.Brighter.Core.Tests/Observability/StaleMessages/"
  - Test file: `When_a_message_header_topic_differs_from_its_publication_should_count_it_toward_the_header_topic.cs`
  - Test should verify:
    - with "orders" and "payments" publications, and m1 (header "orders.v2", bag `ProducerTopic` "orders") and o1 ("orders"): V 2, orders 1, payments 0, and no "orders.v2" series;
    - with a third publication on "orders.v2": orders.v2 1 and orders still 1.
    - 🔁 RED mutation: in `InMemoryOutbox`, count by the `ProducerTopic` bag entry.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE once RED is observed — before committing if the test was green on arrival, before implementing if it was not** *(fires in the `review-before` gear, which is the default)*
  - Implementation should (only if the test is RED on arrival):
    - none is expected beyond T2.5, so stop and ask.

- [ ] **CHARACTERISE: T8.19 A tripped topic is still counted (AC-40)**
  - **USE COMMAND**: `/test-first with orders tripped on a registered outbox circuit breaker both topics are still counted and requested`
  - Test location: "tests/Paramore.Brighter.Core.Tests/Observability/StaleMessages/"
  - Test file: `When_a_topic_is_tripped_should_still_count_it.cs`
  - Test should verify:
    - an `IAmAnOutboxCircuitBreaker` with "orders" tripped, and `InMemoryOutbox` with 2 on "orders" and 1 on "payments";
    - at 12:06:30, orders 2, payments 1 and V 3;
    - with `StaleMessageTopicCountingOutbox`, the 12:01:00 per-topic call carries both topics.
    - 🔁 RED mutation: resolve the breaker and leave tripped topics out of the per-topic call.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE once RED is observed — before committing if the test was green on arrival, before implementing if it was not** *(fires in the `review-before` gear, which is the default)*
  - Implementation should (only if the test is RED on arrival):
    - never read the circuit breaker.

- [ ] **CHARACTERISE: T8.20 The gauges carry fixed, low-cardinality attributes (AC-19)**
  - **USE COMMAND**: `/test-first after ten measurements of each count the overall gauge has one series without a topic, the topic gauge has three series, and no attribute carries a message id, correlation id or topic on the signal`
  - Test location: "tests/Paramore.Brighter.Core.Tests/Observability/StaleMessages/"
  - Test file: `When_measurements_complete_should_tag_the_gauges_with_fixed_attributes.cs`
  - Test should verify:
    - publications on "orders", "payments" and "refunds", with 10 messages over them carrying distinct ids and correlation ids;
    - after 10 completed due times: the overall gauge has one series with the same set {implementation} each time, no `messaging.destination.name`, and no value equal to an id or a topic;
    - the topic gauge has exactly 3 series, each {implementation, `messaging.destination.name`} and nothing else;
    - the counter series carry no topic, id or correlation id.
    - 🔁 RED mutation: tag the overall gauge with the first measured topic.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE once RED is observed — before committing if the test was green on arrival, before implementing if it was not** *(fires in the `review-before` gear, which is the default)*
  - Implementation should (only if the test is RED on arrival):
    - build each tag list from the readings' outbox type and, for the topic gauge, the topic only, in `src/Paramore.Brighter/Observability/OutboxStaleMessageMeter.cs`.

- [ ] **TEST + IMPLEMENT: T8.21 A count above C is reported as C (AC-33 last clause; ADR 0085 risk :964)**
  - **USE COMMAND**: `/test-first an overall count of 150 and a per-topic count of 150 for orders are recorded as 100 with C of 100`
  - Test location: "tests/Paramore.Brighter.Core.Tests/Observability/StaleMessages/"
  - Test file: `When_an_outbox_returns_more_than_the_ceiling_should_record_the_ceiling.cs`
  - Test should verify:
    - the double returns 150 overall and {orders: 150, payments: 0};
    - V is 100 and V_t is orders 100 and payments 0.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - record `CompleteOverall(min(count, C))` and cap each requested topic at C (row 6a).

- [ ] **TEST + IMPLEMENT: T8.22 Unrequested topics in a per-topic result are dropped (AC-33)**
  - **USE COMMAND**: `/test-first a per-topic result with an extra audit entry completes, reports orders and payments, and has no audit series`
  - Test location: "tests/Paramore.Brighter.Core.Tests/Observability/StaleMessages/"
  - Test file: `When_the_per_topic_result_has_an_unrequested_topic_should_drop_it.cs`
  - Test should verify:
    - the result {orders: 2, payments: 0, audit: 5} gives a `per_topic` `completed` total of 1 and series orders 2 and payments 0;
    - no series carries "audit".
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - project the result onto the requested topics before `CompletePerTopic` (row 6a).

- [ ] **CHARACTERISE: T8.23 The app starts at the boundary settings, and a maximum-settings measurement completes (AC-3 second and third clauses)**
  - **USE COMMAND**: `/test-first the host starts with A of 1 ms, C of 1, I of 1 minute and T of 59 seconds, and with A and C at their maximums a measurement against InMemoryOutbox completes`
  - Test location: "tests/Paramore.Brighter.Core.Tests/Observability/StaleMessages/" (in `StaleMessageLoggingCollection`)
  - Test file: `When_settings_are_at_their_bounds_should_start_and_measure.cs`
  - Test should verify:
    - a host with `AddProducers` (`InMemoryOutbox`) and the minimum settings starts;
    - with A 365 d and C 1000, at 12:01:30 the `overall` `completed` total is 1 and no exception was logged.
    - 🔁 RED mutation: in the monitor, pass `TimeSpan.MaxValue` as the overall count's age instead of A. `now - TimeSpan.MaxValue` throws `ArgumentOutOfRangeException` at `src/Paramore.Brighter/InMemoryOutbox.cs:630`, so the call fails, the `completed` total stays 0 and the failure is logged.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE once RED is observed — before committing if the test was green on arrival, before implementing if it was not** *(fires in the `review-before` gear, which is the default)*
  - Implementation should (only if the test is RED on arrival):
    - pass A unchanged to the outbox. The 365-day bound keeps `now - A` in range.

- [ ] **CHARACTERISE: T8.24 The loop does not carry the starter's ambient state into a count**
  - **USE COMMAND**: `/test-first an AsyncLocal value set before StartAsync is not visible inside the count call`
  - Test location: "tests/Paramore.Brighter.Core.Tests/Observability/StaleMessages/"
  - Test file: `When_the_monitor_is_started_from_a_flow_with_ambient_state_should_not_carry_it_into_counts.cs`
  - Test should verify:
    - the double reads an `AsyncLocal<string>` set by the test before `StartAsync`, and sees null at the 12:01:00 call. The pattern is `tests/Paramore.Brighter.Core.Tests/Sweeping/When_the_sweeper_is_started_from_a_flow_with_ambient_state_should_not_carry_it_into_sweeps.cs`.
    - 🔁 RED mutation: remove `ExecutionContext.SuppressFlow` around the loop's `Task.Run`.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE once RED is observed — before committing if the test was green on arrival, before implementing if it was not** *(fires in the `review-before` gear, which is the default)*
  - Implementation should (only if the test is RED on arrival):
    - restore the flow right after `Task.Run`, as `TimedOutboxSweeper.cs:103-105` does.

- [ ] **PROJECT: T8.25 Review the grid computation against the coalescing rules (AC-8 coalescing clause, rows 2 and 3)**
  - Review the wake handling in `src/Paramore.Brighter.Outbox.Hosting/TimedOutboxStaleMessageMonitor.cs` against ADR 0085 *Why one loop that computes the grid?* and rows 2 and 3 (:254-255). Check that:
    - the next grid point is computed from the start timestamp in whole ticks, not from a periodic timer's catch-up;
    - N missed due times give one measurement and `Skip(count, N - 1)` with one Warning;
    - an early wake waits again.
  - Output: a "Reviewed:" sub-bullet under this task, with the file and lines checked.

## Phase 9: Timeout, single flight and failure, for each count (ADR 0085 step 9)

*Depends on Phase 8.*

- [ ] **TEST + IMPLEMENT: T9.1 A count that does not answer is abandoned at T, and logged as an abandonment (AC-10 first clause)**
  - **USE COMMAND**: `/test-first a count that honours cancellation and never answers ends with OperationCanceledException at T, records no value and is logged as abandoned, not failed`
  - Test location: "tests/Paramore.Brighter.Core.Tests/Observability/StaleMessages/" (in `StaleMessageLoggingCollection`)
  - Test file: `When_a_count_does_not_answer_within_the_timeout_should_abandon_it.cs`
  - Test should verify:
    - the 12:01:00 call's token has `IsCancellationRequested` true once fake time reaches 12:01:10, and the call ends with `OperationCanceledException`;
    - no V is recorded for 12:01, and the `overall` `abandoned` total is 1;
    - one Warning names the outbox type, the count and T, and no entry is logged as a failure.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should, in `src/Paramore.Brighter.Outbox.Hosting/TimedOutboxStaleMessageMonitor.cs`:
    - take one start timestamp immediately before a due time's `Task.Run` calls, with deadline = start + T;
    - await `Task.WhenAny` of each undecided call, a delay to the deadline from the timer helper, and the stop task;
    - at the deadline, record `Abandon(count)` and the Warning, then cancel off the loop thread: `CancelAsync` on net8.0+, `Cancel` queued to the pool on netstandard2.0. Observe that work's exception;
    - ignore a later result or exception (ADR risk :959).

- [ ] **CHARACTERISE: T9.2 The next due time after an abandonment records its value (AC-10 second clause)**
  - **USE COMMAND**: `/test-first after the 12:01 call is abandoned the 12:02 call that returns 4 records V of 4`
  - Test location: "tests/Paramore.Brighter.Core.Tests/Observability/StaleMessages/"
  - Test file: `When_a_count_returns_after_an_abandoned_one_should_record_its_value.cs`
  - Test should verify:
    - the 12:01:00 call blocks until its token is cancelled. After 12:01:10 the test waits for the double's signal that the call has ended (determinism rule 3, under "How to use this list" in `specs/0038-outbox-stale-message-telemetry/tasks.md`) before it advances to 12:02:00;
    - at 12:02:30, V is 4 and the `overall` `completed` total is 1.
    - 🔁 RED mutation: after a count abandons a call, skip that count's next due time.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE once RED is observed — before committing if the test was green on arrival, before implementing if it was not** *(fires in the `review-before` gear, which is the default)*
  - Implementation should (only if the test is RED on arrival):
    - clear a count's in-progress task once it has ended, and dispose its source at that wake.

- [ ] **CHARACTERISE: T9.3 A result that arrives after T is never recorded (AC-10 third clause)**
  - **USE COMMAND**: `/test-first a count that ignores cancellation and returns 7 at 12:01:30 never records 7`
  - Test location: "tests/Paramore.Brighter.Core.Tests/Observability/StaleMessages/"
  - Test file: `When_a_count_returns_after_the_timeout_should_never_record_it.cs`
  - Test should verify:
    - the test releases the call at 12:01:30 and waits for the double's signal that it has ended (determinism rule 3, under "How to use this list" in `specs/0038-outbox-stale-message-telemetry/tasks.md`) before it advances further;
    - from 12:01:30 onward, no collection receives 7, and the `completed` total stays 0 until a later due time completes.
    - 🔁 RED mutation: record a late result when its task ends.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE once RED is observed — before committing if the test was green on arrival, before implementing if it was not** *(fires in the `review-before` gear, which is the default)*
  - Implementation should (only if the test is RED on arrival):
    - mark the call decided at abandonment, and attach only an exception-observing continuation.

- [ ] **TEST + IMPLEMENT: T9.4 A count that ends just after the deadline is abandoned, never completed (ADR 0085 risk :965)**
  - **USE COMMAND**: `/test-first a count released just after fake time reaches the deadline is recorded as abandoned and never as completed`
  - Test location: "tests/Paramore.Brighter.Core.Tests/Observability/StaleMessages/"
  - Test file: `When_a_count_ends_at_the_deadline_should_record_it_as_abandoned.cs`
  - Test should verify:
    - after advancing to 12:01:10 exactly and then releasing the double, the `abandoned` total is 1, `completed` is 0, and no V is recorded for 12:01.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - make each count's task record its end timestamp from the `TimeProvider` as its last step, and let a continuation on the task only observe its exception (ADR 0085 :245, :875). Record the end in a `finally`, so that it holds whether the call returns or throws, and await the outbox's task directly, with no forced yield before the `finally`, so that the double's end signal follows the recorded end;
    - decide by that timestamp: before the deadline, rows 6a and 6b; at or after it, row 6c.

- [ ] **CHARACTERISE: T9.5 A count that ends before the deadline completes even when the loop reads it late (ADR 0085 risk :965)**
  - **USE COMMAND**: `/test-first a count that returns before the deadline is recorded as completed even when a blocking logger holds the loop past the deadline`
  - Test location: "tests/Paramore.Brighter.Core.Tests/Observability/StaleMessages/" (in `StaleMessageLoggingCollection`)
  - Test file: `When_the_loop_reads_an_ended_count_after_the_deadline_should_decide_by_its_recorded_end.cs`
  - Test should verify:
    - a `CapturingLogger` that blocks on the overall Debug entry until released holds the loop;
    - the test releases the per-topic call at 12:01:05 with a valid result. The test waits for the double's signal that the call has ended (determinism rule 3, under "How to use this list" in `specs/0038-outbox-stale-message-telemetry/tasks.md`), then moves fake time to 12:01:20, and then releases the logger. While the logger holds the loop, the test waits only for that call's end, and it waits for the outcomes after the release;
    - the `per_topic` `completed` total is 1 and its values are recorded.
    - 🔁 RED mutation: decide by the clock when the loop reads the task.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE once RED is observed — before committing if the test was green on arrival, before implementing if it was not** *(fires in the `review-before` gear, which is the default)*
  - Implementation should (only if the test is RED on arrival):
    - write each log entry as soon as its outcome is recorded, and decide each call only by its recorded end.

- [ ] **TEST + IMPLEMENT: T9.6 At most one call of a count is in flight; due times during it are skipped and logged (AC-9)**
  - **USE COMMAND**: `/test-first a count that ignores cancellation until 12:03:30 is cancelled at 12:01:10, the 12:02 and 12:03 due times are skipped and logged, and the 12:04 call starts`
  - Test location: "tests/Paramore.Brighter.Core.Tests/Observability/StaleMessages/" (in `StaleMessageLoggingCollection`)
  - Test file: `When_a_count_is_still_running_at_the_next_due_time_should_skip_it.cs`
  - Test should verify:
    - the 12:01:00 token is cancelled at 12:01:10;
    - the test releases the call at 12:03:30 and waits for the double's signal that it has ended (determinism rule 3, under "How to use this list" in `specs/0038-outbox-stale-message-telemetry/tasks.md`) before it advances to 12:04:00;
    - no call starts at 12:02 or 12:03, and one Warning naming the outbox type and the count is logged for each;
    - a call starts at 12:04:00, and no V is recorded for 12:01;
    - at the 12:02:20 and 12:03:20 collections, `completed` is 0 while `abandoned` and `skipped` are above 0, which differs from a run with the feature disabled (no series at all).
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - for each count at each wake, skip every due time while its previous call still runs, and every due time at or before that call's recorded end (row 4): `Skip(count, n)` with one Warning;
    - let only the loop cancel or dispose a count's source, and dispose it only after the task has ended.

- [ ] **CHARACTERISE: T9.7 A count that blocks its thread holds neither `Advance` nor the deadline (AC-11 first half)**
  - **USE COMMAND**: `/test-first an overall count that blocks the calling thread before returning a task lets every Advance return, is cancelled at 12:01:10, records no value and makes no call at 12:02`
  - Test location: "tests/Paramore.Brighter.Core.Tests/Observability/StaleMessages/"
  - Test file: `When_a_count_blocks_its_thread_should_still_abandon_it_at_the_timeout.cs`
  - Test should verify:
    - in 1 s steps from 12:00:59 to 12:02:30, each `Advance` returns within 5 s of wall-clock time;
    - the token is cancelled at 12:01:10, no V is recorded for 12:01, and the 12:02:00 due time makes no call.
    - 🔁 RED mutation: call the overall count on the loop thread in place of `Task.Run`.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE once RED is observed — before committing if the test was green on arrival, before implementing if it was not** *(fires in the `review-before` gear, which is the default)*
  - Implementation should (only if the test is RED on arrival):
    - start every count with `Task.Run`, never inline.

- [ ] **CHARACTERISE: T9.8 An overall count that blocks its thread does not delay the per-topic count (AC-36; ADR 0085 risk :952)**
  - **USE COMMAND**: `/test-first an overall count that blocks its thread leaves the per-topic count to start at 12:01 and record 3 and 0 before 12:01:10, and the overall call is abandoned`
  - Test location: "tests/Paramore.Brighter.Core.Tests/Observability/StaleMessages/"
  - Test file: `When_the_overall_count_blocks_its_thread_should_still_record_the_per_topic_count.cs`
  - Test should verify:
    - each `Advance` returns within 5 s;
    - the per-topic call started at 12:01:00, and orders 3 and payments 0 are recorded before fake time passes 12:01:10;
    - the overall token is cancelled at 12:01:10, with no overall V for 12:01.
    - 🔁 RED mutation: start the per-topic `Task.Run` only after the overall call has returned its task.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE once RED is observed — before committing if the test was green on arrival, before implementing if it was not** *(fires in the `review-before` gear, which is the default)*
  - Implementation should (only if the test is RED on arrival):
    - queue both `Task.Run` calls before any wait.

- [ ] **CHARACTERISE: T9.9 A per-topic count that blocks its thread does not delay the overall count (AC-37; ADR 0085 risk :951)**
  - **USE COMMAND**: `/test-first a per-topic count that blocks its thread leaves the overall count to record 1 before 12:01:10, and the per-topic call is abandoned`
  - Test location: "tests/Paramore.Brighter.Core.Tests/Observability/StaleMessages/"
  - Test file: `When_the_per_topic_count_blocks_its_thread_should_still_record_the_overall_count.cs`
  - Test should verify:
    - each `Advance` returns within 5 s;
    - the overall call started at 12:01:00, and V 1 is recorded before 12:01:10;
    - the per-topic token is cancelled at 12:01:10, with no V_t for 12:01.
    - 🔁 RED mutation: decide the calls only once both have ended or the deadline has passed.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE once RED is observed — before committing if the test was green on arrival, before implementing if it was not** *(fires in the `review-before` gear, which is the default)*
  - Implementation should (only if the test is RED on arrival):
    - decide each call as soon as its own task ends.

- [ ] **TEST + IMPLEMENT: T9.10 A count that throws is logged with the exception, and the next due time records (AC-12)**
  - **USE COMMAND**: `/test-first a count that throws connection refused at 12:01 is logged with the exception and outbox type and the 12:02 call records 2`
  - Test location: "tests/Paramore.Brighter.Core.Tests/Observability/StaleMessages/" (in `StaleMessageLoggingCollection`)
  - Test file: `When_a_count_throws_should_log_it_and_measure_the_next_due_time.cs`
  - Test should verify:
    - one Warning carries the `InvalidOperationException("connection refused")`, the outbox type and the count;
    - no V is recorded for 12:01, V is 2 for 12:02, and the host is still running;
    - the `completed` total at the 12:02:20 collection is greater than at the 12:01:20 collection.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - record `Fail(count)` and a Warning with the exception for a task that faulted before the deadline (row 6b).

- [ ] **CHARACTERISE: T9.11 Repeated failures are distinguishable from zero in metrics alone (AC-7, throw variant)**
  - **USE COMMAND**: `/test-first a run that fails after one zero differs from a run that always returns zero at every collection from 12:02:10 to 12:03:30`
  - Test location: "tests/Paramore.Brighter.Core.Tests/Observability/StaleMessages/"
  - Test file: `When_counts_keep_failing_should_differ_from_counts_of_zero.cs`
  - Test should verify:
    - run 1 returns 0 at 12:01 and then throws, and run 2 always returns 0;
    - at each 10 s collection from 12:02:10 to 12:03:30, the two runs' `overall` `completed` totals differ, and, asserted on its own, their `overall` `failed` totals differ;
    - neither run's overall gauge reports anything but 0.
    - 🔁 RED mutation: make `Fail` increment the `completed` total instead of the `failed` total. Both totals then match between the runs.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE once RED is observed — before committing if the test was green on arrival, before implementing if it was not** *(fires in the `review-before` gear, which is the default)*
  - Implementation should (only if the test is RED on arrival):
    - none is expected beyond T9.10 and Phase 3, so stop and ask.

- [ ] **CHARACTERISE: T9.12 Repeated abandonments are distinguishable from zero in metrics alone (AC-7, abandon variant)**
  - **USE COMMAND**: `/test-first a run whose count blocks and honours cancellation from 12:02 differs from an all-zero run at collections 12:02:15, 12:02:25 and on`
  - Test location: "tests/Paramore.Brighter.Core.Tests/Observability/StaleMessages/"
  - Test file: `When_counts_keep_being_abandoned_should_differ_from_counts_of_zero.cs`
  - Test should verify:
    - at collections 12:02:15, 12:02:25 and so on to 12:03:25, the two runs' `overall` `completed` totals differ, and, asserted on its own, their `overall` `abandoned` totals differ;
    - neither gauge reports anything but 0.
    - 🔁 RED mutation: make `Abandon` increment the `completed` total instead of the `abandoned` total. At the 12:02:15 collection both totals then match between the runs.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE once RED is observed — before committing if the test was green on arrival, before implementing if it was not** *(fires in the `review-before` gear, which is the default)*
  - Implementation should (only if the test is RED on arrival):
    - none is expected beyond T9.1, so stop and ask.

- [ ] **CHARACTERISE: T9.13 A blocked per-topic count leaves the overall count unaffected (AC-33 first clause; ADR 0086 risk :414)**
  - **USE COMMAND**: `/test-first a per-topic call blocked from 12:01 to 12:03:30 is abandoned and skipped while the overall gauge records 1 at each due time`
  - Test location: "tests/Paramore.Brighter.Core.Tests/Observability/StaleMessages/"
  - Test file: `When_the_per_topic_count_is_blocked_should_keep_measuring_the_overall_count.cs`
  - Test should verify:
    - V is 1 for each due time from 12:01 to 12:04;
    - the per-topic token is cancelled at 12:01:10, with no V_t for 12:01;
    - the test releases the per-topic call at 12:03:30 and waits for the double's signal that it has ended (determinism rule 3, under "How to use this list" in `specs/0038-outbox-stale-message-telemetry/tasks.md`) before it advances to 12:04:00;
    - no per-topic call at 12:02 or 12:03, and one at 12:04:00;
    - at 12:02:20 and 12:03:20, `per_topic` `completed` is 0 while `overall` `completed` keeps rising.
    - 🔁 RED mutation: use one in-progress slot for both counts.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE once RED is observed — before committing if the test was green on arrival, before implementing if it was not** *(fires in the `review-before` gear, which is the default)*
  - Implementation should (only if the test is RED on arrival):
    - keep the in-progress task, end timestamp and token source per count.

- [ ] **CHARACTERISE: T9.14 A failing overall count leaves the per-topic count unaffected (AC-33)**
  - **USE COMMAND**: `/test-first an overall count that always throws leaves the per-topic gauge reporting 2 and 0, and each failure entry names the overall count`
  - Test location: "tests/Paramore.Brighter.Core.Tests/Observability/StaleMessages/" (in `StaleMessageLoggingCollection`)
  - Test file: `When_the_overall_count_fails_should_keep_reporting_the_per_topic_count.cs`
  - Test should verify:
    - the topic gauge reports orders 2 and payments 0;
    - `overall` `completed` stays 0 while `per_topic` `completed` rises;
    - every failure Warning names the overall count.
    - 🔁 RED mutation: record `Fail` for both counts when either throws.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE once RED is observed — before committing if the test was green on arrival, before implementing if it was not** *(fires in the `review-before` gear, which is the default)*
  - Implementation should (only if the test is RED on arrival):
    - decide and log each count on its own.

- [ ] **TEST + IMPLEMENT: T9.15 An invalid per-topic result fails that measurement only (AC-33)**
  - **USE COMMAND**: `/test-first a per-topic result that is null, lacks payments or holds minus one for payments fails, records no topic value, and the overall value is still recorded`
  - Test location: "tests/Paramore.Brighter.Core.Tests/Observability/StaleMessages/"
  - Test file: `When_the_per_topic_result_is_invalid_should_fail_the_per_topic_measurement.cs`
  - Test should verify, as a theory over null, {orders: 2} and {orders: 2, payments: -1}:
    - `per_topic` `failed` is 1;
    - the topic gauge has no series for that due time;
    - the overall V of that due time is recorded.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - treat a null result, a missing requested topic or a negative requested value as invalid (row 6b), with a Warning naming the count.

- [ ] **TEST + IMPLEMENT: T9.16 A negative overall count fails the measurement**
  - **USE COMMAND**: `/test-first an overall count that returns minus one is recorded as failed with no value`
  - Test location: "tests/Paramore.Brighter.Core.Tests/Observability/StaleMessages/"
  - Test file: `When_the_overall_count_is_negative_should_fail_the_measurement.cs`
  - Test should verify:
    - `overall` `failed` is 1, the overall gauge has no series, and nothing throws to the host.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - check the overall result before capping (row 6b, ADR 0085 :272).

- [ ] **CHARACTERISE: T9.17 The outbox's own cancellation before the deadline is a failure (ADR 0085 risk :963)**
  - **USE COMMAND**: `/test-first a count that throws OperationCanceledException of its own before the deadline is recorded as failed and logged with the exception`
  - Test location: "tests/Paramore.Brighter.Core.Tests/Observability/StaleMessages/" (in `StaleMessageLoggingCollection`)
  - Test file: `When_the_outbox_cancels_its_own_count_should_record_a_failure.cs`
  - Test should verify:
    - the double throws `OperationCanceledException` at 12:01:02, as an outbox's own timeout does (for example DynamoDB's 500 ms);
    - `failed` is 1, `abandoned` is 0, and the Warning carries the exception.
    - 🔁 RED mutation: record any `OperationCanceledException` as `abandoned`.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE once RED is observed — before committing if the test was green on arrival, before implementing if it was not** *(fires in the `review-before` gear, which is the default)*
  - Implementation should (only if the test is RED on arrival):
    - treat every exception of a call ended before the deadline as row 6b, because the loop has not cancelled that token (ADR 0085 :277).

- [ ] **TEST + IMPLEMENT: T9.18 An exception in the loop's own code is logged at Error and the grid continues (ADR 0085 risk :956)**
  - **USE COMMAND**: `/test-first a logger that throws once is logged at Error and the next due time is still measured`
  - Test location: "tests/Paramore.Brighter.Core.Tests/Observability/StaleMessages/" (in `StaleMessageLoggingCollection`)
  - Test file: `When_the_loop_code_throws_should_log_it_and_keep_the_schedule.cs`
  - Test should verify:
    - a `CapturingLogger` throws on the first Debug entry;
    - an Error entry is captured, and the 12:02:00 due time makes a call and records a value.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - wrap each wake's handling in a catch that logs at Error and continues on the grid.

- [ ] **CHARACTERISE: T9.19 A blocking cancellation callback does not hold the loop (ADR 0085 risk :958)**
  - **USE COMMAND**: `/test-first a count whose cancellation callback blocks is recorded as abandoned and the next due time is handled while the callback still blocks`
  - Test location: "tests/Paramore.Brighter.Core.Tests/Observability/StaleMessages/"
  - Test file: `When_a_cancellation_callback_blocks_should_not_hold_the_loop.cs`
  - Test should verify:
    - `abandoned` is 1 at 12:01:10;
    - the 12:02:00 due time is handled (a skip outcome while the first call runs, or a call once it has ended) while the callback is still blocked.
    - 🔁 RED mutation: call `Cancel()` inline on the loop thread.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE once RED is observed — before committing if the test was green on arrival, before implementing if it was not** *(fires in the `review-before` gear, which is the default)*
  - Implementation should (only if the test is RED on arrival):
    - keep cancellation off the loop thread, as T9.1 does.

- [ ] **CHARACTERISE: T9.20 An abandoned call that faults later raises no unobserved task exception (ADR 0085 risk :966)**
  - **USE COMMAND**: `/test-first an abandoned count that faults after the deadline raises no UnobservedTaskException`
  - Test location: "tests/Paramore.Brighter.Core.Tests/Observability/StaleMessages/" (in `StaleMessageLoggingCollection`, which is non-parallel, as GC-driven tests need and as `MessageSerialisation/FinalizerTestCollection.cs:35` is)
  - Test file: `When_an_abandoned_count_faults_later_should_observe_the_exception.cs`
  - Test should verify:
    - the 12:01:00 call ignores cancellation, so the abandonment at 12:01:10 does not end it. At 12:01:30 the test releases it with an `InvalidOperationException`, not an `OperationCanceledException`, which would end the count's task as cancelled rather than faulted. The test waits for the double's signal that the call has ended (determinism rule 3 and "Test doubles", under "How to use this list" in `specs/0038-outbox-stale-message-telemetry/tasks.md`). It then advances to 12:02:00 and waits for that due time's call, which returns 0, and its outcome, because the loop lets go of an ended call only at its next wake (ADR 0085 :870, :875, :877, and T9.2). Then it runs `GC.Collect()`, `GC.WaitForPendingFinalizers()` and `GC.Collect()`, as `tests/Paramore.Brighter.Core.Tests/MessageSerialisation/When_a_pipeline_finalizer_release_throws_it_should_not_escape.cs:53-58` does, and `TaskScheduler.UnobservedTaskException` has not fired for that exception;
    - nothing is logged as a failure.
    - 🔁 RED mutation: remove the exception-observing continuation on each count's task.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE once RED is observed — before committing if the test was green on arrival, before implementing if it was not** *(fires in the `review-before` gear, which is the default)*
  - Implementation should (only if the test is RED on arrival):
    - attach `ContinueWith(t => _ = t.Exception, TaskContinuationOptions.OnlyOnFaulted)` to each count task.

- [ ] **PROJECT: T9.21 Review the row 4 split, token-source disposal and queued-cancel exceptions (ADR 0085 risk :957)**
  - In `src/Paramore.Brighter.Outbox.Hosting/TimedOutboxStaleMessageMonitor.cs`, check that:
    - the "at or before the previous call's end" skip of row 4, which needs a late loop and so has no fake-clock test, matches ADR 0085 :249 and :256 and FR-8;
    - each count's task records its end in a `finally` on every exit path and awaits the outbox's task with no forced yield before that `finally`, and no continuation records the end;
    - only the loop cancels or disposes a source, and only after the count's task has ended;
    - a queued `Cancel` that runs after disposal throws `ObjectDisposedException`, and the loop observes it;
    - the loop's netstandard2.0 branches (`Task.Yield` after each `Task.WhenAny`, queued `Cancel`) match their net8.0+ counterparts (ADR 0085 :863, :874).
  - Output: a "Reviewed:" sub-bullet under this task, with the file and lines checked.

## Phase 10: Stopping (ADR 0085 step 10)

*Depends on Phase 9.*

- [ ] **TEST + IMPLEMENT: T10.1 A stop cancels the in-progress token at once and returns within T (AC-15 first clause)**
  - **USE COMMAND**: `/test-first a stop at 12:01:05 with a count that ignores cancellation cancels its token at 12:01:05, returns by 12:01:15, starts no later call and logs no failure`
  - Test location: "tests/Paramore.Brighter.Core.Tests/Observability/StaleMessages/" (in `StaleMessageLoggingCollection`)
  - Test file: `When_stopping_with_a_count_in_progress_should_cancel_it_and_return_within_the_timeout.cs`
  - Test should verify:
    - after `StopAsync` is called at 12:01:05, and before fake time moves, a bounded wait, as in determinism rule 1, ends with the token's `IsCancellationRequested` true (determinism rule 2). The loop cancels that token only after it observes the stop, at least one asynchronous hop after the call (ADR 0085 :293, :301), so the test does not read it right after the call;
    - the `StopAsync` task has completed once fake time reaches 12:01:15;
    - no call starts in the 10 min of fake time after that;
    - no failure entry, and one Information entry for the stop.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should, following ADR 0085 *Stopping* (:283-308), and leaving to T10.8 the first thing `StopAsync` does there (:289, return at once if the loop never started):
    - `StopAsync`: cancel the stop token, then wait for the first of: the loop ends, T on the `TimeProvider`, or the host's token. Create the wait for T before the first `await` in `StopAsync`, so that T counts from the call (ADR 0085 :291) even when the test advances fake time as soon as its bounded wait ends;
    - in the loop, handle the stop at row 1 and row 6d: `Stop()`, cancel every in-progress token off the loop thread, wait at most T, and dispose each ended source;
    - check the stop token immediately before the first `Task.Run` (row 5);
    - never link a count's source to the stop token.

- [ ] **CHARACTERISE: T10.2 A stop returns as soon as cancelled counts end (AC-15 second clause)**
  - **USE COMMAND**: `/test-first a stop with a count that honours cancellation returns without fake time advancing`
  - Test location: "tests/Paramore.Brighter.Core.Tests/Observability/StaleMessages/"
  - Test file: `When_stopping_with_a_count_that_honours_cancellation_should_return_at_once.cs`
  - Test should verify:
    - `StopAsync` completes within the wall-clock bound with no further `Advance`.
    - 🔁 RED mutation: always wait T on the `TimeProvider` before returning.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE once RED is observed — before committing if the test was green on arrival, before implementing if it was not** *(fires in the `review-before` gear, which is the default)*
  - Implementation should (only if the test is RED on arrival):
    - end the loop as soon as every cancelled call has ended.

- [ ] **CHARACTERISE: T10.3 A stop cancels both counts' tokens (AC-15 third clause)**
  - **USE COMMAND**: `/test-first with both counts blocked and ignoring cancellation a stop at 12:01:05 cancels both tokens, returns by 12:01:15 and starts no later call of either count`
  - Test location: "tests/Paramore.Brighter.Core.Tests/Observability/StaleMessages/"
  - Test file: `When_stopping_with_both_counts_in_progress_should_cancel_both.cs`
  - Test should verify:
    - a publication on "orders";
    - after `StopAsync` is called at 12:01:05, and before fake time moves, a bounded wait as in T10.1 ends with both tokens' `IsCancellationRequested` true; `StopAsync` has completed by 12:01:15, and no call of either count starts in the next 10 min.
    - 🔁 RED mutation: cancel only the overall token at the stop.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE once RED is observed — before committing if the test was green on arrival, before implementing if it was not** *(fires in the `review-before` gear, which is the default)*
  - Implementation should (only if the test is RED on arrival):
    - cancel every in-progress source at the stop.

- [ ] **CHARACTERISE: T10.4 A stop records no outcome for in-progress calls and clears both gauges**
  - **USE COMMAND**: `/test-first a stop while both counts are in progress records no outcome, logs no failure, clears both gauges and keeps the totals`
  - Test location: "tests/Paramore.Brighter.Core.Tests/Observability/StaleMessages/" (in `StaleMessageLoggingCollection`)
  - Test file: `When_stopping_during_both_counts_should_record_no_outcome_and_clear_the_gauges.cs`
  - Test should verify:
    - after a completed 12:01 due time and a stop during the 12:02 calls, every outcome total equals its value before the stop;
    - neither gauge has a series, and no failure entry is logged.
    - 🔁 RED mutation: record `Abandon` for in-progress calls at the stop.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE once RED is observed — before committing if the test was green on arrival, before implementing if it was not** *(fires in the `review-before` gear, which is the default)*
  - Implementation should (only if the test is RED on arrival):
    - call `readings.Stop()` and nothing else on the readings at the stop.

- [ ] **CHARACTERISE: T10.5 A stop returns while a count still blocks its thread (AC-11 second half)**
  - **USE COMMAND**: `/test-first a stop at 12:02:30 with an overall count blocking its thread returns within ten seconds of fake time while the call is still blocked`
  - Test location: "tests/Paramore.Brighter.Core.Tests/Observability/StaleMessages/"
  - Test file: `When_stopping_with_a_count_blocking_its_thread_should_return_within_the_timeout.cs`
  - Test should verify:
    - after the T9.7 setup, `StopAsync(CancellationToken.None)` at 12:02:30 has completed once fake time reaches 12:02:40, while the double's thread is still blocked.
    - 🔁 RED mutation: remove the T bound from both `StopAsync` and the loop's stop wait, so each waits for the blocked call. The loop has one stop wait, whichever row sees the stop (ADR 0085 :297-302). In the T9.7 setup the 12:01 call was abandoned at 12:01:10 and row 4 skipped 12:02:00, so this stop reaches the loop at row 1. Either bound alone returns the stop by 12:02:40 (ADR 0085 :291, :302), so neither can be mutated on its own.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE once RED is observed — before committing if the test was green on arrival, before implementing if it was not** *(fires in the `review-before` gear, which is the default)*
  - Implementation should (only if the test is RED on arrival):
    - keep the T bound in `StopAsync` independent of the loop.

- [ ] **CHARACTERISE: T10.6 A stop while waiting for a due time, with an abandoned call still running, returns within T**
  - **USE COMMAND**: `/test-first a stop while the loop waits for the next due time with an abandoned call still running returns within T`
  - Test location: "tests/Paramore.Brighter.Core.Tests/Observability/StaleMessages/"
  - Test file: `When_stopping_between_due_times_with_an_abandoned_call_should_return_within_the_timeout.cs`
  - Test should verify:
    - the 12:01 call blocks asynchronously and ignores cancellation, as in AC-15, so it is abandoned at 12:01:10 and keeps running, and `StopAsync(CancellationToken.None)` at 12:01:30 has completed by 12:01:40.
    - 🔁 RED mutation: as in T10.5, remove the T bound from both `StopAsync` and the loop's stop wait, so each waits for the abandoned call. This stop also reaches the loop at row 1, and either bound alone returns it by 12:01:40. Unlike T10.5, the call holds no thread.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE once RED is observed — before committing if the test was green on arrival, before implementing if it was not** *(fires in the `review-before` gear, which is the default)*
  - Implementation should (only if the test is RED on arrival):
    - bound the loop's stop wait by T from the stop request.

- [ ] **CHARACTERISE: T10.7 A stop while a cancellation callback blocks returns within T (ADR 0085 risk :958)**
  - **USE COMMAND**: `/test-first a stop requested while a count's cancellation callback blocks still returns within T`
  - Test location: "tests/Paramore.Brighter.Core.Tests/Observability/StaleMessages/"
  - Test file: `When_stopping_while_a_cancellation_callback_blocks_should_return_within_the_timeout.cs`
  - Test should verify:
    - `StopAsync` called at 12:01:05 returns (the call itself is not held) and has completed by 12:01:15, while the callback still blocks.
    - 🔁 RED mutation: cancel each count's source inline from the stop token's registration.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE once RED is observed — before committing if the test was green on arrival, before implementing if it was not** *(fires in the `review-before` gear, which is the default)*
  - Implementation should (only if the test is RED on arrival):
    - leave the stop token's only registration completing the stop task.

- [ ] **TEST + IMPLEMENT: T10.8 `StopAsync` before start, or after a failed start, returns at once and never throws (ADR 0085 risk :956)**
  - **USE COMMAND**: `/test-first StopAsync before StartAsync or after StartAsync threw returns at once without an exception`
  - Test location: "tests/Paramore.Brighter.Core.Tests/Observability/StaleMessages/"
  - Test file: `When_stopping_a_monitor_that_never_started_should_return_at_once.cs`
  - Test should verify:
    - for a never-started monitor and for one whose `StartAsync` threw (either case of T6.3), `StopAsync` completes synchronously without an exception.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - return at once when the loop task is null, and never rethrow a faulted loop task.

- [ ] **CHARACTERISE: T10.9 `Dispose` only signals the stop**
  - **USE COMMAND**: `/test-first Dispose without StopAsync signals the stop, returns without waiting and disposes nothing a count still uses`
  - Test location: "tests/Paramore.Brighter.Core.Tests/Observability/StaleMessages/"
  - Test file: `When_the_monitor_is_disposed_should_signal_the_stop_without_waiting.cs`
  - Test should verify:
    - `Dispose` returns while the overall count is blocked asynchronously and ignores cancellation, so that call has not ended and the loop does not dispose its source while the test holds it (ADR 0085 :302);
    - right after `Dispose` returns, and before the test releases the call, reading the count's token `WaitHandle` inside `Record.Exception` gives no `ObjectDisposedException`. The loop cancels that token only after it observes the stop, at least one asynchronous hop after `Dispose`, and it cancels it off its own thread (ADR 0085 :293, :301, :306), so the test then waits, bounded as in determinism rule 1, until `IsCancellationRequested` is true (determinism rule 2);
    - no call starts later.
    - 🔁 RED mutation: in `Dispose`, dispose the in-progress token sources before signalling the stop. Reading `WaitHandle` on a disposed source throws `ObjectDisposedException`, which `IsCancellationRequested` does not.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE once RED is observed — before committing if the test was green on arrival, before implementing if it was not** *(fires in the `review-before` gear, which is the default)*
  - Implementation should (only if the test is RED on arrival):
    - cancel the stop token in `Dispose` if `StopAsync` has not.

- [ ] **CHARACTERISE: T10.10 The host's stop token ends the stop wait (ADR 0085 risk :954)**
  - **USE COMMAND**: `/test-first StopAsync with a cancelled host token returns without waiting for T while a count ignores cancellation`
  - Test location: "tests/Paramore.Brighter.Core.Tests/Observability/StaleMessages/"
  - Test file: `When_the_host_token_is_cancelled_during_stop_should_end_the_wait.cs`
  - Test should verify:
    - with a count ignoring cancellation, `StopAsync(cancelledToken)` completes without fake time advancing, and does not throw.
    - 🔁 RED mutation: ignore the host token in the wait.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE once RED is observed — before committing if the test was green on arrival, before implementing if it was not** *(fires in the `review-before` gear, which is the default)*
  - Implementation should (only if the test is RED on arrival):
    - include the host token in the `WhenAny`, with no exception surfaced.

- [ ] **PROJECT: T10.11 Review the stop check before the first `Task.Run` (row 5; ADR 0085 risk :955)**
  - In `src/Paramore.Brighter.Outbox.Hosting/TimedOutboxStaleMessageMonitor.cs`, check that the loop reads the stop token immediately before the first `Task.Run`, and that the window ADR 0085 :295 describes is handled by the next wait as row 6d.
  - Output: a "Reviewed:" sub-bullet under this task, with the file and lines checked.

## Phase 11: Isolation from the sweeper and the posting path (ADR 0085 step 11)

*Depends on Phase 10.*

No production change is expected in this phase. The named mutation is how RED is observed.

- [ ] **CHARACTERISE: T11.1 Measurement runs on schedule beside a hung sweep and takes no sweeper lock (AC-13)**
  - **USE COMMAND**: `/test-first with UseOutboxSweeper and a sweep blocked forever five measurements start on their due times and the lock double records the same calls as with the feature disabled`
  - Test location: "tests/Paramore.Brighter.Core.Tests/Observability/StaleMessages/"
  - Test file: `When_a_sweep_hangs_should_measure_on_schedule.cs`
  - Test should verify:
    - `StaleMessageCountingOutbox`'s `OutstandingMessagesAsync` blocks while its count returns at once, and `RecordingDistributedLock` is registered;
    - over 5 intervals, 5 overall calls start at their due times;
    - the lock double's calls equal those of the same run without the stale step;
    - T8.2's test registers no sweeper (first sentence of AC-13).
    - 🔁 RED mutation: make the monitor obtain the `OutboxSweeper` lock (`src/Paramore.Brighter.Outbox.Hosting/TimedOutboxSweeper.cs:62`) through `IDistributedLock` around each due time.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE once RED is observed — before committing if the test was green on arrival, before implementing if it was not** *(fires in the `review-before` gear, which is the default)*
  - Implementation should (only if the test is RED on arrival): none is expected, so stop and ask.

- [ ] **CHARACTERISE: T11.2 Posting is not delayed by blocked async counts (AC-20, async block)**
  - **USE COMMAND**: `/test-first with both counts blocked asynchronously 100 Post and 100 PostAsync calls return and the posting-path calls match the feature-disabled run`
  - Test location: "tests/Paramore.Brighter.Core.Tests/Observability/StaleMessages/"
  - Test file: `When_both_counts_are_blocked_should_not_delay_posting.cs`
  - Test should verify:
    - a publication on "orders" whose `RequestType` is the request's type, an `InMemoryMessageProducer` for it with `UseAsyncPublishConfirmation` left at its default of false (`src/Paramore.Brighter/InMemoryMessageProducer.cs:112`), a sync and an async message mapper for the request, and `StaleMessageTopicCountingOutbox`, so that the per-topic count runs beside the overall count. With the AC setup's empty `ProducerRegistry`, or a publication whose `RequestType` is not the request's type, a post would throw `ConfigurationException` before it adds (`src/Paramore.Brighter/FindPublicationByPublicationTopicOrRequestType.cs:75-81`). Each run builds its own producer, registry and double, because the mediator subscribes to its producer's confirmations when it is built (`src/Paramore.Brighter/OutboxProducerMediator.cs:205`, `:909`). The double keeps the messages that its `Add` and `AddAsync` are given, so that its `Get` and `GetAsync` return them (`:396`, `:465`) and each post sends. Its `Add`, `AddAsync`, `Get`, `GetAsync` and `MarkDispatchedAsync` complete without throwing;
    - in both runs, before the first post, fake time advances to 12:01:00, the first due time (ADR 0085 :449). In the run with the step, the test then waits, bounded as in determinism rule 1, until the double has recorded the async overall call and the per-topic call. The double records a call before it blocks it, so the same wait holds in T11.3. Both calls stay blocked while the run posts. Fake time stays at 12:01:00, short of their deadline at 12:01:10 (ADR 0085 :247), so neither call is abandoned. Without the advance, no count starts during the posts, and the named mutation cannot make the test fail. Without the wait, the first posts can run before the loop starts the counts, and the mutation may not make the test fail;
    - every call returns without an exception within a 30 s wall-clock guard. The test makes the posts of a run inside one `Task.Run` and waits for that task with `WaitAsync(TimeSpan.FromSeconds(30))`, so a post that blocks fails the test instead of hanging it;
    - in each run, a bounded wait, as in determinism rule 1, polls until the double has recorded 200 marks. After it, the double has recorded 200 adds. Both runs post the same requests in the same order, and each message mapper gives its message the request's `Id`. The adds, compared as one ordered list of message ids, equal those of the run without the step. So do the marks, compared as a set of message ids, because the mediator does not await the confirmation task that makes each mark (`src/Paramore.Brighter/OutboxProducerMediator.cs:909`);
    - the test releases the blocked counts with a result in the test class's `Dispose`, before anything that stops the monitor. A stop cancels their tokens and then waits for them, at most until T after the stop request on the fake clock (ADR 0085 :291, :301-302), and fake time does not move past 12:01:00. So a count that ignores its token, or that blocks its thread as in T11.3, would hold a stop that came first. With the named mutation, the release also lets the loop release the semaphore, so no post stays blocked after the test.
    - 🔁 RED mutation: a temporary public static `SemaphoreSlim(1)` on a new non-generic static class in `Paramore.Brighter`, which the monitor's project references (`src/Paramore.Brighter.Outbox.Hosting/Paramore.Brighter.Outbox.Hosting.csproj:19`). It is not a field of `OutboxProducerMediator<TMessage, TTransaction>` (`src/Paramore.Brighter/OutboxProducerMediator.cs:48`), because a static field of a generic type is a separate field for each closed type. It must be public, because the repository uses no `InternalsVisibleTo` (`src/Paramore.Brighter/AmbientScopeSuppression.cs:41`). The mediator's `AddToOutbox` and `AddToOutboxAsync` (`src/Paramore.Brighter/OutboxProducerMediator.cs:319`, `:272`) each take it and release it. The monitor's loop takes it before it starts the calls of a due time and releases it once both calls have ended. The counts stay blocked until `Dispose` releases them, so the first post waits on the semaphore and the 30 s guard fails.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE once RED is observed — before committing if the test was green on arrival, before implementing if it was not** *(fires in the `review-before` gear, which is the default)*
  - Implementation should (only if the test is RED on arrival): none is expected, so stop and ask.

- [ ] **CHARACTERISE: T11.3 Posting is not delayed by counts that block their threads (AC-20, thread block)**
  - **USE COMMAND**: `/test-first with both counts blocking their threads 100 Post and 100 PostAsync calls return and the posting-path calls match the feature-disabled run`
  - Test location: "tests/Paramore.Brighter.Core.Tests/Observability/StaleMessages/"
  - Test file: `When_both_counts_block_their_threads_should_not_delay_posting.cs`
  - Test should verify:
    - the setup and assertions of T11.2, with the double blocking the calling thread before returning a task. The test releases the blocked counts in the test class's `Dispose` before anything that stops the monitor, as T11.2 does, so no pool thread is left blocked after the test and no stop waits on a blocked thread.
    - 🔁 RED mutation: the same temporary shared `SemaphoreSlim` as T11.2.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE once RED is observed — before committing if the test was green on arrival, before implementing if it was not** *(fires in the `review-before` gear, which is the default)*
  - Implementation should (only if the test is RED on arrival): none is expected, so stop and ask.

- [ ] **CHARACTERISE: T11.4 A blocked outstanding-message check does not delay a measurement (AC-20, check blocked)**
  - **USE COMMAND**: `/test-first with MaxOutStandingMessages of 10 and the check's sync count blocked the next stale measurement of each count starts at its due time`
  - Test location: "tests/Paramore.Brighter.Core.Tests/Observability/StaleMessages/"
  - Test file: `When_the_outstanding_check_is_blocked_should_measure_on_schedule.cs`
  - Test should verify:
    - `MaxOutStandingMessages` of 10, and the publication, producer and sync message mapper that T11.5 describes, so that a post adds and queues the check;
    - the double blocks sync count calls with `maxCount` 11;
    - one `Post`, then a bounded wait, as in determinism rule 1, until the double has recorded a sync count call with `maxCount` 11, which it holds. Only then does fake time advance to the next due time, which records an async overall call and a per-topic call at its instant;
    - the test releases the blocked sync count in `Dispose`, so the mediator's check thread is not left blocked after the test.
    - 🔁 RED mutation: make the loop call the sync `GetOutstandingMessageCount` with `maxCount` 11 (`MaxOutStandingMessages + 1`, as `src/Paramore.Brighter/OutboxProducerMediator.cs:1429` computes it) before it starts the calls of a due time. Make the call in the generic helper the monitor closes over the transaction type (ADR 0085 :455), through a cast to `IAmAnOutboxSync<Message, TTransaction>`, because `IAmAnOutbox` does not declare that count. The double blocks that call, so neither count starts at its instant.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE once RED is observed — before committing if the test was green on arrival, before implementing if it was not** *(fires in the `review-before` gear, which is the default)*
  - Implementation should (only if the test is RED on arrival): none is expected, so stop and ask.

- [ ] **CHARACTERISE: T11.5 The outstanding-message check is unchanged (AC-21)**
  - **USE COMMAND**: `/test-first with MaxOutStandingMessages of 5 the check's count arguments and the first OutboxLimitReachedException post are the same with the feature enabled and disabled`
  - Test location: "tests/Paramore.Brighter.Core.Tests/Observability/StaleMessages/" (in `StaleMessageLoggingCollection`)
  - Test file: `When_the_feature_is_enabled_should_leave_the_outstanding_check_unchanged.cs`
  - Test should verify:
    - a publication on "orders", as in AC-20, whose `RequestType` is the request's type, with a producer whose send succeeds, such as `InMemoryMessageProducer` with `UseAsyncPublishConfirmation` left at its default of false (`src/Paramore.Brighter/InMemoryMessageProducer.cs:112`), and a sync message mapper for the request. Each run builds its own producer, registry and double. With the AC setup's empty `ProducerRegistry`, or a publication whose `RequestType` is not the request's type, a post would throw `ConfigurationException` before it adds (`src/Paramore.Brighter/FindPublicationByPublicationTopicOrRequestType.cs:75-81`). A call that fails inside the mediator's resilience pipeline calls `CheckOutstandingMessages` from the pipeline's catch (`src/Paramore.Brighter/OutboxProducerMediator.cs:1495`, `:1537`), which can queue a check that the post would not otherwise queue. The pipeline wraps the double's `Add` (`:338-343`), the send (`:1113`) and the double's `MarkDispatchedAsync`, which the mediator calls when `InMemoryMessageProducer` confirms a send. With that default, the producer confirms inline in `Send` (`src/Paramore.Brighter/InMemoryMessageProducer.cs:261`, `OutboxProducerMediator.cs:909`, `:932-937`). A `Get` that throws ends the post before its check (`:396`, `:430`). So the double's `Add`, `Get` and `MarkDispatchedAsync` complete without throwing. The posts are `Post` calls, and each queues one check after its clear (`:430`);
    - `AddProducers` leaves `MaxOutStandingCheckInterval` at `TimeSpan.Zero` (`src/Paramore.Brighter/ProducersConfiguration.cs:219`), so the throttle at `src/Paramore.Brighter/OutboxProducerMediator.cs:835` drops no check. Keep that default in both runs, and have the double's sync count return the number of messages added to it so far;
    - the sync count calls, compared as one ordered list of (`dispatchedSince`, `maxCount`, `args`), with `args` compared by its entries because each run's mediator passes its own dictionary (`src/Paramore.Brighter/OutboxProducerMediator.cs:200`), are equal between the two runs, the number of calls included;
    - in each run, before the first post, the double has recorded no sync count call. The enabled run reads this after `StartAsync` returns;
    - after each post that returns, a bounded wait, as in determinism rule 1, polls until the double has counted the return of as many sync count calls as posts have returned in that run, and `CapturingLogger` holds at least as many "Current outstanding count" Debug entries from that run. The mediator logs that entry under the `Paramore.Brighter.CommandProcessor` category (`src/Paramore.Brighter/OutboxProducerMediator.cs:52`, `:1616-1617`), in the `finally` at `:1471`, after it stores the count at `:1433`. So the test also captures that category, once, after it resets the provider and before the first run, and each run clears only the entries before its first post, which keeps that category captured. A post that throws `OutboxLimitReachedException` adds nothing and queues no check (`:288`, `:333`, `:806-820`), so the test does not wait after it. `CountingOutbox.WaitForOutstandingCountCallsAsync` (`tests/Paramore.Brighter.Core.Tests/CommandProcessors/TestDoubles/CountingOutbox.cs:78-87`) sees a call only when it starts (`:89-93`), which is why the double counts returns;
    - after that entry, the mediator still clears its in-flight flag (`OutboxProducerMediator.cs:861`), and the test has no signal for that line. If the pool thread is held between the entry and the flag, the next post finds the check still in flight and skips it (`:842`), and the wait for that post times out. An entry in that category from another test's leftover check can end the log half of the wait early, so the wait rests on the double's count of returns alone. The double counts a return just before its sync count returns, so before the mediator stores the count at `:1433`. The next post can then skip its check, as above, or read the old count, in which case the first `OutboxLimitReachedException` can come on a later post in that run than in the other run. Each of these windows can only make the test fail, and its `///` summary names them. When the test runs without the mutation, if a wait times out, or the two runs differ in their sync count calls or in the post that first throws `OutboxLimitReachedException`, stop and ask;
    - `OutboxLimitReachedException` is thrown in both runs, first on the same post, and every earlier post returns without an exception. Each run makes at least 7 posts: the count that the check stores after the sixth post is 6, above the limit of 5 (`src/Paramore.Brighter/OutboxProducerMediator.cs:815`), so the seventh post is the earliest that can throw;
    - the existing check tests in `tests/Paramore.Brighter.Core.Tests/CommandProcessors/` pass unchanged.
    - 🔁 RED mutation: in `StartAsync`, before it returns, make the monitor call the sync count once with the check's arguments. `IAmAnOutbox` does not declare that count (`src/Paramore.Brighter/IAmAnOutboxSync.cs:139`), so make the call in the generic helper the monitor closes over the transaction type (ADR 0085 :455): cast the outbox to `IAmAnOutboxSync<Message, TTransaction>` and pass `MaxOutStandingCheckInterval`, `MaxOutStandingMessages + 1` and `OutBoxBag` from `IAmProducersConfiguration`, or an empty dictionary when `OutBoxBag` is null. The double then records that call before the first post, and the assertion that it records none fails in the enabled run.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE once RED is observed — before committing if the test was green on arrival, before implementing if it was not** *(fires in the `review-before` gear, which is the default)*
  - Implementation should (only if the test is RED on arrival): none is expected, so stop and ask.

## Phase 12: Registration end to end (ADR 0085 step 12)

*Depends on Phase 10.*

No production change is expected in this phase. The named mutation is how RED is observed.

- [ ] **CHARACTERISE: T12.1 Without the step, nothing is measured (AC-1 first clause)**
  - **USE COMMAND**: `/test-first with AddProducers, a publication age and AddBrighterInstrumentation but no stale step ten minutes pass with no count call and no gauge value`
  - Test location: "tests/Paramore.Brighter.Core.Tests/Observability/StaleMessages/"
  - Test file: `When_the_stale_step_is_not_added_should_measure_nothing.cs`
  - Test should verify:
    - `StaleMessageTopicCountingOutbox` with `MaxOutStandingMessages = -1`, and "orders" with A_p 1 min, on `StaleMessageMetricsFixture`;
    - after 10 min, no call of either count, and no value from either gauge.
    - 🔁 RED mutation: in `src/Paramore.Brighter/Observability/OutboxStaleMessageMeter.cs`, when `readings` is null, create the gauges anyway and report 0 from each. This trips "no value from either gauge". No mutation of `Paramore.Brighter.Extensions.Diagnostics` or `Paramore.Brighter.Extensions.DependencyInjection` can start the loop without the step, because neither project references `Paramore.Brighter.Outbox.Hosting`, where the monitor and the step live. T3.4 covers the meter without readings.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE once RED is observed — before committing if the test was green on arrival, before implementing if it was not** *(fires in the `review-before` gear, which is the default)*
  - Implementation should (only if the test is RED on arrival): none is expected, so stop and ask.

- [ ] **CHARACTERISE: T12.2 With the step, both counts are measured and reported (AC-1 second clause)**
  - **USE COMMAND**: `/test-first adding the stale step to the same setup records at least one call and one value of each count`
  - Test location: "tests/Paramore.Brighter.Core.Tests/Observability/StaleMessages/"
  - Test file: `When_the_stale_step_is_added_should_measure_and_report_both_counts.cs`
  - Test should verify:
    - at least one call of each count is recorded, and at least one value is received from each gauge.
    - 🔁 RED mutation: remove the `OutboxStaleMessageMeter` resolution from the method extracted in T4.1.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE once RED is observed — before committing if the test was green on arrival, before implementing if it was not** *(fires in the `review-before` gear, which is the default)*
  - Implementation should (only if the test is RED on arrival): none is expected, so stop and ask.

- [ ] **CHARACTERISE: T12.3 Without instrumentation or a listener, measurement runs and nothing fails (AC-24)**
  - **USE COMMAND**: `/test-first without AddBrighterInstrumentation and without a listener five intervals run and stop with nothing thrown and nothing logged as a failure`
  - Test location: "tests/Paramore.Brighter.Core.Tests/Observability/StaleMessages/" (in `StaleMessageLoggingCollection`)
  - Test file: `When_no_instrumentation_is_configured_should_measure_without_failing.cs`
  - Test should verify:
    - a publication on "orders", 5 intervals, then a stop: no exception, and no failure or Error entry;
    - the `completed` totals in the readings are 5 for each count.
    - 🔁 RED mutation: resolve `OutboxStaleMessageMeter` with `GetRequiredService` in `StartAsync`.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE once RED is observed — before committing if the test was green on arrival, before implementing if it was not** *(fires in the `review-before` gear, which is the default)*
  - Implementation should (only if the test is RED on arrival): none is expected, so stop and ask.

- [ ] **CHARACTERISE: T12.4 An invalid publication age without the step is ignored (AC-35)**
  - **USE COMMAND**: `/test-first a publication age of zero without the stale step throws nothing and causes no count call`
  - Test location: "tests/Paramore.Brighter.Core.Tests/Observability/StaleMessages/"
  - Test file: `When_a_publication_age_is_invalid_without_the_stale_step_should_be_ignored.cs`
  - Test should verify:
    - building the publication, the producer registry and the host, and starting the host, all run inside one asserted `Record.Exception`, which is null;
    - then 10 min pass with no call of either count.
    - 🔁 RED mutation: make the `Publication.StaleMessageAge` setter throw `ConfigurationException` for a value of zero or less.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE once RED is observed — before committing if the test was green on arrival, before implementing if it was not** *(fires in the `review-before` gear, which is the default)*
  - Implementation should (only if the test is RED on arrival): none is expected, so stop and ask.

## Phase 13: Outbox conformance for the overall count (ADR 0085 step 13)

*Depends on no earlier phase, because it tests the overall count the outboxes already have.*

- [ ] **CHARACTERISE: T13.1 `InMemoryOutbox` counts only stale messages (AC-16 age case)**
  - **USE COMMAND**: `/test-first InMemoryOutbox counts five of AC-16's nine messages with an age of five minutes`
  - Test location: "tests/Paramore.Brighter.InMemory.Tests/Outbox/"
  - Test file: `InMemoryOutstandingCountOutboxTests.cs` with the fact `When_counting_outstanding_messages_older_than_the_age_should_count_only_stale_ones`
  - Test code this task also adds:
    - add `OutstandingCountOutboxBaseTests<TTransaction>` (`Outbox`, `ReadConsistencyTimeout`, `CreateStore`, `DeleteStore`, `AddMessageWithAgeAsync`, `BeforeCount`, `AssertAgeAsync`, `AssertCeilingOfThreeAsync`, `AssertCeilingOfFiveAsync` and `ObserveCountAsync`) with no `[Fact]`;
    - add `OutstandingCountCancellationOutboxBaseTests<TTransaction>` and the sealed `OutstandingCountCancellationSetup<TTransaction>` (its `DisposeAsync` never throws), following the AC-17 flow at ADR 0085 :627-636;
    - place all three in `tests/Paramore.Brighter.Base.Test/Outbox/`, beside `CausationTrackingOutboxBaseTests`.
  - Test should verify:
    - `AssertAgeAsync`: 5 undispatched 10 min old, 2 dispatched 10 min old and 2 undispatched 1 min old, on a `FakeTimeProvider` advanced in `BeforeCount`, give 5 with C 100.
    - 🔁 RED mutation: drop the dispatched filter from `InMemoryOutbox`'s overall count (`src/Paramore.Brighter/InMemoryOutbox.cs:633`).
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE once RED is observed — before committing if the test was green on arrival, before implementing if it was not** *(fires in the `review-before` gear, which is the default)*
  - Implementation should (only if the test is RED on arrival): none is expected, so stop and ask.

- [ ] **CHARACTERISE: T13.2 `InMemoryOutbox` stops at the ceiling (AC-16 ceiling cases)**
  - **USE COMMAND**: `/test-first InMemoryOutbox counts three with a ceiling of three and five with a ceiling of five over AC-16's messages`
  - Test location: "tests/Paramore.Brighter.InMemory.Tests/Outbox/"
  - Test file: `InMemoryOutstandingCountOutboxTests.cs` with the facts `When_counting_outstanding_messages_with_a_ceiling_of_three_should_return_three` and `When_counting_outstanding_messages_with_a_ceiling_of_five_should_return_five`
  - Test should verify:
    - `AssertCeilingOfThreeAsync` gives 3, and `AssertCeilingOfFiveAsync` gives 5.
    - 🔁 RED mutation (C=3): ignore `maxCount` in `InMemoryOutbox`'s overall count (`src/Paramore.Brighter/InMemoryOutbox.cs:635`).
    - 🔁 RED mutation (C=5): change `.Take(maxCount)` to `.Take(maxCount - 1)` at `src/Paramore.Brighter/InMemoryOutbox.cs:635`; the C=5 fact fails with 4, not 5.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE once RED is observed — before committing if the test was green on arrival, before implementing if it was not** *(fires in the `review-before` gear, which is the default)*
  - Implementation should (only if the test is RED on arrival): none is expected, so stop and ask.

- [ ] **TEST + IMPLEMENT: T13.3 The generator renders a fourth outbox suite for every outbox, with no `SkipTest` filter**
  - **USE COMMAND**: `/test-first OutboxGenerator renders the OutstandingCount suite and its provider interface for every configured outbox whatever the template names`
  - Test location: "tests/Paramore.Brighter.Test.Generator.Tests/OutboxGenerator/"
  - Test file: `When_generating_outbox_suites_should_render_the_outstanding_count_suite_for_every_outbox.cs`
  - Test should verify:
    - an outbox with `SupportsTransactions` false still gets every OutstandingCount file, including one whose name contains "Transaction";
    - `Plan` lists the suite under `Outbox/<folder>/Generated/OutstandingCount/`;
    - the collection test (`When_generating_outbox_suites_should_honor_collection_configuration.cs:109`) gains OutstandingCount rows in its theory (`:43-55`), and its filter leaves out `IAmAnOutstandingCountOutboxProvider` as it leaves out the Sync and Async provider interfaces;
    - with `Category` set, every OutstandingCount test file carries `[Trait("Category", "<Category>")]`, and with it unset none does.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - in `tools/Paramore.Brighter.Test.Generator/Generators/OutboxGenerator.cs` `SuitesFor` (`:140-155`), yield a fourth `GenerationSuite` after the `:146` loop, with an ignore that always returns false;
    - update the comments at `:127` and `:135`, and the `Suites` summary at `:81`, to name the fourth suite;
    - add `Templates/Outbox/OutstandingCount/`: `IAmAnOutstandingCountOutboxProvider.cs.liquid` (`CreateOutstandingCountStore`, `DeleteOutstandingCountStore`, `PrepareCancellationAsync`), `When_counting_outstanding_messages_older_than_the_age_should_count_only_stale_ones.cs.liquid`, `When_counting_outstanding_messages_with_a_ceiling_should_stop_at_the_ceiling.cs.liquid` and `When_an_outstanding_count_is_cancelled_should_end_the_call.cs.liquid`;
    - make the generated classes derive from the base classes and call the provider, as the causation template does;
    - render one test class per generated file, opening with the `[Trait("Category", ...)]` and `[Collection(...)]` lines of `Templates/Outbox/Causation/CausationTrackingOutboxTests.cs.liquid:34-36`. The collection test asserts exactly one `[Collection(` line per file (`When_generating_outbox_suites_should_honor_collection_configuration.cs:122-123`), and `gcp-ci` leaves out the Spanner facts only through `Category!=Spanner` (`.github/workflows/ci.yml:823`);
    - do not regenerate any outbox project in this task;
    - expect the generated-tree audit in `Paramore.Brighter.Test.Generator.Tests` to report Missing entries until T13.13. They are not a regression to fix.

- [ ] **TEST + IMPLEMENT: T13.4 The read-consistency setting renders a retry window for the suite**
  - **USE COMMAND**: `/test-first OutstandingCountReadConsistencyTimeoutInMilliseconds renders the suite's ReadConsistencyTimeout, zero by default`
  - Test location: "tests/Paramore.Brighter.Test.Generator.Tests/OutboxGenerator/"
  - Test file: `When_an_outbox_sets_an_outstanding_count_read_consistency_timeout_should_render_it.cs`
  - Test should verify:
    - with 10000, the rendered classes override `ReadConsistencyTimeout` with 10 s;
    - by default they render zero or no override;
    - the existing `ReadConsistencyTimeoutInMilliseconds` does not affect it.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - add the property to `tools/Paramore.Brighter.Test.Generator/Configuration/OutboxConfiguration.cs` (beside `:36-77`), included in `WithDefaultsFrom`, and read it in the templates;
    - expect the generated-tree audit in `Paramore.Brighter.Test.Generator.Tests` to report Missing entries until T13.13. They are not a regression to fix.

- [ ] **TEST + IMPLEMENT: T13.5 The below-scan-concurrency setting renders two observation facts**
  - **USE COMMAND**: `/test-first OutstandingCountBelowScanConcurrency renders facts for a ceiling of one and two that write the observed count to the test output`
  - Test location: "tests/Paramore.Brighter.Test.Generator.Tests/OutboxGenerator/"
  - Test file: `When_an_outbox_counts_below_its_scan_concurrency_should_render_observation_facts.cs`
  - Test should verify:
    - when true, the ceiling file has two more facts that call `ObserveCountAsync(1)` and `ObserveCountAsync(2)`, write the text to `ITestOutputHelper`, and assert nothing, because AC-16 asks for both counts with no expected value;
    - when false (the default), they are absent.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - add the property to `OutboxConfiguration.cs`, and add a Liquid `if` block inside the ceiling template's one test class;
    - expect the generated-tree audit in `Paramore.Brighter.Test.Generator.Tests` to report Missing entries until T13.13. They are not a regression to fix.

- [ ] **TEST + IMPLEMENT: T13.6 A gap mark renders that column's facts with a Deferred Skip**
  - **USE COMMAND**: `/test-first each outstanding-count gap mark renders its column's facts with Skip Deferred and the issue number, and no mark renders no Skip`
  - Test location: "tests/Paramore.Brighter.Test.Generator.Tests/OutboxGenerator/"
  - Test file: `When_an_outstanding_count_gap_is_marked_should_render_a_deferred_skip.cs`
  - Test should verify:
    - `OutstandingCountAgeGap` = 1234 renders the age fact with `Skip = "Deferred: #1234 ..."` and a fixed reason for column (a);
    - `OutstandingCountCeilingGap` renders both ceiling facts skipped;
    - `OutstandingCountCancellationGap` renders the cancellation fact skipped;
    - with every mark empty, no `Skip` is rendered.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - add the three marks to `OutboxConfiguration.cs`, empty by default, and render the skip in the three templates;
    - expect the generated-tree audit in `Paramore.Brighter.Test.Generator.Tests` to report Missing entries until T13.13. They are not a regression to fix.

- [ ] **GENERATE: T13.7 Sqlite runs the suite against its store**
  - Generated tests. Do not use `/test-first`, and never write or edit a generated file by hand.
  - Test location: "tests/Paramore.Brighter.Sqlite.Tests/Outbox/Binary/Generated/OutstandingCount/" and "tests/Paramore.Brighter.Sqlite.Tests/Outbox/Text/Generated/OutstandingCount/"
  - Test file: the files the generator renders from the templates of T13.3, never written by hand
  - The generated tests should verify:
    - after regenerating from `tests/Paramore.Brighter.Sqlite.Tests/`, the (a) and (b) facts pass, because both cells are Meets;
    - the (c) fact holds the store for 30 s and ends with `OperationCanceledException` or the client's exception within 5 s of cancellation (the cell is "To verify").
  - Implementation should:
    - implement `IAmAnOutstandingCountOutboxProvider` on `tests/Paramore.Brighter.Sqlite.Tests/Outbox/Binary/SqliteBinaryOutboxProvider.cs` and `Text/SqliteTextOutboxProvider.cs`;
    - for the hold, create a database file of its own in `DELETE` journal mode, held by a second connection with `BEGIN EXCLUSIVE` (A-8: the count runs synchronously, which is why the base runs it with `Task.Run`);
    - leave the gap marks unset in `test-configuration.json`;
    - build the generator (`dotnet build tools/Paramore.Brighter.Test.Generator`) and run it from `tests/Paramore.Brighter.Sqlite.Tests/` (`dotnet run --no-build --project ../../tools/Paramore.Brighter.Test.Generator`);
    - run `dotnet test tests/Paramore.Brighter.Sqlite.Tests --filter "FullyQualifiedName~OutstandingCount"`;
    - record the observed results in the FR-14 table (`specs/0038-outbox-stale-message-telemetry/requirements.md:221-229`), Sqlite row, column (c);
    - do not run the generated-tree audit in this task. It reports Missing entries until T13.13, which runs it.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before committing the generated files** *(fires in the `review-before` gear, which is the default)*

- [ ] **GENERATE: T13.8 MsSql runs the suite against its store**
  - Generated tests. Do not use `/test-first`, and never write or edit a generated file by hand.
  - Test location: "tests/Paramore.Brighter.MSSQL.Tests/Outbox/Binary/Generated/OutstandingCount/" and "tests/Paramore.Brighter.MSSQL.Tests/Outbox/Text/Generated/OutstandingCount/"
  - Test file: the files the generator renders from the templates of T13.3, never written by hand
  - The generated tests should verify:
    - the (a) and (b) facts pass;
    - the (c) fact ends within 5 s of cancellation while the lock holds. Its cell is "To verify": the 15 s default `OutstandingCountTimeout` (`src/Paramore.Brighter/RelationalDatabaseConfiguration.cs:98`) must not end the call first.
  - Implementation should:
    - implement the interface on `tests/Paramore.Brighter.MSSQL.Tests/Outbox/Binary/MSSQLBinaryOutboxProvider.cs` and `Text/MSSQLTextOutboxProvider.cs`, with a hold in a lock mode that blocks a plain read (for example `TABLOCKX` in an open transaction);
    - build the generator (`dotnet build tools/Paramore.Brighter.Test.Generator`) and run it from `tests/Paramore.Brighter.MSSQL.Tests/` (`dotnet run --no-build --project ../../tools/Paramore.Brighter.Test.Generator`);
    - run `dotnet test tests/Paramore.Brighter.MSSQL.Tests --filter "FullyQualifiedName~OutstandingCount"`;
    - record the observed results in the FR-14 table, MsSql row;
    - do not run the generated-tree audit in this task. It reports Missing entries until T13.13, which runs it.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before committing the generated files** *(fires in the `review-before` gear, which is the default)*

- [ ] **GENERATE: T13.9 PostgreSql runs the suite against its store**
  - Generated tests. Do not use `/test-first`, and never write or edit a generated file by hand.
  - Test location: "tests/Paramore.Brighter.PostgresSQL.Tests/Outbox/Binary/Generated/OutstandingCount/" and "tests/Paramore.Brighter.PostgresSQL.Tests/Outbox/Text/Generated/OutstandingCount/"
  - Test file: the files the generator renders from the templates of T13.3, never written by hand
  - The generated tests should verify:
    - the (a) and (b) facts pass, and the (c) fact ends within 5 s of cancellation.
  - Implementation should:
    - implement the interface on `tests/Paramore.Brighter.PostgresSQL.Tests/Outbox/Binary/PostgresBinaryOutboxProvider.cs` and `Text/PostgresTextOutboxProvider.cs`;
    - build the generator (`dotnet build tools/Paramore.Brighter.Test.Generator`) and run it from `tests/Paramore.Brighter.PostgresSQL.Tests/` (`dotnet run --no-build --project ../../tools/Paramore.Brighter.Test.Generator`);
    - run `dotnet test tests/Paramore.Brighter.PostgresSQL.Tests --filter "FullyQualifiedName~OutstandingCount"`;
    - record the observed results in the FR-14 table, PostgreSql row;
    - do not run the generated-tree audit in this task. It reports Missing entries until T13.13, which runs it.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before committing the generated files** *(fires in the `review-before` gear, which is the default)*

- [ ] **GENERATE: T13.10 MySql runs the suite against its store**
  - Generated tests. Do not use `/test-first`, and never write or edit a generated file by hand.
  - Test location: "tests/Paramore.Brighter.MySQL.Tests/Outbox/Binary/Generated/OutstandingCount/" and "tests/Paramore.Brighter.MySQL.Tests/Outbox/Text/Generated/OutstandingCount/"
  - Test file: the files the generator renders from the templates of T13.3, never written by hand
  - The generated tests should verify:
    - the (a) and (b) facts pass, and the (c) fact ends within 5 s of cancellation.
  - Implementation should:
    - implement the interface on `tests/Paramore.Brighter.MySQL.Tests/Outbox/Binary/MySQLBinaryOutboxProvider.cs` and `Text/MySQLTextOutboxProvider.cs` (for example `LOCK TABLES ... WRITE`);
    - build the generator (`dotnet build tools/Paramore.Brighter.Test.Generator`) and run it from `tests/Paramore.Brighter.MySQL.Tests/` (`dotnet run --no-build --project ../../tools/Paramore.Brighter.Test.Generator`);
    - run `dotnet test tests/Paramore.Brighter.MySQL.Tests --filter "FullyQualifiedName~OutstandingCount"`;
    - record the observed results in the FR-14 table, MySql row;
    - do not run the generated-tree audit in this task. It reports Missing entries until T13.13, which runs it.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before committing the generated files** *(fires in the `review-before` gear, which is the default)*

- [ ] **GENERATE: T13.11 MongoDb runs the suite against its store**
  - Generated tests. Do not use `/test-first`, and never write or edit a generated file by hand.
  - Test location: "tests/Paramore.Brighter.MongoDb.Tests/Outbox/Generated/OutstandingCount/"
  - Test file: the files the generator renders from the templates of T13.3, never written by hand
  - The generated tests should verify:
    - the (a) and (b) facts pass;
    - the (c) fact, with `StoreHeld` false, ends with `OperationCanceledException` or the driver's cancellation exception.
  - Implementation should:
    - implement the interface on `tests/Paramore.Brighter.MongoDb.Tests/Outbox/MongoDbOutboxProvider.cs`, with a collection per case;
    - build the generator (`dotnet build tools/Paramore.Brighter.Test.Generator`) and run it from `tests/Paramore.Brighter.MongoDb.Tests/` (`dotnet run --no-build --project ../../tools/Paramore.Brighter.Test.Generator`);
    - run `dotnet test tests/Paramore.Brighter.MongoDb.Tests --filter "FullyQualifiedName~OutstandingCount"`;
    - record the observed results in the FR-14 table, MongoDb row;
    - do not run the generated-tree audit in this task. It reports Missing entries until T13.13, which runs it.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before committing the generated files** *(fires in the `review-before` gear, which is the default)*

- [ ] **GENERATE: T13.12 DynamoDB and DynamoDB V4 run the suite in lockstep**
  - Generated tests. Do not use `/test-first`, and never write or edit a generated file by hand.
  - Deviation from the `GENERATE` shape: the Meets facts must pass, but a Gap fact may fail, and its failure is the result to record, not a defect to fix. It stays red until T13.17 sets its gap mark. Run this task with `/spec:implement`; `/spec:ralph-implement` returns `FAILED` on it by design. The gate question names the red Gap facts.
  - Test location: "tests/Paramore.Brighter.DynamoDB.Tests/Outbox/Generated/OutstandingCount/" and "tests/Paramore.Brighter.DynamoDB.V4.Tests/Outbox/Generated/OutstandingCount/"
  - Test file: the files the generator renders from the templates of T13.3, never written by hand
  - The generated tests should verify:
    - the (a) fact passes, retrying up to 10 s (A-7);
    - the (b) facts run with no mark and their result is recorded (the cell is Gap);
    - the C=1 and C=2 facts write their observed text to the output;
    - the (c) fact, with `StoreHeld` false and its own outbox built with `timeout: 0`, ends with a cancellation exception.
  - Implementation should:
    - implement the interface on both `tests/Paramore.Brighter.DynamoDB.Tests/Outbox/DynamoDBOutboxProvider.cs` and `tests/Paramore.Brighter.DynamoDB.V4.Tests/Outbox/DynamoDBOutboxProvider.cs`, in one commit, each creating and deleting a table per case;
    - set `OutstandingCountReadConsistencyTimeoutInMilliseconds` 10000 and `OutstandingCountBelowScanConcurrency` true in both `test-configuration.json` files;
    - build the generator (`dotnet build tools/Paramore.Brighter.Test.Generator`) and run it from `tests/Paramore.Brighter.DynamoDB.Tests/` and from `tests/Paramore.Brighter.DynamoDB.V4.Tests/` (`dotnet run --no-build --project ../../tools/Paramore.Brighter.Test.Generator`);
    - run `dotnet test tests/Paramore.Brighter.DynamoDB.Tests --filter "FullyQualifiedName~OutstandingCount"` and the same for `tests/Paramore.Brighter.DynamoDB.V4.Tests`;
    - record the observed results in the FR-14 table, DynamoDB row, including the C=1 and C=2 texts, which settle the "below `ScanConcurrency`" cell;
    - do not run the generated-tree audit in this task. It reports Missing entries until T13.13, which runs it.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before committing the generated files** *(fires in the `review-before` gear, which is the default)*

- [ ] **GENERATE: T13.13 Spanner and Firestore run the suite by hand against their emulators**
  - Generated tests. Do not use `/test-first`, and never write or edit a generated file by hand.
  - Deviation from the `GENERATE` shape: the Meets facts must pass, but a Gap fact may fail, and its failure is the result to record, not a defect to fix. It stays red until T13.17 sets its gap mark. Run this task with `/spec:implement`; `/spec:ralph-implement` returns `FAILED` on it by design. The gate question names the red Gap facts.
  - Test location: "tests/Paramore.Brighter.Gcp.Tests/Outbox/SpannerBinary/Generated/OutstandingCount/", ".../SpannerText/Generated/OutstandingCount/" and ".../Firestore/Generated/OutstandingCount/"
  - Test file: the files the generator renders from the templates of T13.3, never written by hand
  - The generated tests should verify:
    - SpannerBinary: (a) and (b) pass, and (c), with `StoreHeld` false, ends with a cancellation exception. The results go to the FR-14 table, Spanner row, noting "emulator, binary".
    - SpannerText: the same three results as SpannerBinary, recorded in the same row, noting "emulator, text".
    - Firestore: (a), (b) and (c) run with no mark, and each result is recorded whether it passes or fails, because all three cells are Gap. The results go to the FR-14 table, Firestore row, noting "emulator".
  - Implementation should:
    - implement the interface on `tests/Paramore.Brighter.Gcp.Tests/Outbox/SpannerBinary/SpannerBinaryOutboxProvider.cs`, `SpannerText/SpannerTextOutboxProvider.cs` and `Firestore/FirestoreOutboxProvider.cs`. Do all three before regenerating `tests/Paramore.Brighter.Gcp.Tests/`, because one regeneration renders all three;
    - give Firestore a collection with a unique name per case, whose documents are deleted afterwards;
    - when `FIRESTORE_EMULATOR_HOST` is set, give Firestore a `Configure` action on its `FirestoreConfiguration` that clears the builder's `Credential`, sets `Endpoint` to the variable's value and sets `ChannelCredentials` to `ChannelCredentials.Insecure`;
    - build the generator (`dotnet build tools/Paramore.Brighter.Test.Generator`) and run it from `tests/Paramore.Brighter.Gcp.Tests/` (`dotnet run --no-build --project ../../tools/Paramore.Brighter.Test.Generator`);
    - run the Spanner and Firestore facts by hand against their emulators, because `gcp-ci` leaves Spanner out and does not run for a pull request from a fork (`.github/workflows/ci.yml:791-793`, `:823`);
    - in the shell that runs `dotnet test`, start the two emulators and set the variables:
      - Spanner: run `docker compose -f docker-compose-spanner.yaml up -d --wait`, which also creates the instance and the database, and set `SPANNER_EMULATOR_HOST=localhost:9010` (`tests/Paramore.Brighter.Gcp.Tests/README.md:27`);
      - Firestore: the repository has no compose file for it, so run its emulator from the `gcr.io/google.com/cloudsdktool/cloud-sdk:emulators` image that `docker-compose-gcp.yaml:13` uses (`docker run --rm -d --name firestore-emulator -p 8080:8080 gcr.io/google.com/cloudsdktool/cloud-sdk:emulators gcloud emulators firestore start --host-port=0.0.0.0:8080`), and set `FIRESTORE_EMULATOR_HOST=localhost:8080`;
      - both: set `GOOGLE_CLOUD_PROJECT=brighter-tests`, which `GatewayFactory.GetProjectId()` reads;
      - if the Firestore emulator rejects the database `brighter-firestore-database`, stop and ask;
    - run `dotnet test tests/Paramore.Brighter.Gcp.Tests -f net10.0 --filter "FullyQualifiedName~OutstandingCount"`, the framework `gcp-ci` uses (`.github/workflows/ci.yml:823`);
    - record the observed results in the FR-14 table, Spanner and Firestore rows, with the notes each result above names;
    - stop the emulators: `docker stop firestore-emulator` and `docker compose -f docker-compose-spanner.yaml down`;
    - run the generated-tree audit: `When_auditing_the_generated_tree_should_find_no_missing_files` passes.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before committing the generated files** *(fires in the `review-before` gear, which is the default)*

- [ ] **PROJECT: T13.14 Record that the token reaches the client for each outbox whose store is not held (AC-17)**
  - For DynamoDB, DynamoDB V4, MongoDb, Spanner and Firestore, review the overall count's code path from the token to the store client.
  - Output: each outbox's FR-14 (c) cell in `specs/0038-outbox-stale-message-telemetry/requirements.md` gains the file and line that pass the token, or the line where it is dropped. For Firestore this is expected at `FirestoreOutbox.cs:1191-1246`.

- [ ] **PROJECT: T13.15 One issue for each FR-14 gap in columns (a) to (c) (Q-9)**
  - Draft one issue per Gap cell, including each "To verify" cell that T13.7 to T13.13 found to be a gap. Expected at least: Firestore (a), (b) and (c), and DynamoDB/V4 (b).
  - Each issue states the observed result, the AC-16 or AC-17 fact that shows it, and the gap mark that skips it until the fix.
  - Also draft the two issues that ADR 0085 :102 names for limits this PR only documents: the read volume of the DynamoDB and DynamoDB V4 counts (NFR-4 (b)), and the row loading of the relational and MongoDb overall counts (NFR-4 (c), Q-5). T14.9 links each known limitation to its issue.
  - Output: the issue texts, then, once each issue is posted, its number in its FR-14 cell (the Notes cell for the two NFR-4 issues).

- [ ] **TEST + IMPLEMENT: T13.16 The Skip audits scan only the messaging-gateway folders**
  - **USE COMMAND**: `/test-first a deferred Skip in a generated outbox suite is outside the artifacts the gateway Skip audits scan`
  - Test location: "tests/Paramore.Brighter.Test.Generator.Tests/ConformanceAudit/"
  - Test file: `When_an_outbox_suite_carries_a_deferred_skip_should_leave_it_out_of_the_gateway_audits.cs`
  - Test should verify:
    - over a temporary repo tree with `tests/Paramore.Brighter.X.Tests/Outbox/Generated/...` and `tests/Paramore.Brighter.X.Tests/MessagingGateway/Generated/...`, `GatewaySkipConventionAudit.EnumerateGatewayArtifacts` yields the gateway file and not the outbox file;
    - the gateway templates are still yielded.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - in `tests/Paramore.Brighter.Test.Generator.Tests/ConformanceAudit/GatewaySkipConventionAudit.cs`, restrict the generated walk (`:109-124`) to `Generated` folders under a `MessagingGateway` folder;
    - update the summary at `:26-37` and the method summary at `:90-94` to say so;
    - confirm `LedgerSkipCrossCheckAudit.cs:270` still walks the set through this method;
    - leave every existing audit test green.

- [ ] **PROJECT: T13.17 Set each gap mark, regenerate and run the audits (ADR 0085 risk :968)**
  - Starts once every Gap-cell issue from T13.15 has been posted and has a number.
  - For each Gap-cell issue from T13.15, in order:
    1. Set its mark in the project's `test-configuration.json`.
    2. Regenerate that project.
    3. Run its suite and confirm that the column's facts are skipped with `Deferred: #<n>`.
  - Set the Firestore marks before the merge, because `gcp-ci` runs Firestore on every push to master.
  - Then run the generated-tree audit, `GatewaySkipConventionAudit` and `LedgerSkipCrossCheckAudit` (`dotnet test tests/Paramore.Brighter.Test.Generator.Tests/Paramore.Brighter.Test.Generator.Tests.csproj -f net10.0`).
  - Output: the FR-14 table shows each mark beside its issue; all audits green.
  - Commit the marks and the regenerated files as `test:`, then the FR-14 table as `docs:`, then the tick.

- [ ] **DOC: T13.18 Describe the fourth outbox suite in the generator documentation**
  - Update `.agent_instructions/generated_tests.md` (the template tree near `:18-21`, the outbox template table near `:95-107`, and the provider pattern near `:326-343`) and `tools/Paramore.Brighter.Test.Generator/README.md`. Cover:
    - the OutstandingCount suite and its provider interface;
    - the two settings;
    - the three gap marks and their `Deferred: #<n>` form;
    - that the suite has no `SkipTest` filter.

## Phase 14: Documentation and follow-up issues (ADR 0085 step 14, ADR 0086 step 3)

*Depends on Phases 1 to 13. T14.9 needs the FR-14 results and the issues of Phase 13.*

- [ ] **PROJECT: T14.1 The per-topic follow-up issue for the relational family (ADR 0086 step 3)**
  - Draft the issue for MsSql, PostgreSql, MySql, Sqlite and Spanner. Spanner is included because `SpannerOutbox` derives from `RelationDatabaseOutbox`. The issue:
    - points at `TopicCountingOutboxBaseTests` as the base test the generated suite derives from;
    - says that the first family to land adds the topic-counting suite to `OutboxGenerator.SuitesFor` (`tools/Paramore.Brighter.Test.Generator/Generators/OutboxGenerator.cs:146`) with the `SupportsTopicCounting` setting (ADR 0086 :363), and asks the family to turn the setting on;
    - asks that review check the query against the contract's "transfers no message body" bullet (ADR 0086 risk :416);
    - says that a store class sets `Header.TimeStamp` relative to the store's clock, which keeps moving, so the base test's cases keep each message's age well away from every requested topic age (ADR 0086 :271).
  - Output: the issue text, then, once the issue is posted, its number in the FR-14 (d) cells of those outboxes.

- [ ] **PROJECT: T14.2 The per-topic follow-up issue for MongoDb**
  - As T14.1, and it also names the bag-topic filter of the overall count (`src/Paramore.Brighter.Outbox.MongoDb/MongoDbOutbox.cs:497-500`).
  - Output: the issue text, then, once the issue is posted, its number in the MongoDb FR-14 (d) cell.

- [ ] **PROJECT: T14.3 The per-topic follow-up issue for DynamoDB and DynamoDB V4**
  - As T14.1, and it also:
    - names the outstanding index keyed by topic (`src/Paramore.Brighter.Outbox.DynamoDB/DynamoDbConfiguration.cs:23-25`, and the V4 twin);
    - names the base test's `ReadConsistencyTimeout` for its eventually consistent index (ADR 0086 risk :417).
  - Output: the issue text, then, once the issue is posted, its number in the DynamoDB FR-14 (d) cell.

- [ ] **PROJECT: T14.4 The per-topic follow-up issue for Firestore**
  - As T14.1.
  - Output: the issue text, then, once the issue is posted, its number in the Firestore FR-14 (d) cell.

- [ ] **DOC: T14.5 XML documentation for every new public type and member (AC-26)**
  - Audit these against `.agent_instructions/documentation.md`: `OutboxStaleMessageReadings`, `StaleMessageCount`, `StaleMeasurementOutcome`, `OutboxStaleMessageMeter`, `TimedOutboxStaleMessageMonitor`, `TimedOutboxStaleMessageMonitorOptions`, `UseOutboxStaleMessageMonitor`, `IAmATopicCountingOutbox`, `Publication.StaleMessageAge` and the six constants.
  - The enums' and readings' remarks say they are public for Brighter's own use.
  - Output: the build has no new CS1591 warning for these types.

- [ ] **DOC: T14.6 Release notes entry**
  - **USE COMMAND**: `/spec:write_release_notes`
  - Add the feature to `release_notes.md`: the step, the three instruments, `Publication.StaleMessageAge`, `IAmATopicCountingOutbox` on `InMemoryOutbox`, and a pointer to the telemetry page.

- [ ] **DOC: T14.7 Telemetry page: what is measured**
  - Add a section to the telemetry page in the BrighterCommand/Docs repository covering:
    - what each count measures;
    - that the overall value is not the sum of the topic values;
    - that a value equal to C means "C or more", for each topic too;
    - the settings, `StaleMessageAge` among them, with their defaults (C 100, I 1 min, T 10 s) and maximums (365 d, 1000, 4,294,967,294 ms);
    - that a topic uses the smallest age of its publications;
    - that a topic with no publication in this process is in the overall count only;
    - how to read "no recent reading" for each count, through the `completed` total not rising for about I + T;
    - the example alert for each gauge, using the maximum of the gauge over a window;
    - which outboxes offer the count per topic (ADR 0086), and that the others report the overall count only;
    - how a tripped topic shows, and that topics are tripped only when the app registers an outbox circuit breaker;
    - the trace each measurement may start (Q-12).
  - Output: the page change and the Docs PR text; once that PR is opened, its link in `specs/0038-outbox-stale-message-telemetry/README.md`.

- [ ] **DOC: T14.8 Telemetry page: operational guidance**
  - In the same section:
    - how T interacts with the outboxes' own count timeouts. If T is longer than an outbox's own count timeout, the outbox fails first, and the due time is recorded as `failed` with the exception, not `abandoned`. To choose T: set it a little above the outbox's own timeout if `abandoned` should mean "the outbox ignored its own timeout", or below it if T should always be the bound. The page names each outbox's timeout:
      - the relational outboxes (MsSql, PostgreSql, MySql, Sqlite and Spanner): `RelationalDatabaseConfiguration.OutstandingCountTimeout` (`src/Paramore.Brighter/RelationalDatabaseConfiguration.cs:98`), 15 s by default. That is above the default T of 10 s, so T applies first;
      - DynamoDB: the operation timeout `DynamoDbConfiguration.Timeout` (`src/Paramore.Brighter.Outbox.DynamoDB/DynamoDbConfiguration.cs:47`), 500 ms by default from the constructor at `:71`. That is below the default T, so DynamoDB fails first;
      - DynamoDB V4: the same setting and default in `Paramore.Brighter.Outbox.DynamoDB.V4` (`src/Paramore.Brighter.Outbox.DynamoDB.V4/DynamoDbConfiguration.cs:40`, constructor at `:64`);
    - a T well below `HostOptions.ShutdownTimeout`, which every hosted service shares;
    - an explicit T when I is 10 s or less, since T must be below I;
    - combining instances that share a store with max, not sum, per topic for the topic gauge (A-6);
    - the clock-skew assumption: ages in minutes, skew in seconds (A-2);
    - the store cost of a short I;
    - the per-topic cost, which grows linearly with the number of topics (up to C entries each), how to choose C, and how a lower C or a larger T helps when the per-topic call keeps passing T;
    - choosing A and each `StaleMessageAge` above the sweeper's `MinimumMessageAge` plus `TimerInterval` (Q-13);
    - that a change to a publication's topic or age needs a restart;
    - a meter provider that shares the application's container (`Sdk.CreateMeterProviderBuilder()` is not supported);
    - that stopping clears both gauges.
  - Source for the timeout bullet, for the implementer and not for the page: ADR 0085 risk row :963, A-7 and A-9.

- [ ] **DOC: T14.9 Telemetry page: known limitations**
  - In the same section:
    - each outbox's FR-14 gaps, each linked to its T13.15 issue;
    - the four per-topic family issues from T14.1 to T14.4;
    - MongoDb's narrowing of the overall count to the bag's topic (FR-12);
    - that the relational outboxes and MongoDb load up to C whole rows per overall count, linked to its T13.15 issue;
    - DynamoDB's read volume, linked to its T13.15 issue, and its eventually consistent index;
    - the header-topic limitation (Q-16);
    - that `InMemoryOutbox` ignores `args`;
    - that a decorator hides the capability unless it implements it.
  - Output: AC-18's "listed under known limitations" holds for every Gap cell.

## Phase 15: Final verification

*Depends on every earlier phase.*

- [ ] **PROJECT: T15.1 Run every affected suite**
  - `tests/Paramore.Brighter.Core.Tests` on net9.0 and net10.0, including `Sweeping/`, `Observability/`, `CommandProcessors/` and the Outbox.Hosting tests, which live in Core.Tests (`Paramore.Brighter.Core.Tests.csproj:12`).
  - `tests/Paramore.Brighter.InMemory.Tests`.
  - The Base.Test-derived suites: `MSSQL.Tests`, `MySQL.Tests`, `PostgresSQL.Tests`, `Sqlite.Tests`, `MongoDb.Tests`, `DynamoDB.Tests`, `DynamoDB.V4.Tests`, and `Gcp.Tests` (Spanner and Firestore run by hand against their emulators).
  - `tests/Paramore.Brighter.Test.Generator.Tests`.
  - Build `src/Paramore.Brighter`, `src/Paramore.Brighter.Outbox.Hosting` and `src/Paramore.Brighter.Extensions.Diagnostics` for every target (netstandard2.0, net8.0, net9.0, net10.0) with no new warnings. The netstandard2.0 branches are built here and reviewed in T9.21. No test project runs them.
  - Output: pass counts per project in the PR description.

- [ ] **PROJECT: T15.2 Generated-tree audit**
  - Run `dotnet test tests/Paramore.Brighter.Test.Generator.Tests/Paramore.Brighter.Test.Generator.Tests.csproj -f net10.0`.
  - Output: no Missing and no Orphan file; the Skip audits are green.

- [ ] **PROJECT: T15.3 Diff review for public API and dependencies (AC-22, AC-24 second clause; ADR 0085 risk :967)**
  - Check the PR diff:
    - no member changed on `IAmABrighterMessagingMeter`, `IAmABrighterDbMeter`, `IAmABrighterSweeperMeter` or `IAmABrighterOutboxMeter`;
    - `IAmAnOutbox`, `IAmAnOutboxSync` and `IAmAnOutboxAsync` unchanged;
    - `Publication` gains only `StaleMessageAge`;
    - no member added to `IAmProducersConfiguration`, `IAmABrighterTracer` or `IAmAnOutboxProducerMediator`;
    - every other public change is a new type, an optional capability or an optional parameter;
    - `OutboxStaleMessageMeter` is sealed and implements no role interface;
    - everything on the "stay unchanged" list at ADR 0085 :685-698 is untouched.
  - Grep the new measurement and recording code (readings, meter, monitor, options) for `OpenTelemetry`. Only `BrighterMetricsBuilderExtensions` may reference it (NFR-5). Note that the core assembly already references the OpenTelemetry package (`src/Paramore.Brighter/Paramore.Brighter.csproj:18`), so the check is by namespace, not by package.
  - Output: a "Reviewed:" sub-bullet under this task.

- [ ] **PROJECT: T15.4 Fake time and listener check (AC-25)**
  - Confirm that the tests for AC-1 to AC-15, AC-19 to AC-21, AC-23, AC-24, AC-27 to AC-33 and AC-35 to AC-40 drive measurement time only through `FakeTimeProvider`, and observe values through a `MeterListener` where they read values.
  - Confirm that every wall-clock wait (the AC-11, AC-20, AC-36 and AC-37 guards, and the synchronisation wait) only detects hangs or waits for work already due.
  - Output: a "Reviewed:" sub-bullet listing any exception found.

- [ ] **PROJECT: T15.5 FR-14 completeness (AC-18)**
  - Confirm that every Gap cell, including each "To verify" cell found to be a gap and column (d) of every outbox but `InMemoryOutbox`, is closed with its case passing, or linked to an open issue and listed under known limitations (T14.9).
  - Confirm one column (d) issue per family.
  - Confirm that the read-volume and row-loading issues from T13.15 exist and are linked from T14.9.
  - Output: the FR-14 table has no unresolved "To verify" cell.

- [ ] **PROJECT: T15.6 AC-to-task cross-check**
  - Walk AC-1 to AC-40, and every ADR 0085 and ADR 0086 step, against the ticked tasks, using the coverage cross-reference at the end of this list.
  - Output: every AC maps to at least one ticked task; any gap becomes a new task, reviewed again with `/spec:review tasks`, before the merge.

- [ ] **PROJECT: T15.7 The Docs PR is open and linked before this PR leaves draft (AC-26, NFR-7)**
  - Confirm that the Docs PR from T14.7 to T14.9 is open, covers every NFR-7 item and is linked from this PR's description before this PR leaves draft.
  - Output: a "Reviewed:" sub-bullet under this task with the Docs PR link.

- [ ] **DOC: T15.8 Close the spec**
  - Tick "Implementation" in `specs/0038-outbox-stale-message-telemetry/README.md`, with the PR number.

---

## Coverage cross-reference

### Functional requirements

| Requirement | Tasks |
|---|---|
| FR-1 | T1.2, T4.3, T12.1, T12.2, T12.4 |
| FR-2 | T5.1 to T5.8, T6.2, T6.5 to T6.7, T8.12, T8.23, T12.4 |
| FR-3 | T8.2, T8.15, T8.19, T13.1, T13.7 to T13.13 |
| FR-3.1 | T6.9, T8.7, T8.9 to T8.12, T8.16, T8.18 |
| FR-4 | T2.2, T8.3, T8.17, T8.21, T13.2, T13.7 to T13.13 |
| FR-5 | T3.1, T3.5, T4.2, T8.1, T8.2, T8.15 |
| FR-6 | T3.1, T3.2, T3.6 to T3.8, T3.10, T8.13, T9.6, T9.10 to T9.14 |
| FR-7 | T8.1, T8.4, T8.8, T8.25 |
| FR-8 | T9.6, T9.13, T9.21 |
| FR-9 | T9.1 to T9.9, T9.13, T9.19 |
| FR-10 | T9.10 to T9.18 |
| FR-11 | T8.2 (no sweeper), T11.1 |
| FR-12 | T2.4, T8.5 to T8.7, T8.14 |
| FR-12.1 | T3.2, T6.8 to T6.10, T8.13 |
| FR-13 | T10.1 to T10.10 |
| FR-14 | T2.2 to T2.11 (column (d), `InMemoryOutbox`), T13.1 to T13.14 |
| FR-14.1 | T13.15, T13.17, T14.1 to T14.4, T14.9, T15.5 |
| FR-15 | T3.5, T8.16, T8.18, T8.20 |

### Non-functional requirements

| Requirement | Tasks |
|---|---|
| NFR-1 | T11.2, T11.3, T11.4 |
| NFR-2 | T11.5 |
| NFR-3 | T1.1, T1.2, T2.1, T15.3 |
| NFR-4 | T8.14, T8.17, T8.21, T2.2, T13.15, T14.8, T14.9, T15.5 |
| NFR-5 | T8.1, T12.3, T15.3 |
| NFR-6 | every Phase 8 to 12 task (determinism rules), T15.4 |
| NFR-7 | T14.5 to T14.9, T15.7 |

### Acceptance criteria

| AC | Tasks |
|---|---|
| AC-1 | T12.1, T12.2 |
| AC-2 | T5.2, T5.3, T5.4, T5.5 |
| AC-3 | T5.1 (defaults), T5.6 (bounds at registration), T8.23 (app starts; measurement at the maximums), T14.7 (documented defaults) |
| AC-4 | T8.2 |
| AC-5 | T8.3 |
| AC-6 | T8.1 |
| AC-7 | T9.11, T9.12 |
| AC-8 | T8.1 (coalescing code), T8.4, T8.8, T8.25 (coalescing by review) |
| AC-9 | T9.6 |
| AC-10 | T9.1, T9.2, T9.3 |
| AC-11 | T9.7 (first half), T10.5 (second half) |
| AC-12 | T9.10 |
| AC-13 | T8.2, T11.1 |
| AC-14 | T8.5, T8.6, T8.7 |
| AC-15 | T10.1, T10.2, T10.3 |
| AC-16 | T13.1, T13.2, T13.3 to T13.5, T13.7 to T13.13 |
| AC-17 | T13.1 (base), T13.3, T13.7 to T13.13, T13.14 |
| AC-18 | T13.15, T13.17, T14.1 to T14.4, T14.9, T15.5 |
| AC-19 | T8.20 |
| AC-20 | T11.2, T11.3, T11.4 |
| AC-21 | T11.5 |
| AC-22 (NFR-3 diff review) | T15.3 |
| AC-23 | T8.14 |
| AC-24 | T12.3, T15.3 |
| AC-25 (NFR-6 FakeTimeProvider/MeterListener check) | T15.4 |
| AC-26 (NFR-7 docs) | T14.5, T14.6, T14.7, T14.8, T14.9, T15.7 |
| AC-27 | T8.15 |
| AC-28 | T8.9, T8.10 |
| AC-29 | T6.8, T6.9, T6.10, T8.13 |
| AC-30 | T8.16 |
| AC-31 | T8.17 |
| AC-32 | T6.5, T6.6, T6.7, T8.12 |
| AC-33 | T9.13, T9.14, T9.15, T8.21, T8.22 |
| AC-34 | T2.2, T2.3, T2.4 |
| AC-35 | T12.4 |
| AC-36 | T9.8 |
| AC-37 | T9.9 |
| AC-38 | T8.18 |
| AC-39 | T8.11 |
| AC-40 | T8.19 |

### ADR 0085 steps and sub-bullets

| Step / sub-bullet | Tasks |
|---|---|
| 1 Six constants | T1.1 |
| 2 `Publication.StaleMessageAge`, 365-day bound in prose | T1.2 |
| 3 Readings, enums, meter, modelled on `PublishConfirmationMeter`, tag with destination | T3.1, T3.5 |
| 3 case: no readings, no instruments | T3.4 |
| 3 case: not started, no series | T3.3 |
| 3 case: started with topics, eight zero series | T3.1 |
| 3 case: started without topics, four overall series | T3.2 |
| 3 case: complete, each gauge reports | T3.5 |
| 3 case: Fail(PerTopic) | T3.6 |
| 3 case: Skip(Overall, 3) | T3.7 |
| 3 case: Stop | T3.8 |
| 4 TryAddSingleton beside :42; resolve in deferred Configure | T4.2 |
| 4 extract the Configure block into a named method | T4.1 |
| 4 with readings report; without none; becomes the fixture | T4.2, T4.3 |
| 5 options and validation; extension | T5.1 to T5.6, T5.8 |
| 5 TryAddSingleton as UseOutboxArchiver | T5.7 |
| 5 default C as its own value | T5.1 |
| 5 AC-3 last clause moved to step 8 | T8.23 |
| 6 AddHostedService; one registration after two calls | T6.1 |
| 6 scope, GetService, dispose before return | T6.3, T6.4 |
| 6 ConfigurationException naming AddProducers | T6.3 |
| 6 transaction type via GetTransactionTypeFromTransactionProvider | T7.1 |
| 6 bag read as the mediator; empty when none | T8.5, T8.6 |
| 6 publications read once; AC-32; AC-28; AC-39 | T6.5 to T6.7, T8.9, T8.10, T8.11, T8.12 |
| 6 capability check and FR-12.1 entry (AC-29) | T6.8, T6.9, T6.10 |
| 6 readings.Start | T6.11 |
| (options re-validated in the constructor, ADR options section) | T6.2 |
| 7 sync-only check, neither interface, no count call | T7.1, T7.2 |
| 8 Task.Run under SuppressFlow | T8.1, T8.24 |
| 8 elapsed from timestamp, whole-tick grid | T8.1, T8.25 |
| 8 CreateTimer helper, clock recheck | T8.1 |
| 8 stop TCS, yield after WhenAny | T8.1, T10.1 |
| 8 each count its own Task.Run, overall first, both before the wait | T8.1, T8.7, T9.8 |
| 8 cap overall at C; drop unrequested; cap each topic | T8.21, T8.22 |
| 8 review AC-8 merging clause | T8.25 |
| 8 AC-14 arguments; AC-23 call count | T8.5 to T8.7, T8.14 |
| 8 end-to-end AC-27, AC-30, AC-31, AC-38, AC-40 | T8.15 to T8.19 |
| 8 AC-3 last clause | T8.23 |
| 8 (heading) AC-4, AC-5, AC-6, AC-8, AC-19 | T8.2, T8.3, T8.1, T8.4, T8.8, T8.20 |
| 9 one start timestamp, deadline = start + T | T9.1 |
| 9 WhenAny with 6d first, then 6a to 6c; abandon at deadline | T9.1, T10.1 |
| 9 test: released just after the deadline is abandoned | T9.4 |
| 9 test: blocking logger, completed before the deadline | T9.5 |
| 9 invalid per-topic result | T9.15 (and T9.16 for the overall count) |
| 9 record then cancel; CancelAsync or queued Cancel; observe | T9.1, T9.19 |
| 9 task records end timestamp; continuation observes | T9.4, T9.20 |
| 9 row 4 skips; AC-9 and AC-33 still-running case; review the split | T9.6, T9.13, T9.21 |
| 9 only the loop cancels or disposes a source | T9.6, T9.21 |
| 9 [LoggerMessage] entries naming the count, written at once | T9.1, T9.5, T9.10, T9.14 |
| 9 catch loop exceptions at Error; throwing-logger test | T9.18 |
| 9 blocking cancellation callback test | T9.19 |
| 10 StopAsync, Dispose, stop handling, TimeProvider and host token | T10.1, T10.8, T10.9, T10.10 |
| 10 row 5 check | T10.1, T10.11 |
| 10 both in progress: cancel both, no outcome, no failure log, clear gauges | T10.3, T10.4 |
| 10 stop while waiting with an abandoned call | T10.6 |
| 10 stop while a callback blocks | T10.7 |
| 10 no call after the stop, even 10 min later | T10.1, T10.3 |
| 11 hung sweep; blocked check; posting-path comparison | T11.1 to T11.5 |
| 12 AC-1 with and without; AC-24; AC-35 | T12.1 to T12.4 |
| 13 generic tests, setup and InMemory class first | T13.1, T13.2 |
| 13 suite in the generator | T13.3 |
| 13 two settings, three gap marks | T13.4, T13.5, T13.6 |
| 13 provider interface on every outbox, settings, regenerate, run with no mark | T13.7 to T13.13 |
| 13 Spanner and Firestore by hand | T13.13 |
| 13 record FR-14 with the token-reaches-client review | T13.7 to T13.14 |
| 13 one issue per gap; known limitations | T13.15, T14.9 |
| 13 restrict the Skip-audit scan, test first | T13.16 |
| 13 set the gap marks | T13.17 |
| 13 (touched-types table) generated_tests.md and the README | T13.18 |
| 14 XML docs | T14.5 |
| 14 release notes | T14.6 |
| 14 example alert per gauge | T14.7 |
| 14 not the sum | T14.7 |
| 14 smallest age | T14.7 |
| 14 header topic (Q-16) | T14.9 |
| 14 per-topic cost and C | T14.8 |
| 14 which outboxes offer the per-topic count | T14.7 |
| 14 combine with max per topic | T14.8 |
| 14 MongoDb narrowing | T14.9 |
| 14 A and StaleMessageAge with a sweeper (Q-13) | T14.8 |
| 14 trace per measurement (Q-12) | T14.7 |
| 14 T well below ShutdownTimeout | T14.8 |
| 14 explicit T when I is 10 s or less | T14.8 |
| 14 shared-container meter provider | T14.8 |
| 14 T versus the outboxes' own count timeouts (ADR 0085 risk row :963) | T14.8 |

### ADR 0086 steps and sub-bullets

| Step / sub-bullet | Tasks |
|---|---|
| 1 interface beside `IAmACausationTrackingOutbox`, contract in `///` | T2.1 |
| 2 base test and derived InMemory class; InMemory implementation (:83, :633-634, :646-655); members with `BeforeCount` and the in-memory clock (:248-271) | T2.2 |
| 2 case: AC-34 with C 100 and 3 (and 30 s gives 7) | T2.2 |
| 2 case: exactly the requested topics | T2.3 |
| 2 case: args do not narrow, args unchanged | T2.4 |
| 2 case: header topic, not ProducerTopic | T2.5 |
| 2 case: empty map, empty result | T2.6 |
| 2 case: null map, ArgumentNullException | T2.7 |
| 2 case: RoutingKey.Empty, ArgumentException; null topic by review | T2.8 |
| 2 case: ages 0, -1 s, 365 d + 1 tick throw; 365 d accepted | T2.9 |
| 2 case: ceiling 0, ArgumentOutOfRangeException | T2.10 |
| 2 case: concurrency through the outbox itself | T2.11 |
| 2 note: capability cancellation belongs to the follow-ups | not tasked (out of scope, OOS-15) |
| 3 one issue per family, pointing at the base test | T14.1 to T14.4 |
| 3 MongoDb names :497-500; DynamoDB names DynamoDbConfiguration.cs:23-25 | T14.2, T14.3 |
| 3 first family adds the suite to SuitesFor with SupportsTopicCounting | T14.1 to T14.4 |
| 3 each family's limitation in the documentation | T14.9 |

### Design review items left for the task breakdown

These are the items that `specs/0038-outbox-stale-message-telemetry/review-design.md` :18-25 leaves open for the task breakdown, and pass 14's finding 2 (:36-38).

| Item | Tasks |
|---|---|
| Pass 11, finding 1: sync point of the blocking-logger test | T9.5, determinism rule 3 |
| Pass 11, finding 2: end recorded in a `finally` | T9.4, T9.21 |
| Pass 11, finding 3: wait for a call's end before the next due time | determinism rule 3, T9.2, T9.3, T9.6, T9.13, T9.20 |
| Pass 11, finding 4: exact age in ADR 0086 | T2.2, T14.1 |
| Pass 13, finding 5: the observation facts stay | T13.5, T13.12 |
| Pass 13, finding 8: one test class per generated file | T13.3, T13.5 |
| Pass 14, finding 2: the Firestore emulator and its variables | T13.13 |

### ADR 0085 risks (:951-968, 18 rows)

| Row | Risk | Tasks |
|---|---|---|
| :951 | per-topic call blocks its thread | T9.9 |
| :952 | overall count blocks its thread | T9.8 |
| :953 | publication changed after start | T8.11, T14.8 |
| :954 | sequential host stop, T delays later services | T10.10, T14.8 |
| :955 | stop arrives at a due time | T10.1, T10.11 |
| :956 | loop's own code throws; StopAsync never throws | T9.18, T10.8 |
| :957 | source disposed while a cancel is queued | T9.21 |
| :958 | cancellation callback blocks | T9.19, T10.7 |
| :959 | OCE after the loop cancelled | T9.1, T9.3, T10.1 |
| :960 | sum across instances | T14.8 |
| :961 | clock skew | T14.8 |
| :962 | very small I | T8.14, T9.6, T14.8 |
| :963 | T longer than the outbox's own timeout | T9.17, T14.8 |
| :964 | outbox returns more than C | T8.21 |
| :965 | result at the deadline | T9.4, T9.5 |
| :966 | unobserved task exception | T9.20 |
| :967 | later ADR moves the instruments | T15.3 |
| :968 | gap mark outlives its issue | T13.15, T13.17, T14.9 |

### ADR 0086 risks (:413-418, 6 rows)

| Row | Risk | Tasks |
|---|---|---|
| :413 | third-party capability narrows, misses a topic or exceeds C | T2.1, T2.3, T2.4, T8.21, T8.22, T9.15 |
| :414 | many topics pass T | T9.13, T14.8 |
| :415 | mapper changes the header topic | T2.5, T8.18, T14.9 |
| :416 | follow-up reads bodies | T14.1 to T14.4 (review bullet) |
| :417 | eventually consistent follow-up | T2.2 (`ReadConsistencyTimeout`), T14.3 |
| :418 | capability beside posting-path calls | T2.11 |

### Scope-creep flags

Each adds a test the ADR does not name, for behaviour the ADR already requires. None adds production scope beyond that contract:
- T3.9 (invalid readings writes) and T3.10 (Abandon clears): the readings contract at ADR 0085 :480-520, but not among the step 3 cases.
- T6.4 (scope disposed): step 6 requires it but names no test.
- T8.24 (SuppressFlow ambient state): step 8 requires SuppressFlow but names no test.
- T9.16 (negative overall count) and T9.17 (the outbox's own OCE): rows 6b and :277, with no test named.
- T9.20 (unobserved exception): risk row :966, with no test named.
- T10.8 to T10.10 (StopAsync before start, Dispose, host token): the *Stopping* section, with no test named.
