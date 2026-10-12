# Requirements

> **Note**: This document captures user requirements and needs. Technical design decisions and implementation details should be documented in an Architecture Decision Record (ADR) in `docs/adr/`.

**Linked Issue**: #4489

**Related**: issue #4554 (outstanding-message check cost), issue #4584 (outstanding-message check with no timeout and a shared semaphore), issue #4573 (hung sweeper detection), PR #4484 (outbox metrics, ADR `0084-outbox-metrics`), ADR `0081-timed-outbox-sweeper-health-metrics`, ADR `0063-failed-delivery-context`, ADR `0022-generate-metrics-from-traces`, ADR `0010-brighter-semantic-conventions`.

Code locations refer to master at c98bd40ea.

## Problem Statement

**As an operator** of an application that publishes through Brighter's outbox, **I would like** a metric that tells me how many messages have stayed in the outbox without being dispatched for longer than an age I choose, for the in-memory outbox and for each database outbox that ships with Brighter, whether or not the outbox sweeper runs, **so that** a message that was never sent shows up on my dashboards and alerts instead of waiting in the outbox where nobody looks.

### Why such a message is invisible today

- `CommandProcessor.PostAsync` deposits the message and then clears it. When the send fails, `ExecuteWithResiliencePipelineAsync` (`OutboxProducerMediator.cs:1453`) logs the exception and returns false (`:1487-1492`). The message stays undispatched and nothing reaches the caller. ADR `0063-failed-delivery-context` applies the same rule (stay undispatched, let a sweeper retry) to failed publish confirmations on Kafka and RabbitMQ.
- The sweeper is opt-in (`UseOutboxSweeper`). Without it, nothing sends the message again. With the default `InMemoryOutbox`, the message is also lost when the process stops.
- Nothing reports how many messages are waiting:
  - the outstanding-message check counts only when `MaxOutStandingMessages` is set (`OutboxProducerMediator.cs:811-814`), and only to enforce the limit;
  - the sweeper metrics of ADR `0081-timed-outbox-sweeper-health-metrics` report the sweeper's own health, and exist only when a sweeper runs;
  - the outbox metrics of ADR `0084-outbox-metrics` count messages added and cleared; their difference has no age and restarts with the process, so it cannot tell a message about to be sent from one that never will be;
  - outbox spans are usually sampled out in production.

### Scope

The maintainer's item on issue #4489 is: "Telemetry on the call that warns about Outbox items over a configured age. It should work across im-memory and out-of-process outboxes." This spec covers that item. What "on the call" and "warns" mean is Q-2. The second item on the issue, a channel that notifies each failed `Clear(Async)`, is out of scope (OOS-1).

### Changes from the proposal on issue #4489

The proposal posted on the issue had four parts:
- a `StaleMessageAge` on `ProducersConfiguration`;
- a timer in the outbox producer mediator, running about twice per age;
- a root span per count;
- a gauge derived from that span by the span-to-metrics processor (ADR `0022-generate-metrics-from-traces`).

This spec changes two things:
- **The value is recorded directly on the Brighter meter (C-1).** ADR `0081-timed-outbox-sweeper-health-metrics` rejected span-derived metrics for health signals because sampling drops the spans exactly when they matter. Issue #4573 points the sweeper's new level instrument at an existing precedent, `PublishConfirmationMeter`, an `ObservableUpDownCounter` registered by `AddBrighterInstrumentation`, and forbids a new member on `IAmABrighterSweeperMeter`. NFR-3 extends that rule to the other meter roles.
- **Where measurement runs and how it is configured are left open (Q-1, Q-4).** The requirements state only what an operator observes, so either a mediator timer or a hosted service can meet them. The interval is a setting of its own, and its default is for the ADR.

Three smaller parts of the proposal change too: the new `InstrumentationOptions.Outbox` flag is dropped, because a directly recorded instrument does not go through span options; the fixed `maxCount` of 100 becomes the configurable ceiling C; and the fixed 30-second give-up becomes the configurable timeout T, which must be less than I (FR-2).

## Proposed Solution

1. The app turns the feature on with one explicit configuration step that names a **stale age**. It may also set a **ceiling**, a **measurement interval** and a **measurement timeout**. Each has a documented default.
2. While measurement runs, Brighter counts the outbox's undispatched messages older than the stale age once per interval, up to the ceiling.
3. Each completed count is recorded on an instrument on the `Paramore.Brighter` meter. An app that already uses `AddBrighterInstrumentation` exports it with no further setup.
4. A count that fails or runs past its timeout records no value. An operator can tell "no recent reading" apart from "zero stale messages" through metrics alone.
5. Measurement never delays a post, never waits for a sweep, and does not change the outstanding-message limit.

Without the configuration step, nothing new runs and the outbox receives no new queries.

## Requirements

### Terms

