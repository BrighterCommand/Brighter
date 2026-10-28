# Requirements

> **Note**: This document captures user requirements and needs. Technical design decisions and implementation details should be documented in an Architecture Decision Record (ADR) in `docs/adr/`.

**Linked Issue**: #4489

**Related**: issue #4554 (outstanding-message check cost), issue #4584 (outstanding-message check with no time bound; PR #4597 removed the shared semaphore it also reported, and PR #4613 bounded the relational count), issue #4573 (hung sweeper detection), PR #4484 (outbox metrics, ADR `0084-outbox-metrics`), PR #4597 (removes the outstanding count semaphore shared across mediators), PR #4598 (this spec, ADR `0085-outbox-stale-message-telemetry` and ADR `0086-outbox-per-topic-stale-count`; on 2026-10-13 the maintainer asked there for an age per publication, in comment `issuecomment-6141309069`, and suggested a count per topic beside the overall count, in review comment `discussion_r4256375898` on this document), ADR `0081-timed-outbox-sweeper-health-metrics`, ADR `0063-failed-delivery-context`, ADR `0022-generate-metrics-from-traces`, ADR `0010-brighter-semantic-conventions`.

Code locations refer to commit 1ce331eb2 on the PR #4598 branch, which merges master at commit ef2a459d9, PR #4597 included. The version approved on 2026-10-11 pinned commit c98bd40ea; every location was checked again at commit 1ce331eb2.

## Problem Statement

**As an operator** of an application that publishes through Brighter's outbox, **I would like** a metric that tells me how many messages have stayed in the outbox without being dispatched for longer than an age I choose, across the whole outbox and, where the outbox supports it, for each topic I publish to, for the in-memory outbox and for each database outbox that ships with Brighter, whether or not the outbox sweeper runs, **so that** a message that was never sent, including one held back because its topic's circuit breaker is open, shows up on my dashboards and alerts instead of waiting in the outbox where nobody looks.

### Why such a message is invisible today

- `CommandProcessor.PostAsync` deposits the message and then clears it. When the send fails, `ExecuteWithResiliencePipelineAsync` (`OutboxProducerMediator.cs:1500`) logs the exception and returns false (`:1534-1539`). The message stays undispatched and nothing reaches the caller. ADR `0063-failed-delivery-context` applies the same rule (stay undispatched, let a sweeper retry) to failed publish confirmations on Kafka and RabbitMQ.
- The sweeper is opt-in (`UseOutboxSweeper`). Without it, nothing sends the message again. With the default `InMemoryOutbox`, the message is also lost when the process stops.
- The outbox circuit breaker exists only when the app registers an `IAmAnOutboxCircuitBreaker`; `AddProducers` resolves it and passes null otherwise (`ServiceCollectionExtensions.cs:1014-1016`). When one is registered, the mediator trips the message's header topic on it after a failed asynchronous send (`OutboxProducerMediator.cs:1313`), a failed bulk batch (`:1240`, the batch's `RoutingKey`, which is its messages' header topic, `MessageBatch.cs:28`) and a failed publish confirmation (`:946`, `:1040`). A failed synchronous send trips nothing (`Dispatch`, `:1119-1136`). The sweeper's clear (`OutboxSweeper.cs:79`, `OutboxProducerMediator.cs:514`) passes the tripped topics to `OutstandingMessagesAsync` (`:762-763`), so that topic's messages stay in the outbox until the breaker cools down. One count over the whole outbox cannot show which topic they belong to.
- Nothing reports how many messages are waiting:
  - the outstanding-message check counts only when `MaxOutStandingMessages` is set (`OutboxProducerMediator.cs:824-827`), and only to enforce the limit;
  - the sweeper metrics of ADR `0081-timed-outbox-sweeper-health-metrics` report the sweeper's own health, and exist only when a sweeper runs;
  - the outbox metrics of ADR `0084-outbox-metrics` count messages added and cleared; their difference has no age and restarts with the process, so it cannot tell a message about to be sent from one that never will be;
  - outbox spans are usually sampled out in production.

### Scope

The maintainer's item on issue #4489 is: "Telemetry on the call that warns about Outbox items over a configured age. It should work across im-memory and out-of-process outboxes." This spec covers that item. What "on the call" and "warns" mean is Q-2. On PR #4598 (2026-10-13), the maintainer asked for an age per publication, because a topic can be circuit broken (comment `issuecomment-6141309069`), and suggested a count per topic beside the overall count, so that topic circuit breakers show up (review comment `discussion_r4256375898`). This spec meets the request and takes up the suggestion (Q-7). The second item on the issue, a channel that notifies each failed `Clear(Async)`, is out of scope (OOS-1).

### Changes from the proposal on issue #4489

The proposal posted on the issue had four parts:
- a `StaleMessageAge` on `ProducersConfiguration`;
- a timer in the outbox producer mediator, running about twice per age;
- a root span per count;
- a gauge derived from that span by the span-to-metrics processor (ADR `0022-generate-metrics-from-traces`).

This spec changes three things:
- **The value is recorded directly on the Brighter meter (C-1).** ADR `0081-timed-outbox-sweeper-health-metrics` rejected span-derived metrics for health signals because sampling drops the spans exactly when they matter. Issue #4573 points the sweeper's new level instrument at an existing precedent, `PublishConfirmationMeter`, an `ObservableUpDownCounter` registered by `AddBrighterInstrumentation`, and forbids a new member on `IAmABrighterSweeperMeter`. NFR-3 extends that rule to the other meter roles.
- **Where measurement runs and how it is configured are left open (Q-1, Q-4).** The requirements state only what an operator observes, so either a mediator timer or a hosted service can meet them. The interval is a setting of its own, and its default is for the ADR.
- **A count per topic is added beside the overall count, with an age per publication (FR-2, FR-3.1, FR-5, FR-12.1).** On PR #4598 the maintainer asked for an age per publication: because a topic can be circuit broken, "we should have an age per publication". A topic whose circuit breaker is open holds its messages back, and one count over the outbox does not show which topic they belong to. The maintainer also suggested supporting a per-topic outstanding count beside the overall one, and this spec takes that up (Q-7). The overall count keeps the proposal's single age; each publication may set an age of its own for its topic.

Three smaller parts of the proposal change too: the new `InstrumentationOptions.Outbox` flag is dropped, because a directly recorded instrument does not go through span options; the fixed `maxCount` of 100 becomes the configurable ceiling C; and the fixed 30-second give-up becomes the configurable timeout T, which must be less than I (FR-2).

## Proposed Solution

1. The app turns the feature on with one explicit configuration step that names a **stale age**. It may also set a **ceiling**, a **measurement interval** and a **measurement timeout**. Each has a documented default. Each publication may set a stale age of its own; a publication without one uses the stale age of the step.
2. While measurement runs, Brighter makes two counts once per interval, each up to the ceiling:
   - the **overall count**: the outbox's undispatched messages older than the stale age;
   - the **per-topic count**: for each topic of a configured publication, the undispatched messages on that topic older than that topic's age. An outbox that does not support a count per topic reports the overall count only.
3. Each completed count is recorded on an instrument of its own on the `Paramore.Brighter` meter: the overall count on one gauge, and the per-topic count on a second gauge with one series per topic, tagged with `messaging.destination.name`. An app that already uses `AddBrighterInstrumentation` exports both with no further setup.
4. A count that fails or runs past its timeout records no value. For each count, an operator can tell "no recent reading" apart from "zero stale messages" through metrics alone.
5. Measurement never delays a post, never waits for a sweep, and does not change the outstanding-message limit.

Without the configuration step, nothing new runs and the outbox receives no new queries.

## Requirements

### Terms