| Term | Meaning |
|---|---|
| **Outbox** | The object registered as `IAmAnOutbox`. `AddProducers` registers an `InMemoryOutbox` when the app supplies none (`ServiceCollectionExtensions.cs:995`). This spec measures one outbox per process (Q-8). |
| **Outbox in scope** | `InMemoryOutbox`; MsSql, PostgreSql, MySql, Sqlite, Spanner; DynamoDB and DynamoDB V4; MongoDb; Firestore. |
| **Dispatched** | The outbox has recorded the message as dispatched (`MarkDispatched[Async]`). |
| **Reference time** | The timestamp each outbox already compares with `dispatchedSince` (A-1). |
| **Age** | The outbox's current time minus the message's reference time. The outbox computes it on its own clock (A-2) when it evaluates the count. |
| **Stale age (A)** | The age the app configures. |
| **Stale message** | A message that is not dispatched and whose age is greater than A. |
| **Stale count (S)** | The number of stale messages when the outbox evaluates the count. |
| **Ceiling (C)** | The largest value one measurement counts to. |
| **Reported value (V)** | The value a completed measurement records. V equal to C means "C or more". |
| **Measurement** | One count. It begins when measurement calls the outbox. It *completes* when the call returns a count, *fails* when the call throws, and is *abandoned* when T elapses first. It is *in progress* until the call returns or throws, including after it has been abandoned. |
| **Measurement runs** | The period from when measurement starts to when it stops. Both are tied to the app's lifetime; what starts and stops it is Q-1. |
| **Due time** | Start time + k × I, for k = 1, 2, 3 and so on. |
| **Measurement interval (I)**, **measurement timeout (T)** | The time between due times, and the longest a measurement waits for the outbox. |
| **Posting path** | `Post[Async]`, `DepositPost[Async]` and `ClearOutbox[Async]`, and the mediator work they cause, including the outstanding-message check. |
| **Outstanding-message check** | `OutboxProducerMediator.OutstandingMessagesCheck` (`OutboxProducerMediator.cs:1372`). |

### Functional Requirements

**FR-1 — The feature is off by default and turned on by one explicit step.**
The step names A; its form is Q-4. When the feature is not enabled, the outbox receives no count call from it and its instrument records nothing. No existing call enables it, including `AddProducers`, `UseOutboxSweeper`, `UseOutboxArchiver` and `AddBrighterInstrumentation`.

**FR-2 — The settings are validated during configuration.**

| Setting | Required | Valid values | Default |
|---|---|---|---|
| A | Yes | Greater than `TimeSpan.Zero`, and at most a maximum the ADR fixes. The maximum must be small enough that the outbox's `now - A` cannot overflow (365 days would do). | None |
| C | No | 1 up to a maximum the ADR fixes, below `int.MaxValue`. The relational outboxes and MongoDb load up to C rows (NFR-4). | ADR |
| I | No | Greater than `TimeSpan.Zero`, and at most 4,294,967,294 ms (about 49.7 days), the largest period .NET timers accept. | ADR |
| T | No | Greater than `TimeSpan.Zero` and less than I. | ADR, less than the default I |

An invalid value throws a `ConfigurationException` that names the setting and the value. It is thrown when the configuration step runs or, at the latest, when measurement starts, and never during a measurement. No measurement then runs. The values do not change while measurement runs.

> *Examples.* Each of these throws: A of zero; A of `TimeSpan.MaxValue`; C of 0; C of `int.MaxValue`; I of 50 days; T equal to I (1 minute each); T of 2 minutes with I of 1 minute. A of 1 ms, C of 1, I of 1 minute and T of 59 seconds together are accepted.

**FR-3 — A measurement counts stale messages and only those.**
Dispatched messages are excluded at any age, and undispatched messages younger than A are excluded. At an age of exactly A the outboxes already differ (`InMemoryOutbox` and DynamoDB include, the relational outboxes and MongoDb exclude). This spec does not align them and does not test that boundary (OOS-12). For an out-of-process outbox, the count includes messages written by other instances that share the store (A-6), and excludes uncommitted ones (A-5). MongoDb narrows it to the bag's topic when one is set (FR-12).

> *Example.* A is 5 minutes and the count runs at 12:06:00. m1 (reference time 12:00:00, undispatched) is counted. m2 (12:00:00, dispatched at 12:00:01) is not. m3 (12:04:00, undispatched, age 2 minutes) is not. V is 1.

**FR-4 — The count stops at the ceiling.**
For an outbox that meets FR-14(b), V is `min(S, C)`. A measurement asks the outbox for at most C. This is stricter than the interface's `maxCount` wording, "the maximum number of messages to scan" (`IAmAnOutboxSync.cs:136`). FR-14 records the outboxes that do not meet it.

> *Example.* With C of 3: S of 0, 2, 3 and 50 give V of 0, 2, 3 and 3.

**FR-5 — Each completed measurement is recorded on an instrument on the Brighter meter.**
- **Meter and name.** The meter is `BrighterSemanticConventions.MeterName` (`Paramore.Brighter`). The working name is `paramore.brighter.outbox.stale_messages`, following ADR `0010-brighter-semantic-conventions`.
- **Unit and value.** The unit is `{message}`. The value is a whole number, and a reader gets the latest V, not a sum. A V of 0 is recorded as 0.
- **Before the first completed measurement**, no value is recorded.
- **Instrument type and owning role** are for the ADR (Q-6). The precedent is `PublishConfirmationMeter` (`PublishConfirmationMeter.cs:44-51`), an `ObservableUpDownCounter` registered by `AddBrighterInstrumentation`.

**FR-6 — "No recent reading" can be told apart from "zero stale messages" through metrics alone.**
The instrument never reports a value that no completed measurement produced, and never reports 0 in place of a missing reading. A due time may produce no completed measurement because it fails, is abandoned or is skipped. No later than T after such a due time, an operator who reads only metrics can see that no measurement has completed since the last success, or since measurement started when none has succeeded yet, and can tell that state apart from the feature being disabled. For a coalesced due time (FR-7), the bound runs from when the scheduler runs again. If a measurement completes before the bound expires, the signal need not show "no recent reading" for that due time. Once a later measurement completes, an operator reading only metrics can see that a measurement has completed since the last failed, abandoned or skipped due time; with an outcome counter, that means the completed count has increased since that outcome was counted. The mechanism is for the ADR. Options:
- an outcome counter (completed, failed, abandoned, skipped), as ADR `0081-timed-outbox-sweeper-health-metrics` counts sweeps, which covers the case before any success on its own;
- not carrying the stale value forward past one interval, or a last-success timestamp; either needs a companion signal, such as a counter or a timestamp seeded with the start time, to tell "no success yet" apart from "disabled".

**FR-7 — Measurements follow a fixed schedule.**
- A measurement starts at each due time unless FR-8 skips it, the third bullet coalesces it, or Q-8 is answered with an election that leaves this instance out.
- How often the metrics pipeline collects does not change how often the outbox is queried (Q-11).
- Due times missed when nothing is in progress, for example through process suspension or thread-pool starvation, are coalesced into at most one measurement, which starts as soon as the scheduler runs again (subject to FR-8), not caught up one by one. Later due times stay on the start + k × I grid. Of N coalesced due times, the N−1 that get no measurement count as skipped for FR-6 and FR-10, and FR-10 logs them as one entry that carries the number skipped. Due times left out by a Q-8 election are not skipped: they are neither shown under FR-6 nor logged under FR-10, and the ADR defines what such an instance reports.

> *Example.* I is 1 minute and measurement starts at 12:00:00. At 12:03:30, three measurements have started, at 12:01, 12:02 and 12:03, however often metrics are collected.

**FR-8 — At most one count from measurement is against the store at a time.**
From measurement, at most one outbox call is in progress at any time, including an abandoned call that has not returned. A due time that arrives while one is in progress makes no outbox call and records no value. The next due time does not change.

**FR-9 — A measurement never waits for the store longer than T.**
T after the call starts, the measurement does three things:
- it stops waiting;
- it cancels the token it passed;
- it records no value.

A result that arrives later is never recorded. This holds when the outbox ignores the token. It also holds when the outbox's asynchronous count blocks the calling thread before it returns a task: `InMemoryOutbox` computes synchronously and then completes the task (`InMemoryOutbox.cs:647-656`), and Sqlite's provider runs async methods synchronously (A-8). The thread such a count occupies is never one the posting path waits for (NFR-1).

**FR-10 — A failed measurement is visible and does not stop later ones.**
If the outbox call throws, no value is recorded and the next due measurement runs as normal. No exception thrown during a measurement reaches the app or ends the process (a `ConfigurationException` from FR-2 is not thrown during a measurement). Failures, abandonments and skipped due times show in metrics through FR-6's "no recent reading" signal; telling the three apart in metrics is not required, though the ADR may choose a signal that does. Each is also logged with the outbox type, and failures carry the exception. Log levels and wording are for the ADR. A cancellation caused by stopping (FR-13) is not logged as a failure.

**FR-11 — Measurement does not depend on the outbox sweeper.**
Measurements run with no sweeper registered. With `UseOutboxSweeper`, due measurements run on schedule while a sweep is in progress, including a sweep that never ends. Measurement neither obtains nor waits for the sweeper's lock (resource `OutboxSweeper`, `TimedOutboxSweeper.cs:62`).

**FR-12 — Measurement passes the same `args` as the outstanding-message check.**
Measurement calls `IAmAnOutboxAsync<Message, TTransaction>.GetOutstandingMessageCountAsync`, or an asynchronous capability added under Q-5. It passes:
- `dispatchedSince` A;
- `maxCount` C;
- `args` equal to what the check passes: the producers' `OutBoxBag` (`ProducersConfiguration.cs:236`), passed at `OutboxProducerMediator.cs:1390` and `:1405`, or the empty dictionary created at `:197` when none is configured;
- `requestContext` null;
- a cancellable token.

It never calls the synchronous count. What happens with an outbox that implements only `IAmAnOutboxSync` is Q-3.

Passing the bag matters because an outbox may read it: MongoDb narrows its count to `args["Topic"]` when the bag sets one (`MongoDbOutbox.cs:497-500`), so an app whose bag sets a topic gets that topic's stale messages only. FR-14 records this, and the documentation states it (NFR-7).

**FR-13 — Stopping measurement is prompt.**
When measurement stops:
- no measurement starts after that;
- the token of an in-progress call is cancelled when the stop is requested;
- stopping returns no later than T after the request, even if the outbox ignores the token or blocks its thread (FR-9);
- when the outbox honours the token, stopping returns as soon as the cancelled call ends, without waiting for T.

If Q-1's hosted-service proposal is taken, the ADR states how T relates to `HostOptions.ShutdownTimeout`.

> *Example.* T is 10 seconds. A count started at 12:01:00 ignores cancellation. A stop is requested at 12:01:05. The token is cancelled at 12:01:05, and stopping has returned by 12:01:15.

**FR-14 — Each outbox in scope meets the accuracy and cancellation requirements, or its gap is recorded.**
- **(a) Age.** The count includes only stale messages (FR-3).
- **(b) Ceiling.** With no concurrent writers, the count returns exactly `min(S, C)`.
- **(c) Cancellation.** If the token is cancelled before the store answers, the call ends with `OperationCanceledException` or the store client's own cancellation exception, without waiting for the store to answer. The exact bound for each outbox is Q-10. AC-17 uses a test tolerance.

The table below is from reading the code at c98bd40ea. AC-16 and AC-17 settle the "To verify" cells.