| Term | Meaning |
|---|---|
| **Outbox** | The object registered as `IAmAnOutbox`. `AddProducers` registers an `InMemoryOutbox` when the app supplies none (`ServiceCollectionExtensions.cs:996`). This spec measures one outbox per process (C-8), so the overall gauge has one series (FR-15). |
| **Outbox in scope** | `InMemoryOutbox`; MsSql, PostgreSql, MySql, Sqlite, Spanner; DynamoDB and DynamoDB V4; MongoDb; Firestore. |
| **Dispatched** | The outbox has recorded the message as dispatched (`MarkDispatched[Async]`). |
| **Reference time** | The timestamp each outbox already compares with `dispatchedSince` (A-1). |
| **Age** | The outbox's current time minus the message's reference time. The outbox computes it on its own clock (A-2) when it evaluates the count. |
| **Topic** | A message's topic is `MessageHeader.Topic` (`MessageHeader.cs:321`), the topic the store holds for it. A message is on topic t when that value equals t as `RoutingKey` compares values: ordinal and case-sensitive (`RoutingKey.cs:118`). |
| **Measured topic** | A topic of a configured publication in this process, as FR-3.1 defines. |
| **Stale age (A)** | The age the app configures in the configuration step. The overall count uses it, and it is the default for every publication. |
| **Publication stale age (A_p)** | An optional age set on one `Publication`. Null means A. |
| **Topic age (A_t)** | The age the per-topic count uses for measured topic t: the smallest A_p among the publications on t, where a publication with no A_p contributes A (FR-3.1). |
| **Stale message**, **stale on topic t** | A message is stale when it is not dispatched and its age is greater than A. It is stale on topic t when it is on topic t, not dispatched, and its age is greater than A_t. |
| **Stale count (S)**, **topic stale count (S_t)** | The number of stale messages, and the number of messages stale on topic t, when the outbox evaluates the count. |
| **Ceiling (C)** | The largest value one count reports: for the overall count, and for each topic in the per-topic count. |
| **Reported value (V)**, **topic value (V_t)** | The value a completed overall measurement records, and the value a completed per-topic measurement records for topic t. A value equal to C means "C or more". |
| **Overall count**, **per-topic count** | The two counts. The overall count is the number of stale messages that measurement obtains through `GetOutstandingMessageCountAsync` (or Q-5's capability); it covers the whole outbox unless the outbox narrows it through `args` (FR-12), as MongoDb does. The per-topic count is the number of messages stale on each measured topic that measurement obtains through the per-topic capability. |
| **Per-topic capability** | A new optional capability that an outbox may offer, as `IHaveAChannelFailureCount` is, through which measurement obtains the per-topic count. The requirements fix only what it must return (FR-12, FR-14 (d)) and its bounds (FR-4, FR-8, FR-9, NFR-4); its name, signature and shape are for the ADR. |
| **Measurement** | One call for one count. It begins when measurement calls the outbox. It *completes* when the call returns a result that FR-10 does not treat as a failure, *fails* when the call throws or FR-10 treats its result as a failure, and is *abandoned* when T elapses first. It is *in progress* until the call returns or throws, including after it has been abandoned. A due time, or a triggered run under Q-2, has at most two measurements, one for each count. |
| **Measurement runs** | The period from when measurement starts to when it stops. Both are tied to the app's lifetime; what starts and stops it is Q-1. |
| **Due time** | Start time + k × I, for k = 1, 2, 3 and so on. |
| **Measurement interval (I)**, **measurement timeout (T)** | The time between due times, and the longest a measurement waits for the outbox. |
| **Posting path** | `Post[Async]`, `DepositPost[Async]` and `ClearOutbox[Async]`, and the mediator work they cause, including the outstanding-message check. |
| **Outstanding-message check** | `OutboxProducerMediator.OutstandingMessagesCheck` (`OutboxProducerMediator.cs:1422`). |

### Functional Requirements

**FR-1 — The feature is off by default and turned on by one explicit step.**
The step names A; its form is Q-4. When the feature is not enabled, the outbox receives no call from it, neither gauge records anything, and an A_p set on a publication has no effect and is not checked. No existing call enables it, including `AddProducers`, `UseOutboxSweeper`, `UseOutboxArchiver` and `AddBrighterInstrumentation`. Setting A_p on a publication does not enable it either.

**FR-2 — The settings are validated before any measurement.**

| Setting | Required | Valid values | Default |
|---|---|---|---|
| A | Yes | Greater than `TimeSpan.Zero`, and at most a maximum the ADR fixes. The maximum must be small enough that the outbox's `now - A` cannot overflow (365 days would do). | None |
| C | No | 1 up to a maximum the ADR fixes, below `int.MaxValue`. The relational outboxes and MongoDb load up to C rows (NFR-4). | ADR |
| I | No | Greater than `TimeSpan.Zero`, and at most 4,294,967,294 ms (about 49.7 days), the largest period .NET timers accept. | ADR |
| T | No | Greater than `TimeSpan.Zero` and less than I. | ADR, less than the default I |
| A_p, on each publication | No | Null, or greater than `TimeSpan.Zero` and at most A's maximum. It may be smaller or greater than A. | Null, meaning A |

An invalid A, C, I or T throws a `ConfigurationException` that names the setting and the value. It is thrown when the configuration step runs or, at the latest, when measurement starts, and never during a measurement. An invalid A_p throws a `ConfigurationException` that names the value and, when the publication's topic is not empty, its topic. It is thrown when measurement starts, because measurement reads the publications then (FR-3.1), and never during a measurement. It is not swallowed: it surfaces from starting measurement, as an invalid A, C, I or T surfaces from the configuration step. Every publication's A_p is checked, including one with an empty topic. In each case no measurement of either count runs. The values, the measured topics and their topic ages do not change while measurement runs.

> *Examples.* Each of these throws: A of zero; A of `TimeSpan.MaxValue`; C of 0; C of `int.MaxValue`; I of 50 days; T equal to I (1 minute each); T of 2 minutes with I of 1 minute; A_p of zero, of -1 second, or of A's maximum plus one tick, on any publication. A of 1 ms, C of 1, I of 1 minute and T of 59 seconds together are accepted. With A of 5 minutes, an A_p of 1 ms on one publication and of 10 minutes on another are accepted. With the feature not enabled, an A_p of zero throws nothing (FR-1).

**FR-3 — Each count counts stale messages and only those.**
The overall count includes stale messages and only those: dispatched messages are excluded at any age, and undispatched messages younger than A are excluded. The per-topic count includes, for each measured topic t, the messages stale on t and only those: messages on other topics, dispatched messages, and undispatched messages younger than A_t are excluded. Neither count excludes a topic whose circuit breaker is open: the count interface takes no tripped topics (`IAmAnOutboxAsync.cs:213-218`), and the per-topic capability excludes none either (FR-14 (d)). At an age of exactly A the outboxes already differ (`InMemoryOutbox` and DynamoDB include, the relational outboxes and MongoDb exclude). This spec does not align them, an outbox's per-topic count follows its overall count at an age of exactly A_t, and that boundary is not tested (OOS-12). For an out-of-process outbox, both counts include messages written by other instances that share the store (A-6), and exclude uncommitted ones (A-5). MongoDb narrows the overall count to the bag's topic when one is set (FR-12).

> *Example.* A is 5 minutes and the count runs at 12:06:00. m1 (reference time 12:00:00, undispatched) is counted. m2 (12:00:00, dispatched at 12:00:01) is not. m3 (12:04:00, undispatched, age 2 minutes) is not. V is 1.
>
> *Example, per topic.* A is 5 minutes; "orders" has A_t of 2 minutes and "payments" has A_t of 5 minutes. At 12:06:00 the outbox holds o1 on "orders" (12:00:00, undispatched), o2 on "orders" (12:03:30, undispatched, age 2.5 minutes), o3 on "orders" (12:00:00, dispatched), p1 on "payments" (12:00:00, undispatched) and p2 on "payments" (12:03:30, undispatched). V is 2 (o1, p1). V_t is 2 for "orders" (o1, o2) and 1 for "payments" (p1). The per-topic values sum to 3, so the overall value is not their sum.

**FR-3.1 — The measured topics and their ages are fixed when measurement starts.**
When measurement starts, it reads the configured publications once. How it reads them is for the ADR.
- The measured topics are the distinct non-empty topics (`Publication.Topic`, `Publication.cs:96`) of the configured publications.
- A publication whose topic is null or empty (`RoutingKey.IsNullOrEmpty`, `RoutingKey.cs:68`) adds no topic, and its A_p is not used. The shipped `ProducerRegistry` rejects a null topic with a `ConfigurationException` (`ProducerRegistry.cs:26-27`, `:49-50`), so with it this case is an empty topic (`RoutingKey.Empty`, `RoutingKey.cs:61`).
- A_t is the smallest of the ages of the publications on t, where each publication contributes its A_p, or A when it has none. Several publications can share a topic, because the producer registry is keyed by topic and CloudEvents type (`ProducerKey.cs:37`). The smallest age is taken so that the topic shows at the earliest alert any of its publications asks for (Q-14).
- With no producer registry (`IAmProducersConfiguration.ProducerRegistry`, `ProducersConfiguration.cs:109`, is null), with an empty one, or with no publication that has a non-empty topic, there are zero measured topics, and FR-12.1 applies.
- A topic that has messages in the store but no publication in this process is not measured. Its stale messages appear in the overall count only, and it has no per-topic series.
- A message counts toward its header topic (Terms, Topic). The store holds that topic, and the circuit breaker trips it: the mediator trips the message's header topic, the batch's (`MessageBatch.cs:28`) or the confirmation's wire topic (`PublishConfirmationResult.cs:45`) at `OutboxProducerMediator.cs:946`, `:1040`, `:1240` and `:1313`. A mapper may give a message a header topic that differs from its publication's topic; Brighter then logs the difference and keeps the publication's topic in the `ProducerTopic` bag entry (`Message.ProducerTopicHeaderName`, `WrapPipeline.cs:97-103`). Such a message counts toward its header topic, and is in the per-topic count only if a publication in this process uses that topic. This is a known limitation (NFR-7). Counting by the bag entry instead was considered and rejected (Q-16).
- Neither the topics nor their ages change while measurement runs, even if the app sets a publication's `Topic`, which has a public setter (`Publication.cs:96`), or its A_p after measurement starts.

> *Example.* A is 5 minutes. P1 is on "orders" with A_p of 2 minutes, P2 on "orders" with A_p of 4 minutes, P3 on "orders" with no A_p, P4 on "payments" with no A_p, and P5 has an empty topic and A_p of 1 minute. The measured topics are "orders" with A_t of 2 minutes (the smallest of 2, 4 and 5) and "payments" with A_t of 5 minutes. P5 adds nothing. A stale message on "audit", which no publication uses, is counted by the overall count only. With P4's A_p set to 10 minutes instead, "payments" has A_t of 10 minutes, greater than A.

**FR-4 — Each count stops at the ceiling, per topic for the per-topic count.**
For an outbox that meets FR-14(b), V is `min(S, C)`. For an outbox that meets FR-14(d), V_t is `min(S_t, C)` for each measured topic, whatever the other topics hold: the ceiling applies to each topic and is not shared among them. A measurement asks the outbox for at most C, for each topic in the per-topic count. This is stricter than the interface's `maxCount` wording, "the maximum number of messages to scan" (`IAmAnOutboxSync.cs:136`). A count above C that an outbox returns anyway, for either count, is reported as C. FR-14 records the outboxes that do not meet this.

> *Example.* With C of 3: S of 0, 2, 3 and 50 give V of 0, 2, 3 and 3. With C of 3 and S_t of 50 on "orders", 2 on "payments" and 0 on "refunds", V_t is 3, 2 and 0, and "refunds" reports 0, not nothing.

**FR-5 — Each completed measurement is recorded on the gauge of its count, on the Brighter meter.**
- **Meter and names.** Both gauges are on `BrighterSemanticConventions.MeterName` (`Paramore.Brighter`). Their names follow ADR `0010-brighter-semantic-conventions`, under `paramore.brighter.outbox.`.
- **Overall gauge.** The working name is `paramore.brighter.outbox.stale_messages`. A reader gets the latest V.
- **Per-topic gauge.** An instrument of its own, not a topic attribute on the overall gauge, so that an aggregation over either gauge never mixes the two counts. Its name is for the ADR. It has one series per measured topic, which carries the topic in `messaging.destination.name` (FR-15). A reader gets the latest V_t of each measured topic.
- **Unit and value.** For both gauges the unit is `{message}`, and each value is a whole number, not a sum. A value of 0 is recorded as 0.
- **Before the first completed measurement** of a count, its gauge records no value.
- **Instrument type and owning role** are for the ADR (Q-6), and the same answer applies to both gauges. The precedent is `PublishConfirmationMeter` (`PublishConfirmationMeter.cs:44-51`), an `ObservableUpDownCounter` registered by `AddBrighterInstrumentation`.

**FR-6 — "No recent reading" can be told apart from "zero stale messages" through metrics alone, for each count.**
Each gauge never reports a value that no completed measurement of its count produced, and never reports 0 in place of a missing reading. A due time may produce no completed measurement of a count because it fails, is abandoned or is skipped; a due time left out under Q-8 or Q-11 is not skipped (FR-7). No later than T after such a due time, an operator who reads only metrics can see that no measurement of that count has completed since its last success, or since measurement started when none has succeeded yet, and can tell that state apart from the feature being disabled. For a coalesced due time (FR-7), the bound runs from when the scheduler runs again. If a measurement of the count completes before the bound expires, the signal need not show "no recent reading" for that due time. Once a later measurement of the count completes, an operator reading only metrics can see that one has completed since the last failed, abandoned or skipped due time; with an outcome counter, that means the count's completed total has increased since that outcome was counted.

Each count has a signal of its own: a per-topic measurement that does not complete never shows as "no recent reading" for the overall count, and the other way round. The per-topic count makes one measurement for all measured topics, so its signal covers all of them together and carries no topic. When FR-12.1 leaves the per-topic count out, neither its gauge nor its signal has a series, which an operator tells apart from "no recent reading" as from "disabled". The mechanism is for the ADR. Options:
- an outcome counter (completed, failed, abandoned, skipped) with an attribute that names the count, as ADR `0081-timed-outbox-sweeper-health-metrics` counts sweeps, which covers the case before any success on its own;
- not carrying a stale value forward past one interval, or a last-success timestamp per count; either needs a companion signal, such as a counter or a timestamp seeded with the start time, to tell "no success yet" apart from "disabled".

**FR-7 — Measurements follow a fixed schedule.**
- At each due time the overall measurement starts, and so does the per-topic measurement unless FR-12.1 leaves it out. A count's measurement does not start when FR-8 skips it for that count, when the third bullet coalesces the due time, when Q-8 is answered with an election that leaves this instance out, or when Q-11 is answered by skipping a due measurement while no listener is subscribed.
- How often the metrics pipeline collects does not change how often the outbox is queried (Q-11).
- Due times missed while no measurement of a count is in progress, for example through process suspension or thread-pool starvation, are coalesced for that count into at most one measurement, which starts as soon as the scheduler runs again (subject to FR-8), not caught up one by one. Later due times stay on the start + k × I grid. Of N coalesced due times, the N−1 that get no measurement count as skipped for that count under FR-6 and FR-10, and FR-10 logs them as one entry that carries the number skipped. Due times left out by a Q-8 election or by a Q-11 skip are not skipped outcomes: they are neither shown under FR-6 nor logged under FR-10, as failures or otherwise. The ADR defines what an instance left out by a Q-8 election reports.

> *Example.* I is 1 minute and measurement starts at 12:00:00. At 12:03:30, three overall measurements have started, at 12:01, 12:02 and 12:03, however often metrics are collected. With measured topics and the per-topic capability, three per-topic measurements have started at the same times.

**FR-8 — At most one measurement of each count is against the store at a time.**
From measurement, at most one overall call and at most one per-topic call are in progress at any time, including an abandoned call that has not returned, so at most two calls are in progress in total. A due time that arrives while a call of one count is in progress makes no call for that count and records no value for it. The other count of that due time is decided on its own. The next due time does not change.

> *Example.* A per-topic call starts at 12:01:00, ignores cancellation and returns at 12:03:30. The overall calls at 12:01, 12:02 and 12:03 run and record. The per-topic due times at 12:02 and 12:03 make no call and record nothing, and a per-topic call starts at 12:04.

**FR-9 — A measurement never waits for the store longer than T, and neither count waits for the other.**
T after a call starts, its measurement does three things:
- it stops waiting;
- it cancels the token it passed;
- it records no value.

A result that arrives later is never recorded. The overall and per-topic measurements of one due time do not wait for each other: both calls start when the due time is handled, so no later than T after that, each has completed, failed or been abandoned. This holds when the outbox ignores the token. It also holds when the outbox's asynchronous count, of either kind, blocks the calling thread before it returns a task: `InMemoryOutbox` computes synchronously and then completes the task (`InMemoryOutbox.cs:646-655`), and Sqlite's provider runs async methods synchronously (A-8). A count that blocks its thread neither delays the start of the other count of the same due time nor stops the other from being recorded or abandoned within T. The thread such a count occupies is never one the posting path waits for (NFR-1).

**FR-10 — A failed measurement is visible and stops neither later ones nor the other count.**
If the outbox call throws, no value is recorded for its count and the next due measurement runs as normal. A call also fails when it returns a negative count or, for the per-topic call, a null result or a result that lacks a requested topic; a failed per-topic measurement records no V_t for any topic. Counts returned for topics that were not requested are ignored. The two counts fail independently: the failure of one changes neither the value nor the outcome of the other. No exception thrown during a measurement reaches the app or ends the process (a `ConfigurationException` from FR-2 is not thrown during a measurement). Failures, abandonments and skipped due times show in metrics through FR-6's "no recent reading" signal of their count; telling the three apart in metrics is not required, though the ADR may choose a signal that does. Each is also logged with the outbox type and the count it belongs to, and failures carry the exception. Log levels and wording are for the ADR. A cancellation caused by stopping (FR-13) is not logged as a failure.

**FR-11 — Measurement does not depend on the outbox sweeper.**
Measurements of both counts run with no sweeper registered. With `UseOutboxSweeper`, due measurements run on schedule while a sweep is in progress, including a sweep that never ends. Measurement neither obtains nor waits for the sweeper's lock (resource `OutboxSweeper`, `TimedOutboxSweeper.cs:62`).

**FR-12 — Both counts pass the same `args` as the outstanding-message check.**
The overall measurement calls `IAmAnOutboxAsync<Message, TTransaction>.GetOutstandingMessageCountAsync`, or an asynchronous capability added under Q-5. It passes:
- `dispatchedSince` A;
- `maxCount` C;
- `args` equal to what the check passes: the producers' `OutBoxBag` (`ProducersConfiguration.cs:236`), passed at `OutboxProducerMediator.cs:1438` and `:1453`, or the empty dictionary created at `:200` when none is configured;
- `requestContext` null;
- a cancellable token.

The per-topic measurement obtains, through the per-topic capability, a count for every measured topic. The outbox receives:
- every measured topic, each with its A_t;
- C, as the ceiling for each topic;
- `args` equal to what the overall count passes;
- `requestContext` null;
- a cancellable token.

The requirements bound the per-topic measurement only by what an operator and the store can observe: at most one per-topic outbox call in progress (FR-8); every measured topic answered, or the measurement abandoned, within T (FR-9); at most C for each topic (FR-4); no message body transferred (NFR-4 (c)); and at most two outbox calls per due time, counting the overall call (NFR-4 (a)). The capability's shape within those bounds, and its alternatives, are for the ADR.

Measurement never calls the synchronous count. What happens with an outbox that implements only `IAmAnOutboxSync` is Q-3.

Passing the bag matters because an outbox may read it: MongoDb narrows its overall count to `args["Topic"]` when the bag sets one (`MongoDbOutbox.cs:497-500`), so an app whose bag sets a topic gets only that topic's stale messages in the overall count. The per-topic count's topics come from the publications (FR-3.1), never from `args`, and the capability does not narrow them by `args` (FR-14 (d)). `InMemoryOutbox`, the one outbox that offers the capability in this spec, ignores `args` in both counts, as its overall count does today (`InMemoryOutbox.cs:617-643`). FR-14 records this, and the documentation states it (NFR-7).

**FR-12.1 — Without the per-topic capability, or with no measured topic, only the overall count runs.**
If, when measurement starts, the registered outbox does not offer the per-topic capability, or there are zero measured topics (FR-3.1), then while measurement runs:
- no per-topic call is made;
- the per-topic gauge and the per-topic count's FR-6 signal have no series;
- the overall count runs as FR-3 to FR-13 state;
- one Information log entry, written when measurement starts, names the outbox type and the reason: no capability, or no measured topic (when both apply, no capability). Nothing more about it is logged at due times.

This is not an error: nothing throws, and the app starts. The capability is optional, as `IHaveAChannelFailureCount` is: an outbox without it still reports the overall count, and a third-party outbox may offer it (C-6). How measurement detects it is for the ADR. In this spec only `InMemoryOutbox` offers it (FR-14, Q-15).

> *Example.* A `MsSqlOutbox` with publications on "orders" and "payments": the overall gauge reports at each due time, the per-topic gauge has no series, and one Information entry at start says that `MsSqlOutbox` has no count per topic.

**FR-13 — Stopping measurement is prompt.**
When measurement stops:
- no measurement of either count starts after that;
- the token of every in-progress call, of either count, is cancelled when the stop is requested;
- stopping returns no later than T after the request, even if the outbox ignores the token or blocks its thread (FR-9);
- when the outbox honours the tokens, stopping returns as soon as the cancelled calls end, without waiting for T.

If Q-1's hosted-service proposal is taken, the ADR states how T relates to `HostOptions.ShutdownTimeout`.

> *Example.* T is 10 seconds. A count started at 12:01:00 ignores cancellation. A stop is requested at 12:01:05. The token is cancelled at 12:01:05, and stopping has returned by 12:01:15.

**FR-14 — Each outbox in scope meets the accuracy and cancellation requirements, or its gap is recorded.**
- **(a) Age.** The overall count includes only stale messages (FR-3).
- **(b) Ceiling.** With no concurrent writers, the overall count returns exactly `min(S, C)`.
- **(c) Cancellation.** If the token is cancelled before the store answers, the call ends with `OperationCanceledException` or the store client's own cancellation exception, without waiting for the store to answer. The exact bound for each outbox is Q-10. AC-17 uses a test tolerance.
- **(d) Count per topic.** The outbox offers the per-topic capability. With no concurrent writers, it returns exactly `min(S_t, C)` for each requested topic t, counting only messages stale on t at the requested A_t, and 0 for a requested topic with no such message. It counts each requested topic this way whatever `args` holds: it never narrows the requested topics by `args`, which it receives for other uses (FR-12). It answers every requested topic, excludes no tripped topic, transfers no message body, loads at most C entries per topic into the process or counts on the server, and meets (c) for its own token.

The table below is from reading the code at commit 1ce331eb2, with the relational (c) cells updated for PR #4613. AC-16, AC-17 and AC-34 settle the "To verify" cells.

| Outbox | (a) Age | (b) Ceiling | (c) Cancellation | (d) Count per topic | Notes |
|---|---|---|---|---|---|
| `InMemoryOutbox` | Meets | Meets | Not applicable: no I/O | Added in this spec; AC-34 verifies it | PR #4583 removed the sort each count did (issue #4554) |
| MsSql, PostgreSql, MySql | Meets | Meets | MsSql meets: AC-17 against a table held with `TABLOCKX` ended the count within 5 s of the cancellation, before the 15 s command timeout (A-9). PostgreSql meets: AC-17 against a table held with `ACCESS EXCLUSIVE` ended the count within 5 s of the cancellation. MySql meets: AC-17 against a table held with `LOCK TABLES ... WRITE` ended the count within 5 s of the cancellation | **Gap.** Not in this spec; one issue for the relational family (FR-14.1) | Loads up to C whole rows |
| Sqlite | Meets | Meets | **Gap.** AC-17 against a store held with `BEGIN EXCLUSIVE` found the count still running 10 s after the cancellation: the command gets the token (`RelationDatabaseOutbox.cs:1239`), but Microsoft.Data.Sqlite runs the blocked step synchronously (A-8), so the token does not interrupt the lock | **Gap.** Relational family | Loads up to C whole rows |
| Spanner | Meets: AC-16 against the emulator, binary and text, returned 5 for A of 5 minutes | Meets: AC-16 against the emulator, binary and text, returned 3 for C of 3 and 5 for C of 5 | Meets: AC-17 against the emulator, binary and text, ended the count with `OperationCanceledException`; the table is not held on the emulator, so the check is the token, not the 15 s command timeout (A-9) | **Gap.** Relational family; `SpannerOutbox` derives from `RelationDatabaseOutbox` (`SpannerOutbox.cs:32-33`) | Loads up to C whole rows |
| MongoDb | Meets, but narrowed to `args["Topic"]` when the bag sets a topic (FR-12) | Meets | Meets: AC-17 against a MongoDb outbox ended the count within 5 s of the cancellation; the store is not held, so the token ends the count with `OperationCanceledException` | **Gap.** Not in this spec; one issue | Loads up to C whole documents. Its overall count already filters on one topic |
| DynamoDB, DynamoDB V4 | Meets, on an eventually consistent index (A-7): AC-16 against a table of its own returned 5 for A of 5 minutes, after the 10 s retry | **Gap.** C is split across `ScanConcurrency` segments, and each stops at its share. With C of 100 and 3 segments (shares 34, 33, 33) and S of 100 spread 50, 30, 20, V is 84 (`DynamoDbOutbox.cs:851-924`, `:974`; V4 `:863`, `:986`). Confirmed by AC-16: a C of 1 or 2, below `ScanConcurrency`, gives a segment a share of 0, so it sends a Scan with `Limit = 0` (`:906`) and the count throws `AmazonDynamoDBException` ("Limit must be greater than or equal to 1"). A C of 3 returned 2 and a C of 5 returned 4 on 9 messages with 5 stale, in both projects. | Meets: AC-17 against a DynamoDB outbox ended the count within 5 s of the cancellation; the table is not held, so the token ends the count with `OperationCanceledException` (the token is linked with the outbox's own timeout) | **Gap.** Not in this spec; one issue for both | `Select.COUNT`, so no bodies are read. The age filter is applied after the read, so read volume is not bounded by C (NFR-4) |
| Firestore | **Gap.** Filters only on the dispatched flag; AC-16 against the emulator returned 7 where 5 was expected | **Gap.** No limit; AC-16 against the emulator returned 7 for C of 3 and for C of 5 | **Gap.** The token is not passed to the aggregation query or its stream (`FirestoreOutbox.cs:1191-1246`); AC-17 against the emulator found the count finishing without an exception after the cancellation | **Gap.** Not in this spec; one issue | The count runs on the server |

**FR-14.1 — Every gap is closed or tracked before release.**
Each Gap cell, including "To verify" cells that turn out to be gaps, is either closed in this spec with its AC passing, or tracked in a linked GitHub issue and listed as a known limitation in the documentation. For columns (a) to (c), Q-9 decides which, gap by gap. For column (d) the gaps are tracked, one issue per family: the relational outboxes (MsSql, PostgreSql, MySql, Sqlite and Spanner), MongoDb, DynamoDB and DynamoDB V4, and Firestore (Q-15). Each family's known limitation states that the outbox reports the overall count only (FR-12.1).

**FR-15 — The gauges' attributes are fixed and few.**
- Each gauge has the same attribute set on every measurement.
- Both gauges carry an attribute that identifies the outbox implementation. The overall gauge carries no topic attribute, and a process has at most one overall series per measured outbox, which with one outbox per process (C-8) means one.
- The per-topic gauge carries the same attributes plus `messaging.destination.name` (`BrighterSemanticConventions.MessagingDestination`, `BrighterSemanticConventions.cs:90`), set to the topic's value, as `OutboxMeter` tags its instruments (`OutboxMeter.cs:135`). It has exactly one series per measured topic, so the number of its series is fixed by configuration when measurement starts (FR-3.1) and grows linearly with the number of measured topics; no limit on that number is set (NFR-4 (b)).
- No attribute on either gauge, or on the FR-6 signal, carries a message id, correlation id or other per-message value. The FR-6 signal carries no topic.
- The implementation attribute's name is for the ADR; `BrighterSemanticConventions.OutboxType` (`paramore.brighter.outbox.type`, `BrighterSemanticConventions.cs:114`) is an existing candidate. Per-instance identity is Q-8.

### Non-functional Requirements

**NFR-1 — Measurement adds nothing to the posting path.**
Enabling the feature adds no work, outbox call, lock or wait to a posting-path call, and the posting path does not read A_p. If Q-2 is answered by adding an on-call trigger, that trigger is the only exception to this requirement, including the rest of this paragraph: it is a non-blocking signal that does no I/O, takes no lock that measurement or the outstanding-message check can hold while doing I/O, and never waits, and the ADR names it. Apart from the trigger's own signal, measurement shares no lock, semaphore or flag with the posting path, in particular not `_outstandingCheckInFlight` (`OutboxProducerMediator.cs:87`), the flag that keeps at most one outstanding-message check queued or running. PR #4597 removed `s_checkOutstandingSemaphoreToken`, the static semaphore the check used to share across mediators of the same types (issue #4584). A blocked measurement of either count does not delay posting. A blocked outstanding-message check does not delay a measurement.

**NFR-2 — The outstanding-message check is unchanged.**
With or without the feature, the check keeps its trigger, interval, count arguments, result and `OutboxLimitReachedException` behaviour. Measurement neither reads nor writes the mediator's outstanding count.

**NFR-3 — Public API changes are additive, and existing meter roles and outbox interfaces get no new members.**
No member is added to, removed from or changed on `IAmABrighterMessagingMeter`, `IAmABrighterDbMeter`, `IAmABrighterSweeperMeter` or `IAmABrighterOutboxMeter` (`IAmABrighterOutboxMeter.cs:40`). The maintainer stated the rule for `IAmABrighterSweeperMeter` on issue #4573: "it is a public role, and a new member would break anyone who implements it". This spec applies it by analogy to the other three meter roles, because the same reason holds for them. No member is added to `IAmAnOutbox`, `IAmAnOutboxSync` or `IAmAnOutboxAsync`; the count per topic is a new optional capability instead. A member is added to `IAmABrighterTracer`, `IAmAnOutboxProducerMediator` or `IAmProducersConfiguration` only as Q-4 answers. `Publication` gains exactly one public member, the nullable A_p; `Publication` is a concrete class, so code that constructs it or derives from it compiles and behaves as before. New types, optional capability interfaces (as `IHaveAChannelFailureCount` is), and optional constructor parameters with defaults are allowed.

**NFR-4 — Each measurement makes one bounded call, and a due time at most two.**
- **(a)** Each measurement makes exactly one outbox call and retries none. A due time has at most two measurements, so at most two outbox calls: one overall and, unless FR-12.1 leaves it out, one per-topic that answers every measured topic. If Q-2 adds an on-call trigger, a triggered run covers both counts, with at most one triggered measurement of each count per I. Measurement makes no other outbox call; in particular it never lists messages (`OutstandingMessagesAsync`) to count them.
- **(b)** The overall request is limited to C. The per-topic request is limited to C for each topic, so its answer holds at most C per measured topic, and an outbox that offers the capability loads at most C entries per topic or counts on the server (FR-14 (d)). The per-topic cost therefore grows linearly with the number of measured topics: up to C entries for each. No limit on the number of measured topics is set; the documentation explains the trade-off (NFR-7). Firestore does not meet this for the overall count (FR-14). DynamoDB limits the count it returns but not the items it reads. A large backlog of young outstanding messages is read in full over many round trips, and against the outbox's 500 ms default timeout (A-7) that can make every measurement fail.
- **(c)** No message body is transferred to produce either count. For the per-topic count this is binding, through FR-14 (d). For the overall count, the relational outboxes and MongoDb do not meet it: a comment on issue #4489 reports 100 rows of 200 KB allocating about 60 MB, while `GetNumberOfOutstandingMessages` (`RelationDatabaseOutbox.cs:769`) runs `COUNT(1)`. For the overall count, (c) is binding if Q-5 is answered yes; otherwise it is a known limitation under FR-14.1.

**NFR-5 — Measurement depends on neither a metrics pipeline nor tracing.**
Measurement and recording go through `System.Diagnostics.Metrics` and `IMeterFactory` only, and use no OpenTelemetry SDK types. The only exception is registration code inside `AddBrighterInstrumentation`, which already works on OpenTelemetry's `MeterProviderBuilder`, if Q-6 places the instruments there. With no metrics configured, measurements behave as FR-7 to FR-13 state (subject to Q-11), and nothing throws. Turning tracing off, or sampling every span out, changes no recorded value.

**NFR-6 — Measurement's own time is testable with a fake clock.**
Measurement's due times, timeout and stop bound come from an injectable `TimeProvider`. Ages are the outbox's (A-2) and are not covered by this requirement. Both gauges can be observed through a `MeterListener` over an `IMeterFactory` from `ServiceCollection.AddMetrics()`, as the tests of ADR `0081-timed-outbox-sweeper-health-metrics` do.

**NFR-7 — The feature is documented.**
Every new public type and member has XML documentation that follows `.agent_instructions/documentation.md`. The telemetry page in BrighterCommand/Docs covers:
- what each count measures, and that the overall value is not the sum of the per-topic values;
- the settings, A_p among them, their defaults and their maximums;
- that a topic uses the smallest age of its publications (FR-3.1);
- that a topic with no publication in this process appears in the overall count only;
- that a message counts toward its header topic, so a message whose header topic differs from its publication's topic is in the per-topic count only if a publication in this process uses that header topic (FR-3.1), as a known limitation;
- that a value equal to C means "C or more", for each topic too;
- that the per-topic count's cost and its number of series grow linearly with the number of measured topics, up to C entries per topic (NFR-4 (b)), and how to choose C with that in mind;
- how to read "no recent reading" for each count (FR-6);
- that instances sharing a store are combined with max, not sum, and per topic for the per-topic gauge (A-6);
- that MongoDb's overall count covers only the bag's topic when the bag sets one (FR-12);
- which outboxes offer the count per topic, and that the others report the overall count only (FR-12.1);
- how the per-topic gauge shows a topic whose circuit breaker is open, and that topics are tripped only when the app registers an outbox circuit breaker;
- that the instruments report only when the meter provider shares the application's container, so a provider built with `Sdk.CreateMeterProviderBuilder()` is not supported;
- an example alert;
- how to choose A and A_p when a sweeper runs (A-3);
- each outbox's known limitations.

The release notes describe the feature.

### Constraints and Assumptions

- **C-1.** The value is recorded directly, never derived from spans. The reason is the one ADR `0081-timed-outbox-sweeper-health-metrics` gives: sampling drops spans.
- **C-2.** Public API changes are additive only (NFR-3).
- **C-3.** The outstanding-message check is not changed (NFR-2). PR #4597 has fixed the semaphore shared across mediators that issue #4584 reported, and PR #4613 has bounded the relational count with a command timeout (A-9). Neither change is part of this spec.
- **C-4.** `TimedOutboxSweeper`, its options, `IAmABrighterSweeperMeter` and the sweeper instruments are not changed.
- **C-5.** Names follow ADR `0010-brighter-semantic-conventions` and are added to `BrighterSemanticConventions`.
- **C-6.** A third-party outbox is measured through the same count and gets whatever accuracy its count provides. It gets a per-topic count only if it offers the per-topic capability (FR-12.1).
- **C-7.** When the feature is not enabled, behaviour is identical to today.
- **C-8.** Measurement covers one outbox per process: the one registered as `IAmAnOutbox` (Terms, Outbox). Measuring several outboxes in one process is not part of this spec, and FR-15's single overall series rests on this.

- **A-1. Reference time per outbox.**
  - `InMemoryOutbox`: the time the entry was added (`WriteTime`, `InMemoryOutbox.cs:150`).
  - Relational outboxes: the `Timestamp` column, taken from `Header.TimeStamp` (`RelationDatabaseOutbox.cs:1456`).
  - MongoDb and Firestore: `TimeStamp` from the header.
  - DynamoDB: `OutstandingCreatedTime`, from `Header.TimeStamp`, or `DateTimeOffset.UtcNow` if that is unset (`MessageItem.cs:229-239`), which is the system clock rather than the outbox's `TimeProvider`.
  - Firestore is the exception: its count compares no timestamp at all, which is its FR-14 (a) gap.
- **A-2. Each outbox's clock.**
  - `InMemoryOutbox`, MongoDb, Firestore and both DynamoDB outboxes use their configured `TimeProvider`.
  - The relational outboxes use `DateTimeOffset.UtcNow` (`RelationDatabaseOutbox.cs:1412`, `MsSqlOutbox.cs:100`, `SpannerOutbox.cs:87`).
  - For a store shared across processes, a clock skew of d shifts every age by d. Clocks are assumed to agree within seconds, and A and every A_p to be minutes.
- **A-3. Choosing A and A_p is the app's job.** A message that legitimately waits longer than A, or than its topic's A_t, counts as stale. When a sweeper runs, A and every A_p should exceed its `MinimumMessageAge` plus `TimerInterval` (Q-13).
- **A-4.** A message the broker accepted but the outbox never marked as dispatched is counted, as the sweeper would resend it.
- **A-5.** Messages in uncommitted transactions are not visible to either count.
- **A-6.** Each instance sharing a store counts the same rows, so summing their values overcounts and max does not. For the per-topic gauge, max is taken per topic. This holds when the instances pass the same bag and, for a topic, use the same A_t; an instance with a smaller A_t counts more, so max reports the count at the smallest A_t among the instances, and an instance that does not publish to a topic reports no series for it. MongoDb instances with different topics in their bags count different sets in the overall count (FR-12). Each `InMemoryOutbox` reports only its own process.
- **A-7.** DynamoDB scans `OutstandingAllTopics` with `ConsistentRead = false` and has its own operation timeout (`DynamoDbConfiguration.Timeout`, 500 ms by default). When T is greater than that timeout, a count that exceeds it fails before T applies.
- **A-8.** Microsoft.Data.Sqlite runs its async ADO.NET methods synchronously.
- **A-9.** Since PR #4613, the relational count runs with a command timeout of 15 seconds by default, Spanner included. The app can set it with `outstandingCountTimeout` on `RelationalDatabaseConfiguration`, rounded up to whole seconds. These locations refer to commit 62099d908 on master: `RelationDatabaseOutbox.cs:1015` and `:1021` (the count), `RelationalDatabaseConfiguration.cs:13` (the default), `:38` (the parameter) and `:114` (the rounding), and `SpannerOutbox.cs:57` (60 seconds only for a timeout of 0 or less).

### Out of Scope

- **OOS-1.** The channel that notifies each failed `Clear(Async)`, and any option that makes `PostAsync` throw.
- **OOS-2.** Alerting rules, beyond one documented example.
- **OOS-3.** Changes to the sweeper, its schedule, its lock or its metrics.
- **OOS-4.** Changes to the outstanding-message check or `MaxOutStandingMessages`. Work on issue #4584 stays outside this spec: PR #4597 removed the semaphore shared across mediators, and PR #4613 bounded the relational count with a command timeout (A-9).
- **OOS-5.** Per-message detail in either gauge (message id, correlation id and the like); a topic attribute on the overall gauge or on the FR-6 signal; and per-topic series for topics with no publication in this process.
- **OOS-6.** A health check for stale messages.
- **OOS-7.** Sending, retrying, deleting or archiving stale messages.
- **OOS-8.** The inbox.
- **OOS-9.** Accuracy guarantees for third-party outboxes.
- **OOS-10.** The correction to the Post section of the Outbox Support docs raised on issue #4489.
- **OOS-11.** An instrument for the age of the oldest stale message.
- **OOS-12.** Aligning the outboxes' behaviour at an age of exactly A or A_t.
- **OOS-13.** A per-topic stale age set anywhere other than `Publication`, for example per topic in the configuration step.
- **OOS-14.** The circuit breaker's state as a metric, such as which topics are tripped.
- **OOS-15.** Implementing the per-topic capability on the outboxes other than `InMemoryOutbox`. FR-14.1 tracks each family in an issue of its own.

## Acceptance Criteria

**Setup unless an AC says otherwise:**
- **Clock.** A `FakeTimeProvider` at 2026-01-01T12:00:00Z, and measurement starts at 12:00:00.
- **Settings.** A of 5 minutes, C of 100, I of 1 minute, T of 10 seconds.
- **Publications.** No publication is configured (an empty `ProducerRegistry`), so no topic is measured and only the overall count runs (FR-12.1). Where an AC adds publications, each has a distinct CloudEvents type unless the AC says otherwise. In an AC that names no count, "the gauge", "V", "value" and "call" refer to the overall count.
- **Listener.** A `MeterListener` over `AddMetrics()`, collecting every 10 seconds of fake time.
- **Instrument creation.** However Q-6 is answered: if the instruments are created by `AddBrighterInstrumentation`, the setup includes it; if they are created directly through `IMeterFactory`, it does not need to. The ADR states which, and the listener-based ACs use that setup.
- **Outbox.** An `InMemoryOutbox` on the same clock.
- **Logger.** A test logger that captures entries.
- **Counting outbox.** A double that implements both outbox interfaces and the per-topic capability; on request it leaves the capability out. It records each call of either count, and each call of its synchronous count, with its arguments and token. On request, for either count, it can block asynchronously until released, block the calling thread before returning a task, throw, return a chosen result, or ignore cancellation. It can also block its synchronous count, which the outstanding-message check calls because the double implements `IAmAnOutboxSync` (`OutboxProducerMediator.cs:1431-1442`), as the existing `CountingOutbox` test double holds its counts (`CountingOutbox.cs:58`, `:89-94`).
- **Synchronisation.** After each `Advance`, the test waits, with a bounded wall-clock wait of 5 seconds, until every call that FR-7 and FR-8 expect to start at that instant has been recorded by the counting outbox, and until every outcome that FR-6 and FR-10 expect at that instant has been recorded: the value or outcome of every measurement that has returned, every abandonment at T, and every skip with its log entry. The test fails rather than hangs if either has not happened. A due time that FR-8 skips makes no call, so only its skip outcome is waited for. The same wait applies to the ACs that use the `InMemoryOutbox`, waiting on the measurement's outcome rather than on a recorded call. T is measured from the due time handled on the timer callback, so the instant at which a token is cancelled (for example 12:01:10 for the 12:01:00 call) does not depend on when a thread-pool thread runs.

**AC-1 (FR-1).** **Given** `AddProducers` with the counting outbox (`MaxOutStandingMessages = -1`), a publication on "orders" with A_p of 1 minute, `AddBrighterInstrumentation`, and no stale step, **When** fake time advances 10 minutes, **Then** the counting outbox records no call of either count and the listener receives no value from either FR-5 gauge. **And** with the step added, at least one call of each count is recorded and at least one value is received from each gauge.

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

**When** the listener collects at each 10-second step from 12:02:10 to 12:03:30, **Then** at every collection, what the listener receives in run 1 differs from run 2 through the signal the ADR chose. In neither run does the overall gauge report a value other than 0. **And** the same holds when run 1's count, from 12:02:00 on, blocks and honours cancellation, so each call is abandoned at T; in that variant the collections run at 12:02:15, 12:02:25 and so on, 5 seconds off the abandonment instants.

**AC-8 (FR-7).** **Given** the counting outbox returning 0 at once, **When** time advances in 1-second steps to 12:03:30, **Then** exactly 3 calls were made, at 12:01, 12:02 and 12:03, whether the listener collects every 10 seconds or every 1 second. **And with** publications on "orders" and "payments", exactly 3 per-topic calls were also made, at the same times. The coalescing of FR-7's third bullet is not tested here: a fake clock fires each due time in turn when it advances, so it does not produce a missed due time reliably. The result would not be deterministic with a fake clock. It is checked by reviewing the scheduling code against the ADR, which states how the scheduler computes the next grid point itself (for example a one-shot timer set to the next start + k × I), rather than relying on a periodic timer's own catch-up behaviour.

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

The synchronous count is never called. **And with** no bag configured, `args` is an empty dictionary, as the check passes. **And with** publications on "orders" (A_p of 2 minutes) and "payments" (no A_p), the per-topic capability is called 3 times, once per due time, each with exactly those two topics and their ages (2 and 5 minutes), C as the ceiling, `args` with the same entries as the bag, `requestContext` null and a cancellable token.

**AC-15 (FR-13).** **Given** a count that blocks asynchronously from 12:01:00 and ignores cancellation, **When** a stop is requested at 12:01:05, inside T, **Then** all of these hold:
- the token is cancelled at 12:01:05;
- stopping has returned once fake time reaches 12:01:15;
- no call starts afterwards, even 10 minutes later;
- nothing is logged as a failure.

**And with** a count that honours cancellation, stopping returns without fake time advancing. **And with** a publication on "orders" and both counts blocked from 12:01:00, ignoring cancellation, both tokens are cancelled at 12:01:05, stopping has returned by 12:01:15, and no call of either count starts afterwards.

**AC-16 (FR-3, FR-4, FR-14 (a)(b)).** **Given** each outbox in scope against its real store or emulator, holding 9 messages:
- 5 undispatched, with a reference time 10 minutes old;
- 2 with the same reference time, dispatched;
- 2 undispatched, with a reference time 1 minute old.

Reference times are relative to the outbox's own clock. For every outbox the test sets `Header.TimeStamp` on each message: relative to real time for the relational outboxes, whose clock is `DateTimeOffset.UtcNow` (A-2); relative to the `FakeTimeProvider` for `InMemoryOutbox`, which also adds each message at that fake time, because its reference time is when the entry was added (A-1); and relative to the configured `TimeProvider` for the others. **When** the asynchronous count runs with A of 5 minutes, **Then** it returns 5 for C of 100 (the (a) age assertion), and 3 for C of 3 and 5 for C of 5 (the (b) ceiling assertions). For DynamoDB, whose index is eventually consistent (A-7), each count is retried until it equals the expected value or 10 seconds pass. For a column marked Meets, the assertion then fails as usual; recording the observed value under AC-18 applies only to columns marked Gap or "To verify". An outbox whose FR-14 cell for a column is Gap is exempt from that column's assertion, and its observed result is recorded under AC-18. **And for** DynamoDB and DynamoDB V4 with the default `ScanConcurrency` of 3, the count also runs once with C of 1 and once with C of 2, below the scan concurrency, with no expected value and no retry. Whether it returns, fails or undercounts is recorded under AC-18, which settles the FR-14 "To verify" cell for a C below `ScanConcurrency`.

**AC-17 (FR-14 (c)).** **Given** each outbox other than `InMemoryOutbox`, with the store kept from answering for at least 30 seconds, **When** the token is cancelled 1 second after the call starts, **Then** the call ends with `OperationCanceledException`, or the client's cancellation exception, within 5 seconds of the cancellation.
- The 5 seconds is a test tolerance, not a contract (Q-10).
- For the relational outboxes, a second connection holds an exclusive table lock.
- For DynamoDB and DynamoDB V4, the configuration is constructed with `timeout: 0` (no outbox timeout), so the outbox's own 500 ms default (A-7) does not end the call before the token is cancelled.
- Where the store cannot be held (for example DynamoDB Local, MongoDb, the Spanner emulator or the Firestore emulator, unless the test environment offers a way), the test uses a token cancelled before the call, plus a review showing that the token reaches the client.

**AC-18 (FR-14.1).** **Given** the FR-14 table after AC-16, AC-17 and AC-34, **When** the feature is prepared for release, **Then** every Gap, including column (d) of every outbox other than `InMemoryOutbox`, is either closed, with its case passing, or linked to an open issue and listed under that outbox's known limitations. Column (d) has one issue per family named in FR-14.1.

**AC-19 (FR-15).** **Given** publications on "orders", "payments" and "refunds", and messages with 10 ids and 10 correlation ids spread over those 3 topics, **When** 10 overall and 10 per-topic measurements complete, **Then**:
- **overall gauge:** every value carries the same attribute set, which identifies the outbox implementation and has no `messaging.destination.name`; no attribute value equals a message id, topic or correlation id; and there is one series;
- **per-topic gauge:** there are exactly 3 series; each carries the overall gauge's attribute set plus `messaging.destination.name` equal to its topic, and nothing else; no attribute value equals a message id or correlation id;
- **FR-6 signal:** no attribute value equals a topic, a message id or a correlation id.

**AC-20 (NFR-1).** **Given** a publication on "orders" and both counts blocked indefinitely, once asynchronously and once by blocking the thread, **When** the app calls `Post` 100 times and `PostAsync` 100 times, **Then** every call returns (with a 30-second wall-clock guard), and the counting outbox records the same posting-path calls as with the feature disabled. **And given** `MaxOutStandingMessages` of 10, with the check's count blocked (calls of the synchronous count with `maxCount` 11, which the check makes because the counting outbox implements `IAmAnOutboxSync`), **then** the next stale measurement of each count starts at its due time.

**AC-21 (NFR-2).** **Given** `MaxOutStandingMessages` of 5, **When** the same posts run with the feature enabled and disabled, **Then**:
- the check's count calls carry the same `dispatchedSince`, `maxCount` and `args` in both runs;
- after each post the test waits until the check queued by that post has finished, using a bounded wall-clock wait that polls with a deadline, as `CountingOutbox.WaitForOutstandingCountCallsAsync` does (`CountingOutbox.cs:78-87`);
- with that wait, `OutboxLimitReachedException` is first thrown on the same post in both runs;
- the existing check tests pass unchanged.

**AC-22 (NFR-3).** **Given** the pull request diff, **Then** all of these hold:
- no meter role listed in NFR-3 is changed;
- `IAmAnOutbox`, `IAmAnOutboxSync` and `IAmAnOutboxAsync` gain no member;
- `Publication` gains exactly one public member, the nullable A_p, and loses or changes none;
- any member added to another existing interface matches the answer to Q-4;
- every other public change is a new type (the per-topic capability among them), an optional capability interface, or an optional parameter with a default.

**AC-23 (NFR-4).** **Given** the counting outbox, C of 25, and publications on "orders" and "payments", **When** 5 due times each complete both counts, **Then** exactly 5 overall calls were made, each with `maxCount` 25, exactly 5 per-topic calls were made, one per due time, each with both topics and a ceiling of 25 for each, so that no per-topic answer can hold more than 50, and no other outbox call came from measurement; in particular `OutstandingMessagesAsync` was never called. **And with** the capability left out, exactly 5 calls were made, all overall. Body-transfer and read-volume gaps are recorded under AC-18.

**AC-24 (NFR-5).** **Given** a publication on "orders", no `AddBrighterInstrumentation` and no listener, **When** the app runs 5 intervals and stops, **Then** nothing throws and nothing is logged as a failure. **And** new measurement and recording code references no `OpenTelemetry` namespace; registration code inside `AddBrighterInstrumentation` is exempt (NFR-5).

**AC-25 (NFR-6).** **Given** AC-1 to AC-15, AC-19 to AC-21, AC-23, AC-24, AC-27 to AC-33 and AC-35 to AC-40, **Then** they drive measurement's time through `FakeTimeProvider` and observe values through `MeterListener`. The wall-clock guards in AC-11, AC-20, AC-36 and AC-37, and the setup's synchronisation wait, only detect hangs or wait for calls already due; they do not drive time.

**AC-26 (NFR-7).** **Given** the merged feature, **Then** every new public type and member has XML documentation, the telemetry page covers each item in NFR-7, and the release notes describe the feature.

**AC-27 (FR-3, FR-3.1, FR-5).** **Given** publications P1 on "orders" with A_p of 2 minutes and P2 on "payments" with no A_p, and the `InMemoryOutbox` receiving:
- o1 on "orders" at 12:00:00, not dispatched;
- o2 on "orders" at 12:03:30, not dispatched;
- o3 on "orders" at 12:00:00, dispatched at 12:00:01;
- p1 on "payments" at 12:00:00, not dispatched;
- p2 on "payments" at 12:03:30, not dispatched.

**When** time advances to 12:06:30, **Then** the 12:06:00 measurements record V of 2 (o1, p1) on the overall gauge, and on the per-topic gauge V_t of 2 for "orders" (o1, o2) and 1 for "payments" (p1). **And** the 12:01:00 measurements recorded 0 on the overall gauge and 0 for each topic, as values. **And** at 12:00:30 neither gauge has a value.

**AC-28 (FR-3.1).** **Given** A of 5 minutes, publications P1 on "orders" with A_p of 2 minutes, P2 on "orders" with A_p of 4 minutes and P3 on "orders" with no A_p (three CloudEvents types on one topic), and P5 with an empty topic (`RoutingKey.Empty`) and A_p of 1 minute, **When** the 12:01:00 due time is handled with the counting outbox, **Then** the per-topic call carries exactly one topic, "orders", with an age of 2 minutes. **And given** the `InMemoryOutbox` instead, holding one undispatched message on "orders" added at 12:03:30, **then** the 12:06:00 measurements record V_t of 1 for "orders" and V of 0.

**AC-29 (FR-6, FR-12.1).** **Given** the counting outbox with the capability left out, and publications on "orders" and "payments", **When** measurement starts and fake time advances to 12:05:30, **Then** all of these hold:
- one Information entry, logged at start, names the outbox type and says the outbox has no count per topic, and no other entry about it is logged;
- the overall gauge records a value for each due time from 12:01 to 12:05;
- at every collection, neither the per-topic gauge nor the per-topic count's FR-6 signal has a series.

**And given** the counting outbox with the capability and, in turn, no producer registry available to measurement, however the ADR reads it (FR-3.1; `AddProducers` itself rejects a null registry), an empty `ProducerRegistry` and a registry whose only publication has an empty topic (`RoutingKey.Empty`), **then** the same holds, the entry gives "no measured topic" as the reason, and the capability is never called. **And given** the capability left out and an empty `ProducerRegistry`, so that both reasons apply, **then** exactly one entry is logged at start, and it gives "no capability" as the reason.

**AC-30 (FR-3.1, FR-15).** **Given** a publication on "orders" only, and the `InMemoryOutbox` holding a1 on "audit" and o1 on "orders", both added at 12:00:00 and not dispatched, **When** time advances to 12:06:30, **Then** the 12:06:00 measurements record V of 2 on the overall gauge, and the per-topic gauge has exactly one series, `messaging.destination.name` "orders", with V_t of 1. No series carries "audit".

**AC-31 (FR-4).** **Given** C of 3, publications on "orders", "payments" and "refunds", and the `InMemoryOutbox` holding 50 undispatched messages on "orders" and 2 on "payments", all added at 12:00:00, and none on "refunds", **When** time advances to 12:06:30, **Then** the 12:06:00 measurements record V_t of 3, 2 and 0 for "orders", "payments" and "refunds", and V of 3.

**AC-32 (FR-2).** **Given** A of 5 minutes and, in turn, a publication on "orders" with each of these A_p: zero; -1 second; A's maximum plus one tick; `TimeSpan.MaxValue`, **When** measurement starts, **Then** a `ConfigurationException` naming "orders" and the value is thrown when measurement starts and surfaces from starting it rather than being swallowed, and no call of either count is made. **And** the same holds for an A_p of zero on a publication with an empty topic (`RoutingKey.Empty`), with the exception naming the value. **And given** an A_p of 1 ms, of A's maximum, or of 10 minutes, **then** measurement starts without an exception. **And given** a null A_p, **then** the per-topic call carries "orders" with an age of 5 minutes.

**AC-33 (FR-4, FR-6, FR-8, FR-9, FR-10).** **Given** publications on "orders" and "payments" and a counting outbox whose overall count returns 1 at once, **When** the per-topic call blocks asynchronously from 12:01:00, ignores cancellation and is released at 12:03:30, and time advances to 12:04:30, **Then** all of these hold:
- the overall gauge records 1 for each due time from 12:01 to 12:04;
- the per-topic call's token was cancelled at 12:01:10, and no per-topic value was recorded for 12:01;
- no per-topic call started at 12:02 or 12:03, and a per-topic call started at 12:04:00;
- at the collections at 12:02:20 and 12:03:20, the per-topic count's FR-6 signal shows that no per-topic measurement has completed since measurement started, and the overall count's signal does not show "no recent reading".

**And given** instead an overall count that throws `InvalidOperationException` at every due time and a per-topic call that returns 2 for "orders" and 0 for "payments", **then** the per-topic gauge reports 2 and 0, the overall count's signal shows "no recent reading" while the per-topic count's does not, and each failure entry names the overall count. **And given** a per-topic call that returns null, a count for "orders" only, or -1 for "payments", **then** that per-topic measurement fails, no V_t is recorded for either topic for that due time, and the overall value of that due time is recorded. **And given** a per-topic call that returns 2 for "orders", 0 for "payments" and 5 for "audit", which was not requested, **then** the measurement completes, the per-topic gauge reports 2 and 0, and no series carries "audit". **And given** C of 100, an overall count that returns 150 and a per-topic call that returns 150 for "orders" and 0 for "payments", **then** V of 100 is recorded, and V_t of 100 for "orders" and 0 for "payments".

**AC-34 (FR-12, FR-14 (d)).** **Given** an `InMemoryOutbox` on a `FakeTimeProvider`, holding:
- on "orders": 5 undispatched messages 10 minutes old, 2 dispatched messages 10 minutes old, and 2 undispatched messages 1 minute old;
- on "payments": 1 undispatched message 3 minutes old;
- on "audit": 4 undispatched messages 10 minutes old.

**When** the per-topic capability is called with "orders" at 5 minutes, "payments" at 2 minutes and "refunds" at 5 minutes, **Then** it returns 5 for "orders", 1 for "payments" and 0 for "refunds" with C of 100, and 3, 1 and 0 with C of 3, and its result has no entry for "audit". **And with** "orders" at 30 seconds and C of 100, it returns 7 for "orders". **And with** `args` of `{"Topic": "orders"}` and C of 100, it still returns 5 for "orders", 1 for "payments" and 0 for "refunds": `args` does not narrow the requested topics.

**AC-35 (FR-1, FR-2).** **Given** `AddProducers` with the counting outbox, a publication on "orders" with A_p of zero, and no stale step, **When** the app starts and fake time advances 10 minutes, **Then** nothing throws, no `ConfigurationException` is raised for the A_p, and the counting outbox records no call of either count.

**AC-36 (FR-9).** **Given** publications on "orders" and "payments", and a counting outbox whose `GetOutstandingMessageCountAsync` blocks the calling thread on a wait handle before returning any task, ignoring the token, while its per-topic call returns 3 for "orders" and 0 for "payments" at once, **When** fake time advances in 1-second steps from 12:00:59 to 12:01:30, **Then** all of these hold:
- every `Advance` call returns; the test fails, rather than hangs, if one has not returned within 5 seconds of wall-clock time;
- the per-topic call started at 12:01:00, and V_t of 3 for "orders" and 0 for "payments" was recorded for 12:01 before fake time passed 12:01:10;
- the overall call's token was cancelled at 12:01:10, and no overall value was recorded for 12:01.

**AC-37 (FR-9).** **Given** publications on "orders" and "payments", and a counting outbox whose per-topic call blocks the calling thread on a wait handle before returning any task, ignoring the token, while its `GetOutstandingMessageCountAsync` returns 1 at once, **When** fake time advances in 1-second steps from 12:00:59 to 12:01:30, **Then** all of these hold:
- every `Advance` call returns; the test fails, rather than hangs, if one has not returned within 5 seconds of wall-clock time;
- the overall call started at 12:01:00, and V of 1 was recorded for 12:01 before fake time passed 12:01:10;
- the per-topic call's token was cancelled at 12:01:10, and no V_t was recorded for 12:01.

**AC-38 (FR-3.1, FR-15).** **Given** publications on "orders" and "payments", and the `InMemoryOutbox` holding, both added at 12:00:00 and not dispatched:
- m1 with header topic "orders.v2" and the `ProducerTopic` bag entry "orders", as `WrapPipeline` stores it when a mapper changes the topic;
- o1 on "orders".

**When** time advances to 12:06:30, **Then** the 12:06:00 measurements record V of 2, and V_t of 1 for "orders" (o1 only) and 0 for "payments", and the per-topic gauge has no series for "orders.v2". **And with** a third publication on "orders.v2", **then** the 12:06:00 measurements record V_t of 1 for "orders.v2" and still 1 for "orders".

**AC-39 (FR-3.1).** **Given** a publication P1 on "orders" with A_p of 2 minutes and the counting outbox, **When** at 12:01:30 the test sets P1's `Topic` to "payments" and its A_p to 4 minutes, and time advances to 12:03:30, **Then** the per-topic calls at 12:02:00 and 12:03:00 each carry exactly one topic, "orders", with an age of 2 minutes, as the 12:01:00 call did, and nothing throws.

**AC-40 (FR-3, FR-14 (d)).** **Given** an `IAmAnOutboxCircuitBreaker` registered with "orders" tripped, publications on "orders" and "payments", and the `InMemoryOutbox` holding 2 undispatched messages on "orders" and 1 on "payments", all added at 12:00:00, **When** time advances to 12:06:30, **Then** the 12:06:00 measurements record V_t of 2 for "orders" and 1 for "payments" on the per-topic gauge, and V of 3 on the overall gauge. **And given** the counting outbox with the same breaker and publications, **then** the 12:01:00 per-topic call carries both "orders" and "payments" while "orders" is tripped.

### Requirement → Acceptance Criteria map

| Requirement | AC | Requirement | AC |
|---|---|---|---|
| FR-1 | AC-1, AC-35 | FR-12.1 | AC-29 |
| FR-2 | AC-2, AC-3, AC-32, AC-35 | FR-13 | AC-11, AC-15 |
| FR-3 | AC-4, AC-16, AC-27, AC-40 | FR-14 | AC-16, AC-17, AC-34, AC-40 |
| FR-3.1 | AC-27, AC-28, AC-30, AC-38, AC-39 | FR-14.1 | AC-18 |
| FR-4 | AC-5, AC-16, AC-31, AC-33 | FR-15 | AC-19, AC-30, AC-38 |
| FR-5 | AC-4, AC-6, AC-27 | NFR-1 | AC-20 |
| FR-6 | AC-7, AC-9, AC-12, AC-29, AC-33 | NFR-2 | AC-21 |
| FR-7 | AC-8 | NFR-3 | AC-22 |
| FR-8 | AC-9, AC-33 | NFR-4 | AC-23 |
| FR-9 | AC-9, AC-10, AC-11, AC-33, AC-36, AC-37 | NFR-5 | AC-6, AC-24 |
| FR-10 | AC-7, AC-12, AC-33 | NFR-6 | AC-25 |
| FR-11 | AC-13 | NFR-7 | AC-3, AC-26 |
| FR-12 | AC-14, AC-34 | | |

**Definition of done:**
- Every AC passes, or, for AC-16 and AC-17, the gap is recorded under AC-18.
- These test projects pass:
  - `Paramore.Brighter.Core.Tests`, including `Sweeping/` and `Observability/`;
  - `InMemory.Tests`;
  - `MSSQL.Tests`, `MySQL.Tests`, `PostgresSQL.Tests` and `Sqlite.Tests`;
  - `MongoDb.Tests`;
  - `DynamoDB.Tests` and `DynamoDB.V4.Tests`;
  - `Gcp.Tests` (Spanner and Firestore).
- The ADR records:
  - the defaults and the maximums;
  - the per-topic capability's contract and its shape within FR-12's bounds;
  - how the per-topic capability applies the ceiling C to each topic (FR-4);
  - which age a topic uses when several publications share it (Q-14);
  - which outboxes get the per-topic capability in this spec (Q-15);
  - the answers to the other Q-items it depends on.

## Additional Context

### Open questions for the maintainer

- **Q-1. Where does measurement run, and what starts and stops it?**
  - **Proposal:** a hosted service of its own in `Paramore.Brighter.Outbox.Hosting`, registered by its own extension, as `UseOutboxArchiver` registers `TimedOutboxArchiver`. It would run apart from `TimedOutboxSweeper`.
  - **Alternative: a timer in the mediator.** This was the proposal on the issue. It couples measurement to the posting component.
  - **Alternative: an observable gauge whose callback runs the count.** This runs store I/O on the collection thread and ties query frequency to collection, against FR-7.
- **Q-2. What do "on the call" and "warns" mean?** "On the call" could mean telemetry emitted on `Post`/`Clear` rather than on a schedule. But a quiet system with no posts would then never report a message that failed once, so the proposal is that any on-call trigger is added to the schedule of FR-7, not put in its place. A triggered run covers both counts, with at most one triggered measurement of each count per I, and each counts as completed, failed or abandoned for FR-6 like any other measurement. If Q-2 is answered that way, FR-7's title and first bullet, NFR-4 (a) and step 2 of the Proposed Solution are updated to include the trigger. "Warns" could mean a log Warning whenever V > 0, in addition to the metric.
- **Q-3. Outbox that implements only `IAmAnOutboxSync`.** Options:
  - reject it at configuration;
  - run the synchronous count on a dedicated thread;
  - skip measurement and log once.
- **Q-4. Configuration form and existing interfaces.** Is a new member on `IAmProducersConfiguration` (for example `StaleMessageAge`), on `IAmABrighterTracer`, or, if Q-1 picks the timer in the mediator, on `IAmAnOutboxProducerMediator` acceptable, possibly with a default implementation? Or should the step be a registration extension with options of its own? NFR-3 now rules out a member on the outbox interfaces; A_p lives on `Publication`.
- **Q-5. An age-aware count capability for the overall count?** This would be an optional interface that runs `COUNT` with an age filter and a limit, instead of loading rows, with a fallback to `GetOutstandingMessageCountAsync`. It decides whether NFR-4(c) is binding for the overall count. The per-topic capability of FR-12.1 is age-aware and transfers no body by contract, so Q-5 now concerns the overall count only.
- **Q-6. Instrument type and owning role.** Should the gauges be observable instruments registered by `AddBrighterInstrumentation`, as `PublishConfirmationMeter` is, or synchronous gauges? Should they go with `OutboxMeter` from ADR `0084-outbox-metrics`, or have a meter of their own? The same answer applies to both gauges (FR-5).
- **Q-7. What remains open about the age per publication and the count per topic?** The maintainer asked for an age per publication on PR #4598 (comment `issuecomment-6141309069`, 2026-10-13 08:15Z): because a topic can be circuit broken, "we should have an age per publication", or the topic does not show clearly. In a review comment on step 2 of the Proposed Solution (`discussion_r4256375898`, on line 46 of the version reviewed), the maintainer suggested the project "might choose to support both" an overall outstanding count and a per-topic outstanding count, so that topic circuit breakers show up. This spec meets the request and takes up the suggestion: it keeps A for the overall count (FR-3), adds A_p on `Publication` with A as its fallback (FR-2, FR-3.1), and reports the per-topic count beside the overall count (FR-5). What stays open to the maintainer's review on PR #4598:
  - the shape with two gauges: the per-topic count on an instrument of its own rather than a topic attribute on the overall gauge, so that an aggregation over either gauge never mixes the two counts (FR-5, FR-15);
  - which age a topic uses when several publications share it (Q-14);
  - which outboxes get the per-topic capability in this spec (Q-15).
- **Q-8. Instance identity.** The implementation attribute is settled: both gauges carry an attribute that identifies the outbox implementation (FR-15), and instances that share a store are combined with max, per topic for the per-topic gauge (A-6, NFR-7). Only per-instance identity remains open. Options:
  - an attribute that names the instance, or the table or collection;
  - one measuring instance, elected through a lock on a resource of its own. Choosing this means instances that are not elected start no measurement at their due times (FR-7 allows for it).
- **Q-9. Close the FR-14 gaps in columns (a) to (c) here, or in separate issues?** Column (d) is answered by Q-15.
- **Q-10. Cancellation bound.** What bound should each outbox's count meet after its token is cancelled? AC-17's 5 seconds is a test tolerance only.
- **Q-11. Skip a due measurement when no listener is subscribed?** A due time left out this way is not a skipped outcome (FR-7).
- **Q-12. Suppress the database span the outbox's count creates?** With a null request context (FR-12), each measurement may start a new trace.
- **Q-13. Warn at startup when A, or an A_p, is not greater than the sweeper's `MinimumMessageAge` plus `TimerInterval` (A-3)?**
- **Q-14. Which age does a topic use when several publications share it?** This revision takes the smallest (FR-3.1), so the topic shows at the earliest alert any of its publications asks for. Alternatives: the largest, which raises fewer false alarms, or an age per topic set outside `Publication` (OOS-13). Open to the maintainer's review on PR #4598.
- **Q-15. Which outboxes get the per-topic capability in this spec?** This revision implements it on `InMemoryOutbox` only, with the capability's contract and its conformance test (AC-34), and tracks one issue per family under FR-14.1, keeping the first pull request small, as the split on issue #4554 did. Alternative: the relational family too, with one query per dialect and a database container for each. Open to the maintainer's review on PR #4598.
- **Q-16. Should the per-topic count follow the publication's topic when a mapper changes the header topic?** Answered in this spec: no. The per-topic count counts by the header topic (FR-3.1), because the store holds it and the circuit breaker trips it: a publish confirmation carries the wire topic, `MessageHeader.Topic`, to trip the breaker on (`PublishConfirmationResult.cs:45`), and the mediator trips the header topic at `OutboxProducerMediator.cs:946`, `:1040`, `:1240` and `:1313`. Rejected alternative: count by the `ProducerTopic` bag entry (`Message.ProducerTopicHeaderName`, `Message.cs:59`; written at `WrapPipeline.cs:97-103`). It would report held messages under a topic the breaker does not trip, and it would need each outbox that offers the capability to filter on a bag value.

### Code locations

- **Counts:**
  - `IAmAnOutboxSync.cs:139`, `IAmAnOutboxAsync.cs:213`;
  - `InMemoryOutbox.cs:617-643`, `:646-655`;
  - `RelationDatabaseOutbox.cs:1000-1010`;
  - `DynamoDbOutbox.cs:851-924` (V3), `:863` (V4); the `OutstandingAllTopics` index name at `DynamoDbConfiguration.cs:78`;
  - `MongoDbOutbox.cs:975-984` (topic filter at `:497-500`);
  - `FirestoreOutbox.cs:404`, `:1191`.
- **Publications and topics:**
  - `Publication.cs:96` (`Topic`, public setter), `ProducersConfiguration.cs:109` (`ProducerRegistry`), `ProducerKey.cs:37`;
  - `ProducerRegistry.cs:26-27`, `:49-50` (a null topic throws `ConfigurationException`);
  - one way to read the publications, for the ADR: `IAmAProducerRegistry.cs:43` (`Producers`), `IAmAMessageProducer.cs:36` (`Publication`);
  - `MessageHeader.cs:321` (`Topic`), `RoutingKey.cs:61` (`Empty`), `:68` (`IsNullOrEmpty`), `:118` (equality);
  - `WrapPipeline.cs:97-103` (a header topic that differs from the publication's, kept in the `ProducerTopic` bag entry), `Message.cs:59` (`ProducerTopicHeaderName`);
  - `BrighterSemanticConventions.cs:90` (`MessagingDestination`), as `OutboxMeter.cs:135` uses it.
- **Circuit breaker:** `IAmAnOutboxCircuitBreaker.cs:49` (`TrippedTopics`); resolved only when registered at `ServiceCollectionExtensions.cs:1014-1016`; the sweeper's clear at `OutboxSweeper.cs:79` and `OutboxProducerMediator.cs:514`, `:740`, with tripped topics passed at `:762-763`; topics tripped through `TripTopic` (`:1542-1546`) at `:946` and `:1040` (failed publish confirmations, wire topic per `PublishConfirmationResult.cs:45`), `:1240` (failed bulk batch, `MessageBatch.cs:28`) and `:1313` (failed asynchronous send); the synchronous `Dispatch` (`:1119-1136`) trips none.
- **Outstanding-message check:** `OutboxProducerMediator.cs:1422-1473`, queued at `:822-867` behind the in-flight flag (`:87`, claimed at `:842`); PR #4597 removed the static semaphore it used to take.
- **Sweeper and meters:**
  - `TimedOutboxSweeper.cs`, `SweeperMeter.cs`, `PublishConfirmationMeter.cs`, `IAmABrighterOutboxMeter.cs`, `OutboxMeter.cs`;
  - registered in `BrighterMetricsBuilderExtensions.AddBrighterInstrumentation`.
- **Hosting:** `Paramore.Brighter.Outbox.Hosting/HostedServiceCollectionExtensions.cs`.
- **Optional capability precedent:** `Paramore.Brighter.ServiceActivator/IHaveAChannelFailureCount.cs`.
- **Test precedent:** `tests/Paramore.Brighter.Core.Tests/CommandProcessors/TestDoubles/CountingOutbox.cs` (holds the check's counts, `:58`, `:89-94`; bounded wait, `:78-87`).