| Outbox | (a) Age | (b) Ceiling | (c) Cancellation | Notes |
|---|---|---|---|---|
| `InMemoryOutbox` | Meets | Meets | Not applicable: no I/O | Each count sorted all entries at c98bd40ea (issue #4554); PR #4583 has since removed the sort |
| MsSql, PostgreSql, MySql | Meets | Meets | To verify: the command has no timeout (A-9) | Loads up to C whole rows |
| Sqlite | Meets | Meets | To verify: async runs synchronously (A-8) | Loads up to C whole rows |
| Spanner | Meets | Meets | To verify: 60 s command timeout (A-9) | Loads up to C whole rows |
| MongoDb | Meets, but narrowed to `args["Topic"]` when the bag sets a topic (FR-12) | Meets | To verify | Loads up to C whole documents |
| DynamoDB, DynamoDB V4 | Meets, on an eventually consistent index (A-7) | **Gap.** C is split across `ScanConcurrency` segments, and each stops at its share. With C of 100 and 3 segments (shares 34, 33, 33) and S of 100 spread 50, 30, 20, V is 84 (`DynamoDbOutbox.cs:851-924`, `:974`; V4 `:863`, `:986`). To verify: a C below `ScanConcurrency` gives a segment a share of 0, so it sends a Scan with `Limit = 0` (`:906`). | Meets: the token is linked with the outbox's own timeout | `Select.COUNT`, so no bodies are read. The age filter is applied after the read, so read volume is not bounded by C (NFR-4). |
| Firestore | **Gap.** Filters only on the dispatched flag | **Gap.** No limit | **Gap.** The token is not passed to the aggregation query or its stream (`FirestoreOutbox.cs:1191-1246`) | The count runs on the server |

**FR-14.1 — Every gap is closed or tracked before release.**
Each Gap cell, including "To verify" cells that turn out to be gaps, is either closed in this spec with its AC passing, or tracked in a linked GitHub issue and listed as a known limitation in the documentation. Q-9 decides which, gap by gap.

**FR-15 — The instrument's attributes are fixed and few.**
- The attribute set is the same on every measurement.
- No attribute carries a message id, topic, routing key, correlation id or other per-message value.
- A process has at most one series per measured outbox, which in this spec means one.
- Outbox and instance identity is Q-8. `BrighterSemanticConventions.OutboxType` (`paramore.brighter.outbox.type`, `BrighterSemanticConventions.cs:110`) is an existing candidate.

### Non-functional Requirements

**NFR-1 — Measurement adds nothing to the posting path.**
Enabling the feature adds no work, outbox call, lock or wait to a posting-path call. If Q-2 is answered by adding an on-call trigger, that trigger is the only exception to this requirement, including the rest of this paragraph: it is a non-blocking signal that does no I/O, takes no lock that measurement or the outstanding-message check can hold while doing I/O, and never waits, and the ADR names it. Apart from the trigger's own signal, measurement shares no lock, semaphore or flag with the posting path, in particular not `s_checkOutstandingSemaphoreToken` (`OutboxProducerMediator.cs:75`, issue #4584) or `_outstandingCheckInFlight` (`:91`). A blocked measurement does not delay posting. A blocked outstanding-message check does not delay a measurement.

**NFR-2 — The outstanding-message check is unchanged.**
With or without the feature, the check keeps its trigger, interval, count arguments, result and `OutboxLimitReachedException` behaviour. Measurement neither reads nor writes the mediator's outstanding count.

**NFR-3 — Public API changes are additive, and existing meter roles get no new members.**
No member is added to, removed from or changed on `IAmABrighterMessagingMeter`, `IAmABrighterDbMeter` or `IAmABrighterSweeperMeter`. The maintainer stated the rule for `IAmABrighterSweeperMeter` on issue #4573: "it is a public role, and a new member would break anyone who implements it". This spec applies it by analogy to the other two meter roles, because the same reason holds for them. A member is added to the outbox interfaces, `IAmABrighterTracer`, `IAmAnOutboxProducerMediator` or `IAmProducersConfiguration` only as Q-4 answers. New types, optional capability interfaces (as `IHaveAChannelFailureCount` is), and optional constructor parameters with defaults are allowed.

**NFR-4 — One measurement costs one bounded count.**
- **(a)** Exactly one outbox call per measurement, with no retry.
- **(b)** The request is limited to C. Firestore does not meet this (FR-14). DynamoDB limits the count it returns but not the items it reads. A large backlog of young outstanding messages is read in full over many round trips, and against the outbox's 500 ms default timeout (A-7) that can make every measurement fail.
- **(c)** No message body is transferred to produce the count. The relational outboxes and MongoDb do not meet this: a comment on issue #4489 reports 100 rows of 200 KB allocating about 60 MB, while `GetNumberOfOutstandingMessages` (`RelationDatabaseOutbox.cs:769`) runs `COUNT(1)`. (c) is binding if Q-5 is answered yes; otherwise it is a known limitation under FR-14.1.

**NFR-5 — Measurement depends on neither a metrics pipeline nor tracing.**
Measurement and recording go through `System.Diagnostics.Metrics` and `IMeterFactory` only, and use no OpenTelemetry SDK types. The only exception is registration code inside `AddBrighterInstrumentation`, which already works on OpenTelemetry's `MeterProviderBuilder`, if Q-6 places the instrument there. With no metrics configured, measurements behave as FR-7 to FR-13 state (subject to Q-11), and nothing throws. Turning tracing off, or sampling every span out, changes no recorded value.

**NFR-6 — Measurement's own time is testable with a fake clock.**
Measurement's due times, timeout and stop bound come from an injectable `TimeProvider`. Ages are the outbox's (A-2) and are not covered by this requirement. The instrument can be observed through a `MeterListener` over an `IMeterFactory` from `ServiceCollection.AddMetrics()`, as the tests of ADR `0081-timed-outbox-sweeper-health-metrics` do.

**NFR-7 — The feature is documented.**
Every new public type and member has XML documentation that follows `.agent_instructions/documentation.md`. The telemetry page in BrighterCommand/Docs covers:
- what is measured;
- the settings, their defaults and their maximums;
- that V equal to C means "C or more";
- how to read "no recent reading" (FR-6);
- that instances sharing a store are combined with max, not sum (A-6);
- that MongoDb counts only the bag's topic when the bag sets one (FR-12);
- an example alert;
- how to choose A when a sweeper runs (A-3);
- each outbox's known limitations.

The release notes describe the feature.

### Constraints and Assumptions

- **C-1.** The value is recorded directly, never derived from spans. The reason is the one ADR `0081-timed-outbox-sweeper-health-metrics` gives: sampling drops spans.
- **C-2.** Public API changes are additive only (NFR-3).
- **C-3.** The outstanding-message check, and issue #4584's findings about it, are not changed (NFR-2).
- **C-4.** `TimedOutboxSweeper`, its options, `IAmABrighterSweeperMeter` and the sweeper instruments are not changed.
- **C-5.** Names follow ADR `0010-brighter-semantic-conventions` and are added to `BrighterSemanticConventions`.
- **C-6.** A third-party outbox is measured through the same count and gets whatever accuracy its count provides.
- **C-7.** When the feature is not enabled, behaviour is identical to today.

- **A-1. Reference time per outbox.**
  - `InMemoryOutbox`: the time the entry was added (`WriteTime`, `InMemoryOutbox.cs:150`).
  - Relational outboxes: the `Timestamp` column, taken from `Header.TimeStamp` (`RelationDatabaseOutbox.cs:1456`).
  - MongoDb and Firestore: `TimeStamp` from the header.
  - DynamoDB: `OutstandingCreatedTime`, from `Header.TimeStamp`, or `DateTimeOffset.UtcNow` if that is unset (`MessageItem.cs:229-239`), which is the system clock rather than the outbox's `TimeProvider`.
  - Firestore is the exception: its count compares no timestamp at all, which is its FR-14 (a) gap.
- **A-2. Each outbox's clock.**
  - `InMemoryOutbox`, MongoDb, Firestore and both DynamoDB outboxes use their configured `TimeProvider`.
  - The relational outboxes use `DateTimeOffset.UtcNow` (`RelationDatabaseOutbox.cs:1412`, `MsSqlOutbox.cs:100`, `SpannerOutbox.cs:87`).
  - For a store shared across processes, a clock skew of d shifts every age by d. Clocks are assumed to agree within seconds, and A to be minutes.
- **A-3. Choosing A is the app's job.** A message that legitimately waits longer than A counts as stale. When a sweeper runs, A should exceed its `MinimumMessageAge` plus `TimerInterval` (Q-13).
- **A-4.** A message the broker accepted but the outbox never marked as dispatched is counted, as the sweeper would resend it.
- **A-5.** Messages in uncommitted transactions are not visible to the count.
- **A-6.** Each instance sharing a store counts the same rows, so summing their values overcounts and max does not. This holds when the instances pass the same bag; MongoDb instances with different topics in their bags count different sets (FR-12). Each `InMemoryOutbox` reports only its own process.
- **A-7.** DynamoDB scans `OutstandingAllTopics` with `ConsistentRead = false` and has its own operation timeout (`DynamoDbConfiguration.Timeout`, 500 ms by default). When T is greater than that timeout, a count that exceeds it fails before T applies.
- **A-8.** Microsoft.Data.Sqlite runs its async ADO.NET methods synchronously.
- **A-9.** The relational count always passes a timeout of -1 (`RelationDatabaseOutbox.cs:987`), which `CreateCommand` turns into `CommandTimeout` 0, no limit (`:1398`). Spanner turns it into 60 seconds (`SpannerOutbox.cs:57`). The app cannot configure this timeout.

### Out of Scope

- **OOS-1.** The channel that notifies each failed `Clear(Async)`, and any option that makes `PostAsync` throw.
- **OOS-2.** Alerting rules, beyond one documented example.
- **OOS-3.** Changes to the sweeper, its schedule, its lock or its metrics.
- **OOS-4.** Changes to the outstanding-message check or `MaxOutStandingMessages`, including the fixes for issue #4584.
- **OOS-5.** Per-message or per-topic detail in the metric.
- **OOS-6.** A health check for stale messages.
- **OOS-7.** Sending, retrying, deleting or archiving stale messages.
- **OOS-8.** The inbox.
- **OOS-9.** Accuracy guarantees for third-party outboxes.
- **OOS-10.** The correction to the Post section of the Outbox Support docs raised on issue #4489.
- **OOS-11.** An instrument for the age of the oldest stale message.
- **OOS-12.** Aligning the outboxes' behaviour at an age of exactly A.

## Acceptance Criteria

**Setup unless an AC says otherwise:**
- **Clock.** A `FakeTimeProvider` at 2026-01-01T12:00:00Z, and measurement starts at 12:00:00.
- **Settings.** A of 5 minutes, C of 100, I of 1 minute, T of 10 seconds.
- **Listener.** A `MeterListener` over `AddMetrics()`, collecting every 10 seconds of fake time.
- **Instrument creation.** However Q-6 is answered: if the instrument is created by `AddBrighterInstrumentation`, the setup includes it; if it is created directly through `IMeterFactory`, it does not need to. The ADR states which, and the listener-based ACs use that setup.
- **Outbox.** An `InMemoryOutbox` on the same clock.
- **Logger.** A test logger that captures entries.
- **Counting outbox.** A double that implements both outbox interfaces. It records each call with its arguments and token. On request it can block asynchronously until released, block the calling thread before returning a task, throw, or ignore cancellation.

**AC-1 (FR-1).** **Given** `AddProducers` with the counting outbox (`MaxOutStandingMessages = -1`), `AddBrighterInstrumentation`, and no stale step, **When** fake time advances 10 minutes, **Then** the counting outbox records no count call and the listener receives no value from the FR-5 instrument. **And** with the step added, at least one count call is recorded and at least one value is received.

**AC-2 (FR-2).** **Given** each of these in turn:
- A of zero;
- A of -1 second;
- A of `TimeSpan.MaxValue`;
- C of 0;
- C of `int.MaxValue`;
- I of zero;
- I of 50 days;
- T of zero;
- T equal to I;
- T of 2 minutes with I of 1 minute.

**When** the step runs and the app starts, **Then** a `ConfigurationException` naming the setting and value is thrown, no later than when measurement starts, and no count call is made.

**AC-3 (FR-2).** **Given** only A, **Then** C, I and T equal the defaults the ADR records and the documentation states, and the default T is less than the default I. **And given** A of 1 ms, C of 1, I of 1 minute and T of 59 seconds, **then** the app starts. **And given** A and C at the ADR's maximums, **then** the app starts, and a measurement against `InMemoryOutbox` completes without an exception.

**AC-4 (FR-3, FR-5).** **Given** the following:
- m1 added at 12:00:00, not dispatched;
- m2 added at 12:00:00, dispatched at 12:00:01;
- m3 added at 12:04:00, not dispatched.

**When** time advances to 12:06:30, **Then** the 12:06:00 measurement records V of 1. **And when** m1 is dispatched at 12:06:40 and time reaches 12:07:30, **then** V of 0 is recorded as a value. **And at** 12:10:30, V is 1, because m3 is now 6 minutes old.

**AC-5 (FR-4).** **Given** C of 3 and S undispatched messages all older than 5 minutes, **Then** V is 0, 2, 3 and 3 for S of 0, 2, 3 and 50.

**AC-6 (FR-5, NFR-5).** **Given** no `ActivityListener`, **Then** no value has been received at 12:00:30. At 12:01:30 a value has been received, and it meets these conditions:
- it is on meter `Paramore.Brighter`;
- its name starts with `paramore.brighter.outbox.`;
- its unit is `{message}`;
- it equals the V of the 12:01:00 measurement.

**AC-7 (FR-6, FR-10).** **Given** two runs with the counting outbox:
- run 1 returns 0 at 12:01:00 and throws `InvalidOperationException` at every later due time;
- run 2 returns 0 at every due time.

**When** the listener collects at each 10-second step from 12:02:10 to 12:03:30, **Then** at every collection, what the listener receives in run 1 differs from run 2 through the signal the ADR chose. In neither run does the FR-5 instrument report a value other than 0. **And** the same holds when run 1's count, from 12:02:00 on, blocks and honours cancellation, so each call is abandoned at T; in that variant the collections run at 12:02:15, 12:02:25 and so on, 5 seconds off the abandonment instants.

**AC-8 (FR-7).** **Given** the counting outbox returning 0 at once, **When** time advances in 1-second steps to 12:03:30, **Then** exactly 3 calls were made, at 12:01, 12:02 and 12:03, whether the listener collects every 10 seconds or every 1 second. The coalescing of FR-7's third bullet is not tested here: a fake clock fires each due time in turn when it advances, so it does not produce a missed due time reliably. The result would not be deterministic with a fake clock. It is checked by reviewing the scheduling code against the ADR, which states how the scheduler computes the next grid point itself (for example a one-shot timer set to the next start + k × I), rather than relying on a periodic timer's own catch-up behaviour.

**AC-9 (FR-6, FR-8, FR-9).** **Given** a count that blocks asynchronously, ignores cancellation and is released at 12:03:30, **When** time advances to 12:04:30, **Then** all of these hold:
- the 12:01:00 call's token was cancelled at 12:01:10;
- no call started at 12:02 or 12:03, and an entry naming the outbox type was logged for each;
- a call started at 12:04:00;
- no value was recorded for 12:01;
- at the collections at 12:02:20 and 12:03:20, the FR-6 signal shows that no measurement has completed since measurement started (skipped due times included), and differs from what the listener receives in the same run with the feature disabled.

**AC-10 (FR-9).** **Given** a count that honours cancellation and does not answer, **Then** the 12:01:00 call ends with `OperationCanceledException`, no value is recorded, and the abandonment is logged as an abandonment, not as a failure. **And when** the 12:02:00 call returns 4, **then** V of 4 is recorded. **And given** instead a count that ignores cancellation and returns 7 at 12:01:30, **then** 7 is never recorded.

**AC-11 (FR-9, FR-13).** **Given** a counting outbox whose `GetOutstandingMessageCountAsync` blocks the calling thread on a wait handle before returning any task, ignoring the token, **When** fake time advances in 1-second steps from 12:00:59 to 12:02:30, **Then** all of these hold:
- every `Advance` call returns; the test fails, rather than hangs, if one has not returned within 5 seconds of wall-clock time;
- the token was cancelled at 12:01:10;
- no value was recorded for 12:01;
- the 12:02:00 due time made no call.

**And when** a stop is requested at 12:02:30 and time advances 10 seconds, **then** stopping has returned while the call is still blocked.

**AC-12 (FR-6, FR-10).** **Given** a count that throws `InvalidOperationException("connection refused")` at 12:01:00 and returns 2 at 12:02:00, **Then** a log entry carries the exception and the outbox type, no value is recorded for 12:01, V of 2 is recorded for 12:02, and the app is still running. **And** at the collection at 12:02:20 the FR-6 signal shows that a measurement has completed since the 12:01:00 failure, compared with what the listener received at the 12:01:20 collection.

**AC-13 (FR-11).** AC-4 passes with no sweeper registered. **Given** `UseOutboxSweeper`, with a sweep blocked indefinitely by a counting outbox whose `OutstandingMessagesAsync` blocks while its count returns at once (so the setup does not depend on where Q-1 puts measurement), and a lock double that records every call, **When** time advances 5 intervals, **Then** 5 measurements start on their due times. The lock double records the same calls as in the same run with the feature disabled, apart from any calls on a resource of the feature's own (Q-8).

**AC-14 (FR-12).** **Given** the counting outbox and `OutBoxBag` of `{"Topic": "orders"}`, **When** 3 measurements run, **Then** the asynchronous count is called 3 times, each with these arguments:
- `dispatchedSince` A;
- `maxCount` C;
- `args` with the same entries as the bag;
- `requestContext` null;
- a cancellable token.

The synchronous count is never called. **And with** no bag configured, `args` is an empty dictionary, as the check passes.

**AC-15 (FR-13).** **Given** a count that blocks asynchronously from 12:01:00 and ignores cancellation, **When** a stop is requested at 12:01:05, inside T, **Then** all of these hold:
- the token is cancelled at 12:01:05;
- stopping has returned once fake time reaches 12:01:15;
- no call starts afterwards, even 10 minutes later;
- nothing is logged as a failure.

**And with** a count that honours cancellation, stopping returns without fake time advancing.

**AC-16 (FR-3, FR-4, FR-14 (a)(b)).** **Given** each outbox in scope against its real store or emulator, holding 9 messages:
- 5 undispatched, with a reference time 10 minutes old;
- 2 with the same reference time, dispatched;
- 2 undispatched, with a reference time 1 minute old.

Reference times are relative to the outbox's own clock. `InMemoryOutbox` uses a `FakeTimeProvider`. The relational outboxes use real time (A-2). The others set `Header.TimeStamp`. **When** the asynchronous count runs with A of 5 minutes, **Then** it returns 5 for C of 100 (the (a) age assertion), and 3 for C of 3 and 5 for C of 5 (the (b) ceiling assertions). For DynamoDB, whose index is eventually consistent (A-7), each count is retried until it equals the expected value or 10 seconds pass. For a column marked Meets, the assertion then fails as usual; recording the observed value under AC-18 applies only to columns marked Gap or "To verify". An outbox whose FR-14 cell for a column is Gap is exempt from that column's assertion, and its observed result is recorded under AC-18. **And for** DynamoDB and DynamoDB V4 with the default `ScanConcurrency` of 3, the count also runs once with C of 1 and once with C of 2, below the scan concurrency, with no expected value and no retry. Whether it returns, fails or undercounts is recorded under AC-18, which settles the FR-14 "To verify" cell for a C below `ScanConcurrency`.

**AC-17 (FR-14 (c)).** **Given** each outbox other than `InMemoryOutbox`, with the store kept from answering for at least 30 seconds, **When** the token is cancelled 1 second after the call starts, **Then** the call ends with `OperationCanceledException`, or the client's cancellation exception, within 5 seconds of the cancellation.
- The 5 seconds is a test tolerance, not a contract (Q-10).
- For the relational outboxes, a second connection holds an exclusive table lock.
- For DynamoDB and DynamoDB V4, the configuration is constructed with `timeout: 0` (no outbox timeout), so the outbox's own 500 ms default (A-7) does not end the call before the token is cancelled.
- Where the store cannot be held (for example DynamoDB Local, MongoDb, the Spanner emulator or the Firestore emulator, unless the test environment offers a way), the test uses a token cancelled before the call, plus a review showing that the token reaches the client.

**AC-18 (FR-14.1).** **Given** the FR-14 table after AC-16 and AC-17, **When** the feature is prepared for release, **Then** every Gap is either closed, with its case passing, or linked to an open issue and listed under that outbox's known limitations.

**AC-19 (FR-15).** **Given** 10 completed measurements over messages with 10 ids and 3 topics, **Then** every value carries the same attribute set, no attribute value equals a message id, topic or correlation id, and there is one series.

**AC-20 (NFR-1).** **Given** the stale count blocked indefinitely, once asynchronously and once by blocking the thread, **When** the app calls `Post` 100 times and `PostAsync` 100 times, **Then** every call returns (with a 30-second wall-clock guard), and the counting outbox records the same posting-path calls as with the feature disabled. **And given** `MaxOutStandingMessages` of 10, with the check's count blocked (calls with `maxCount` 11), **then** the next stale measurement starts at its due time.

**AC-21 (NFR-2).** **Given** `MaxOutStandingMessages` of 5, **When** the same posts run with the feature enabled and disabled, **Then**:
- the check's count calls carry the same `dispatchedSince`, `maxCount` and `args` in both runs;
- after each post the test waits until the check queued by that post has finished, using the bounded wait-and-retry of `When_the_outstanding_limit_is_exceeded_with_an_async_only_outbox_should_throw.cs`;
- with that wait, `OutboxLimitReachedException` is first thrown on the same post in both runs;
- the existing check tests pass unchanged.

**AC-22 (NFR-3).** **Given** the pull request diff, **Then** no meter role listed in NFR-3 is changed, any member added to another existing interface matches the answer to Q-4, and every other public change is a new type, an optional capability interface, or an optional parameter with a default.

**AC-23 (NFR-4).** **Given** the counting outbox and C of 25, **When** 5 measurements complete, **Then** exactly 5 asynchronous count calls were made, each with `maxCount` 25, and no other outbox call came from measurement. Body-transfer and read-volume gaps are recorded under AC-18.

**AC-24 (NFR-5).** **Given** no `AddBrighterInstrumentation` and no listener, **When** the app runs 5 intervals and stops, **Then** nothing throws and nothing is logged as a failure. **And** new measurement and recording code references no `OpenTelemetry` namespace; registration code inside `AddBrighterInstrumentation` is exempt (NFR-5).

**AC-25 (NFR-6).** **Given** AC-1 to AC-15, AC-19 to AC-21, AC-23 and AC-24, **Then** they drive measurement's time through `FakeTimeProvider` and observe values through `MeterListener`. The wall-clock guards in AC-11 and AC-20 only detect hangs; they do not drive time.

**AC-26 (NFR-7).** **Given** the merged feature, **Then** every new public type and member has XML documentation, the telemetry page covers each item in NFR-7, and the release notes describe the feature.

### Requirement → Acceptance Criteria map

| Requirement | AC | Requirement | AC |
|---|---|---|---|
| FR-1 | AC-1 | FR-12 | AC-14 |
| FR-2 | AC-2, AC-3 | FR-13 | AC-11, AC-15 |
| FR-3 | AC-4, AC-16 | FR-14 | AC-16, AC-17 |
| FR-4 | AC-5, AC-16 | FR-14.1 | AC-18 |
| FR-5 | AC-4, AC-6 | FR-15 | AC-19 |
| FR-6 | AC-7, AC-9, AC-12 | NFR-1 | AC-20 |
| FR-7 | AC-8 | NFR-2 | AC-21 |
| FR-8 | AC-9 | NFR-3 | AC-22 |
| FR-9 | AC-9, AC-10, AC-11 | NFR-4 | AC-23 |
| FR-10 | AC-7, AC-12 | NFR-5 | AC-6, AC-24 |
| FR-11 | AC-13 | NFR-6 | AC-25 |
| | | NFR-7 | AC-3, AC-26 |

**Definition of done:**
- Every AC passes, or, for AC-16 and AC-17, the gap is recorded under AC-18.
- These test projects pass:
  - `Paramore.Brighter.Core.Tests`, including `Sweeping/` and `Observability/`;
  - `InMemory.Tests`;
  - `MSSQL.Tests`, `MySQL.Tests`, `PostgresSQL.Tests` and `Sqlite.Tests`;
  - `MongoDb.Tests`;
  - `DynamoDB.Tests` and `DynamoDB.V4.Tests`;
  - `Gcp.Tests` (Spanner and Firestore).
- The ADR records the defaults, the maximums, and the answers to the Q-items it depends on.

## Additional Context

### Open questions for the maintainer

- **Q-1. Where does measurement run, and what starts and stops it?**
  - **Proposal:** a hosted service of its own in `Paramore.Brighter.Outbox.Hosting`, registered by its own extension, as `UseOutboxArchiver` registers `TimedOutboxArchiver`. It would run apart from `TimedOutboxSweeper`.
  - **Alternative: a timer in the mediator.** This was the proposal on the issue. It couples measurement to the posting component.
  - **Alternative: an observable gauge whose callback runs the count.** This runs store I/O on the collection thread and ties query frequency to collection, against FR-7.
- **Q-2. What do "on the call" and "warns" mean?** "On the call" could mean telemetry emitted on `Post`/`Clear` rather than on a schedule. But a quiet system with no posts would then never report a message that failed once, so the proposal is that any on-call trigger is added to the schedule of FR-7, not put in its place, runs at most one triggered measurement per I, and counts as completed, failed or abandoned for FR-6 like any other measurement. If Q-2 is answered that way, FR-7's title and first bullet and step 2 of the Proposed Solution are updated to include the trigger. "Warns" could mean a log Warning whenever V > 0, in addition to the metric.
- **Q-3. Outbox that implements only `IAmAnOutboxSync`.** Options:
  - reject it at configuration;
  - run the synchronous count on a dedicated thread;
  - skip measurement and log once.
- **Q-4. Configuration form and existing interfaces.** Is a new member on `IAmProducersConfiguration` (for example `StaleMessageAge`), `IAmABrighterTracer` or the outbox interfaces acceptable, possibly with a default implementation? Or should the step be a registration extension with options of its own?
- **Q-5. An age-aware count capability?** This would be an optional interface that runs `COUNT` with an age filter and a limit, instead of loading rows, with a fallback to `GetOutstandingMessageCountAsync`. It decides whether NFR-4(c) is binding.
- **Q-6. Instrument type and owning role.** Should it be an observable instrument registered by `AddBrighterInstrumentation`, as `PublishConfirmationMeter` is, or a synchronous gauge? Should it go with `OutboxMeter` from ADR `0084-outbox-metrics`, or have a meter of its own?
- **Q-7. One age, or one per publication?** A per-publication age would need per-topic counts and per-topic series (OOS-5).
- **Q-8. Outbox and instance identity.** Options:
  - an attribute such as `paramore.brighter.outbox.type` plus the table or collection name;
  - documenting max across instances;
  - one measuring instance, elected through a lock on a resource of its own. Choosing this means instances that are not elected start no measurement at their due times (FR-7 allows for it).
- **Q-9. Close the FR-14 gaps here, or in separate issues?**
- **Q-10. Cancellation bound.** What bound should each outbox's count meet after its token is cancelled? AC-17's 5 seconds is a test tolerance only.
- **Q-11. Skip a due measurement when no listener is subscribed?**
- **Q-12. Suppress the database span the outbox's count creates?** With a null request context (FR-12), each measurement may start a new trace.
- **Q-13. Warn at startup when A is not greater than the sweeper's `MinimumMessageAge` plus `TimerInterval` (A-3)?**

### Code locations

- **Counts:**
  - `IAmAnOutboxSync.cs:139`, `IAmAnOutboxAsync.cs:213`;
  - `InMemoryOutbox.cs:617`, `:647-656`;
  - `RelationDatabaseOutbox.cs:1000-1009`;
  - `DynamoDbOutbox.cs:851-924` (V3), `:863` (V4);
  - `MongoDbOutbox.cs:975-984` (topic filter at `:497-500`);
  - `FirestoreOutbox.cs:404`, `:1191`.
- **Outstanding-message check:** `OutboxProducerMediator.cs:1372-1426`, queued at `:809-854`.
- **Sweeper and meters:**
  - `TimedOutboxSweeper.cs`, `SweeperMeter.cs`, `PublishConfirmationMeter.cs`;
  - registered in `BrighterMetricsBuilderExtensions.AddBrighterInstrumentation`.
- **Hosting:** `Paramore.Brighter.Outbox.Hosting/HostedServiceCollectionExtensions.cs`.
- **Optional capability precedent:** `Paramore.Brighter.ServiceActivator/IHaveAChannelFailureCount.cs`.
