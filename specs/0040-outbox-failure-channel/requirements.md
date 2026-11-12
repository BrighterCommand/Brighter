# Requirements

> **Note**: This document captures user requirements and needs. Technical design decisions and implementation details should be documented in an Architecture Decision Record (ADR) in `docs/adr/`.

**Linked Issue**: #4489

**Related**: PR #4598 and spec 0038 (`0038-outbox-stale-message-telemetry`), the telemetry half of issue #4489; ADR `0085-outbox-stale-message-telemetry`, ADR `0086-outbox-per-topic-stale-count`, ADR `0084-outbox-metrics`, ADR `0063-failed-delivery-context`, ADR `0081-timed-outbox-sweeper-health-metrics`, spec 0039 (`0039-circuit-breaker-stale-tripped-topics`).

Code locations refer to commit 14850234f on master, and every file is under `src/`, apart from `CONTRIBUTING.md`. A bare `:NNN` is in `OutboxProducerMediator.cs`, unless the same citation has just named another file. `DynamoDbOutbox.cs` means `Paramore.Brighter.Outbox.DynamoDB`, and V4 means `Paramore.Brighter.Outbox.DynamoDB.V4`. `RmqMessageProducer.cs` means `Paramore.Brighter.MessagingGateway.RMQ.Async`. `ServiceCollectionExtensions.cs` means `Paramore.Brighter.Extensions.DependencyInjection`.

## Problem Statement

**As a developer** of an application that publishes through Brighter's outbox, **I would like** to subscribe to a notification each time a send from the outbox fails, the broker does not confirm a message, or a sent message cannot be marked as dispatched, whether the clear came from `Post`, from an explicit `ClearOutbox` or from the sweeper, **so that** my code can alert, compensate or remove the message soon after the failure, instead of finding out from a log or from the age of the message. **And when I ask for it**, I would like a notification each time a sweep sends a message through a producer that does not confirm publication, **so that** code that compensated for a failure can learn that the message went after all.

### What a subscriber can do with a notification

| Reaction | What it needs | Limit |
|---|---|---|
| Raise an alert or a log entry of its own, per topic or per kind | Topic, kind, exception | None |
| Compensate, for example with a reversing transaction, when no sweeper runs, or with FR-16 on when one does | Message id, kind. It reads the body through `IAmAnOutboxSync.Get` or `IAmAnOutboxAsync.GetAsync` | The notification is not final: a sweeper may still send the message later (FR-6), and FR-16 reports that send when it is turned on and the producer does not confirm publication (OOS-16). No failure kind proves the message was not sent: `MarkDispatchedFailed` means probably sent, `ConfirmationFailed` means not confirmed, and a `SendFailed` from a cancellation or a client timeout can follow the broker's acceptance |
| Ask for the clear again, through `ClearOutbox` or `ClearOutboxAsync` | Message id | A clear that fails raises another notification, so a retry on every notification can repeat without end (NFR-11) |
| Stop further sends, through `IAmAnOutboxSync.Delete` or `IAmAnOutboxAsync.DeleteAsync` | Message id | The application decides this. Brighter never does it for them |
| Undo or review a compensation when a sweep sends the message after all | Message id, kind `SentBySweep` (FR-16) | `SentBySweep` means the send returned without an exception, and the mediator then tries to mark the message dispatched. It says nothing about what happened to the message before: a sweep also sends a message that was deposited and never cleared. It is raised only for a producer that does not confirm publication (OOS-16), and only on the instance that ran the sweep (FR-14) |

A subscriber cannot fail the `Post` that caused the notification, because that call may have returned. It also cannot make a handler fail so that its incoming message is retried, which is the second row of the table on issue #4489. This spec does not address that row (OOS-7).

### Why a failed clear is invisible today

- A send runs inside `ExecuteWithResiliencePipeline` (`OutboxProducerMediator.cs:1475`) or `ExecuteWithResiliencePipelineAsync` (`:1500`). Each catches every exception, logs it, and returns false (`:1492-1497`, `:1534-1539`). The exception goes no further, so `PostAsync` returns normally.
- A producer that supports publish confirmation reports a refusal through a callback (`:940-947`, `:1034-1041`), which may arrive before or after the clear returns, usually on another thread. On the async path, Kafka turns a `ProduceException` into a failed confirmation and does not rethrow it (`KafkaMessagePublisher.cs:56-67`), so the mediator's `sent` is true for a message that failed.
- `MarkDispatched` can fail after a good send. Today only a log records this (`:1126-1133`, `:1215-1225`, `:1297-1308`, `:932-937`, `:1029-1031`). The message stays undispatched and a sweeper sends it again.
- The stale-message gauge of ADR `0085-outbox-stale-message-telemetry` reports how many messages are waiting, after a configured age and on a measurement interval. It does not say which message failed or why, and it is opt-in.

### Scope

The maintainer's suggestions on issue #4489 were:

- 2026-10-01: "A better solution might be a channel you can subscribe to, where we notify you of failed messages that the Outbox gives up on."
- 2026-10-03: "A channel to which we notify any failed Clear(Async) allowing you to subscribe to the Channel to pick up a more immediate notification, if you have some way to react to that in your code."

This spec covers that channel, with the differences listed under "One channel or two". PR #4598 shipped the telemetry half and left the channel out. The other items on the issue are later, separate work (OOS-1 to OOS-3). On 2026-10-01 the maintainer raised a concern about making `PostAsync` throw, and this spec does not do that (OOS-4).

On 2026-11-10, on PR #4725, the author of issue #4489 pointed out that with a sweeper a failure notification can lead a subscriber to compensate, for example by marking an order as failed, and the next sweep can then still send the message, with nothing to tell the subscriber. Checking is not practical, because `Get` does not say whether a message was dispatched. The author suggested one notification for when the sweeper sends a message, and noted that it needs no list of ids. This spec adds that notification as an option (FR-16) and keeps every other success out (OOS-13).

### One channel or two: what "gives up" means in the code

Asked what should count as the outbox giving up, the maintainer replied on 2026-10-03 with "any failed Clear(Async)". This spec takes one channel from that wording, with no "gave up" notification (D-1), and differs from it in two directions, listed below. The direct question, in the issue comment of 2026-10-06, has no answer on the issue. The code supports one channel, with these qualifications:

- **No give-up point exists to notify from.** Nothing counts send attempts. `OutstandingMessagesAsync` takes an age, a page and the tripped topics (`:762-763`). A message stays eligible until it is dispatched or the application deletes it. The mediator also does not know whether a sweeper runs, so it cannot tell a last attempt from a first one.
- **A store does not silently discard an undispatched message unless a TTL is configured.** `InMemoryOutbox` removes dispatched entries only (`InMemoryOutbox.cs:76-79`, `:742`, `:757`), and `OutboxArchiver` deals only in dispatched messages (`OutboxArchiver.cs:110`, `:122`). With a TTL set, DynamoDB stamps an expiry on add (`DynamoDbOutbox.cs:192-193`, `:1366-1368`; V4 `:1378-1380`) and Firestore stamps a `Ttl` field on add (`FirestoreOutbox.cs:1290-1305`), whatever the dispatch state. Whether a row is deleted depends on the store: the DynamoDB table setting and a Firestore TTL policy are outside Brighter. If a row is deleted, the mediator cannot observe it and the stale gauge stops counting it, so the age telemetry does not cover this case (Q-3, OOS-9).
- **The literal wording would lose Kafka, so this spec is wider in three cases.** A failed confirmation may arrive before or after the clear returns, usually on another thread, and a failed `MarkDispatched` follows a successful send. Neither is a failed `Clear` call in the literal sense. Without them, a broker refusal on the async Kafka path raises nothing (D-4). This spec defines a *failed clear* to include both. The third comes along with them: the confirmation of a `Call`, which follows no clear (FR-3.2, Q-8).
- **In four cases this spec is narrower than "any".** A fetch that throws stays out, because the clear never reached a message and the failure already reaches the caller or ends the sweep as failed. A topic with no usable producer, a bulk sweep whose batch creation throws, and an id not in the outbox also stay out, and FR-4 says what reports each of them. Q-6 puts this to the maintainer.

Everything else that goes undispatched is covered by the age telemetry when it is enabled: a confirmation that never arrives, a deposit never cleared, and a message held back by a tripped topic.

## Proposed Solution

1. The application turns the feature on with one explicit step, and may set a capacity. Without that step, nothing changes.
2. When a send fails, the broker does not confirm a message or `MarkDispatched` fails (FR-3), the mediator raises one notification naming the message id, its topic, the kind of failure, the exception when there is one, and the time. It raises it for failures on the synchronous, asynchronous, bulk and confirmation paths, for every outbox.
3. Raising never blocks, slows or fails the publishing path, and everything the mediator does today stays as it is, including exceptions that already reach the caller.
4. A subscriber reads notifications independently of the publishing path. They are best effort: held in memory up to capacity and lost on a crash. When the channel is full, a notification is lost and the loss is counted.
5. The notification is a signal and not a verdict. The message may still be retried, and each failed attempt raises another notification.
6. As a second option of the same step, the mediator also raises one notification for each message that a sweep sends without an exception through a producer that does not confirm publication, so that a subscriber can learn that a message it treated as failed went after all.

The maintainer suggested a channel the application subscribes to. This spec reads that as an in-process channel the subscriber reads from. The type and API are for the ADR.

## Requirements

### Terms

| Term | Meaning |
|---|---|
| **Clear** | One call of `ClearOutbox`, `ClearOutboxAsync` or `ClearOutstandingFromOutboxAsync` on the mediator. `Post` and `PostAsync` deposit and then call a clear (`CommandProcessor.cs:720`, `:785-786`). |
| **Explicit clear**, **sweep** | A clear from `ClearOutbox(Async)` (`OutboxProducerMediator.cs:381`, `:447`), and a clear from `ClearOutstandingFromOutboxAsync` (`:514`), whose only caller in Brighter is `OutboxSweeper.SweepAsync`. A sweep is asynchronous only. |
| **Attempt** | One run of the resilience pipeline `CommandProcessor.OutboxProducer` around one send, one batch send or one `MarkDispatched`. The default pipeline makes up to four tries in one attempt (`ResiliencePipelineRegistryExtensions.cs:62-67`). |
| **Failed clear** | A case in which a clear, or the confirmation that follows it, did not end with the message both sent and marked dispatched, as FR-3 lists. A `Call`'s confirmation counts too, although no clear precedes it (FR-3.2). FR-4 lists what stays out. |
| **Notification channel** | What this spec adds: the in-process channel from which a subscriber reads notifications. Brighter already uses "channel" for a consumer's channel to a broker, so public names need care (Q-5). |
| **Notification** | One record of one failed clear of one message, or of one sweep send (FR-16), with the fields in FR-5. |
| **Sweep send** | In a sweep, an attempt by a producer that does not confirm publication to send one message, or one batch, that ended without an exception. The mediator then tries to mark the message dispatched. |
| **Raise** | What the mediator does to put a notification on the notification channel. This document uses "send" for messages only. |
| **Subscriber** | The application code that reads notifications. |
| **Delivered** | Read by the subscriber. |
| **Capacity** | The most notifications the channel holds that have not been delivered. |
| **Dropped count** | Since the channel was created, the total number of notifications raised and never delivered because of FR-9 or FR-13. |
| **Kind** | The kind of failure, `SendFailed`, `ConfirmationFailed` or `MarkDispatchedFailed`, or `SentBySweep` for a sweep send. These are working names; the ADR fixes the final ones. |
| **Topic** | `MessageHeader.Topic`, the wire topic. For a confirmation, this is `PublishConfirmationResult.Topic`, which holds the same value. |

### Functional Requirements

**FR-1 — The feature is off by default and turned on by one explicit step.**
No existing call turns it on, including `AddProducers`, `UseOutboxSweeper`, `UseOutboxStaleMessageMonitor` and `AddBrighterInstrumentation`. When it is off, the mediator raises nothing and creates no buffer, thread, timer or task for it. The form of the step is for the ADR. If it is a new property, its default means off.

> *Example.* `AddProducers` with no such step, and a producer that always throws: `PostAsync` behaves exactly as at commit 14850234f, and no notification exists.

**FR-2 — Capacity has a default and is validated.**
Capacity is optional. It has a documented default, and it must be at least 1. Its default, and any maximum, are for the ADR (P-2). An invalid capacity throws a `ConfigurationException` that names the setting and the value, no later than when the mediator is constructed. On the DI path the mediator is built lazily through `Activator.CreateInstance` (`ServiceCollectionExtensions.cs:416-420`, `:832`), so unless the ADR validates earlier, that exception reaches the application at the first resolve, as the inner exception of a `TargetInvocationException`.

> *Example.* 0 and -1 throw. 1 is accepted. With no capacity set, the documented default applies.

**FR-3 — Three kinds of failure raise a notification.**
A notification is raised at each site below, on the sweep and on the explicit clear wherever both reach it. The notification sites are not the `TripTopic` sites: a synchronous send failure trips nothing today (`:1119-1136`), and a `MarkDispatched` failure trips nothing on any path.

**FR-3.1 — `SendFailed`: an attempt to send ended in an exception.**
Sites:
- `Dispatch`, non-confirming producer (`:1119-1122`);
- `Dispatch`, confirming producer, where the result of the pipeline is discarded today (`:1113-1115`);
- `DispatchAsync` (`:1285-1292`);
- `BulkDispatchAsync`, one notification for each id in the failed batch (`:1201-1208`, `:1238-1241`).

A cancelled send counts: `OperationCanceledException` is caught like any other exception and the message stays undispatched (D-8). There is no synchronous bulk path: `IAmABulkMessageProducerAsync` is the only bulk role, and `BulkDispatchAsync` is the only caller.

> *Example.* A 300 KB message to a broker whose limit is 256 KB, the case on issue #4489, with the default pipeline: four sends, `PostAsync` returns, and there is one notification of kind `SendFailed` with the broker's exception.

**FR-3.2 — `ConfirmationFailed`: the producer reported that the broker did not confirm the message.**
Sites: `:940-947` and `:1034-1041`. This kind means "not confirmed", not "not delivered". Kafka reports any status other than `Persisted` as a failure, including `PossiblyPersisted` (`KafkaMessageProducer.cs:417`, `:455`). A result with an empty id or a null topic still raises a notification, as FR-5 says. A `Call` uses the same producers, and its message is not stored (`CommandProcessor.cs:1506`). Neither the result nor the mediator records that a confirmation came from a `Call` (D-10), so a failed one raises a notification. A successful one goes on to `MarkDispatched` like any other, where FR-3.3 applies.

> *Example.* A message goes through `PostAsync`, and the producer reports a result of `(false, "m1", "orders", null)`. One notification with id `m1`, topic `orders`, kind `ConfirmationFailed` and no exception.

**FR-3.3 — `MarkDispatchedFailed`: the send succeeded, or was confirmed, but the outbox was not updated.**
Sites:
- `marked` is false at `:1126-1134`, `:1215-1226` (per id) or `:1297-1309`;
- the result of the pipeline is false at `:932-937` or `:1029-1031`, where it is discarded today;
- on a successful confirmation, an exception reaches the catch at `:949` or `:1043`.

The message has probably reached the broker. Unless it has no row, it is still undispatched, so a sweeper will send it again. This is a kind of its own: not notifying would hide a duplicate on its way, and reporting it as `SendFailed` would invite a subscriber to compensate for a message the broker has.

**A message with no row.** What `MarkDispatched` does when no row matches depends on the store. `DynamoDbOutbox` and `FirestoreOutbox` throw (`DynamoDbOutbox.cs:540`, `:550-553`; V4 `:553`, `:565`; `FirestoreOutbox.cs:312`, `:782`), while `InMemoryOutbox` and `RelationDatabaseOutbox` do not (`InMemoryOutbox.cs:532`; `RelationDatabaseOutbox.cs:1192`, `:1235`). A `Call`'s message never has a row. So on those two stores, with a confirming producer, every successful `Call` raises `MarkDispatchedFailed` although nothing failed, on the path that logs an `Error` there today (`OutboxProducerMediator.cs:1536`). A message the application deleted before its confirmation arrives does the same (D-10, Q-8).

> *Example.* The send succeeds and `MarkDispatchedAsync` throws `TimeoutException`. One notification of kind `MarkDispatchedFailed` with that exception. The later sweep sends the message a second time.

**FR-4 — These cases raise no notification.**

| Case | Location | What reports it today |
|---|---|---|
| An id passed to an explicit clear is not in the outbox | `:397-401`, `:466-470` | Log at `Error`; the rest of the clear continues, and no exception is thrown (Q-6) |
| No producer for the topic | `:1102`, `:1173`, `:1277`, which throw `ConfigurationException` (`ProducerRegistry.cs:112`, `:130`) | On an explicit clear, the exception reaches the caller. On a sweep, log at `:787` and `SweepOutcome.Failed` (`TimedOutboxSweeper.cs:244-248`) (Q-6) |
| The producer lacks the required role, or creating batches throws | `:1140`, `:1246`, `:1316`, `:1199` | The same as no producer; `:1199` and `:1246` occur only on a sweep (Q-6) |
| Fetching messages for an explicit clear throws | `:396`, `:465` | The exception reaches the caller (Q-6) |
| Fetching messages for a sweep throws | `:762-763` | Log at `:787`, `SweepOutcome.Failed` (`TimedOutboxSweeper.cs:244-248`) (Q-6) |
| A message not attempted because an earlier one in the same clear threw | The sites above | The exception, then a later sweep |
| No resilience pipeline is registered, at a send or a `MarkDispatched` of a clear. The lookup sits outside the `try`. On a confirmation, FR-3.3 applies | `:1477`, `:1506` | The exception reaches the caller. On a sweep, also log at `:787` and `SweepOutcome.Failed` |
| The logger or `CheckOutstandingMessages` throws inside the catch of `ExecuteWithResiliencePipeline(Async)`, at a send or a `MarkDispatched` of a clear. On a confirmation, FR-3.3 applies | `:1494-1495`, `:1536-1537` | The exception reaches the caller. On a sweep, also log at `:787` and `SweepOutcome.Failed` |
| A sweep is skipped because another is running | `:799-803` | Log |
| A message on a tripped topic is left out of a sweep | `:762-763` | Stale gauge |
| A send inside an attempt that fails and is then retried by the pipeline | `:1483`, `:1512` | The notification for the attempt, if the attempt fails |
| Writing to the outbox fails | `:305-306`, `:345-346`, `:657-658`, `:691-692` | `ChannelFailureException` to the caller |
| `OutboxLimitReachedException`, or the outstanding-message check fails | `:817-819`, `:1463-1468` | The exception; log |
| A `Call` whose send throws | `:358-367` | The exception is logged, and no row exists. Its confirmation is in FR-3.2 |
| A confirmation that never arrives, including those cleared when RabbitMQ's connection fails | `RmqMessageProducer.cs:231` | Stale gauge |
| A `Persisted` report with no message id, on a store that does not throw when `MarkDispatched` matches no row. On a store that throws, FR-3.3 applies | `KafkaMessageProducer.cs:432-438` | Log; stale gauge |
| A successful `Call` confirmation, on a store that does not throw when `MarkDispatched` matches no row | `:932-937`, `:1029-1031` | Nothing: the message was sent |
| A confirmation reports success and the mediator has no outbox of the type that callback uses. Only with a third-party outbox or producer, or the control bus's sink outbox (`ControlBusReceiverBuilder.cs:232`) | `:922`, `:1019` | No failure log; stale gauge where the outbox can be read |
| A message deposited and never cleared | none | Stale gauge |
| A store deletes an undispatched message by its own TTL | See "One channel or two" | Nothing (Q-3, OOS-9) |
| A clear that succeeds, apart from a sweep send while FR-16 is on | none | The metrics of ADR `0084-outbox-metrics` |

> *Example.* `ClearOutbox(["m1", "ghost"])` with `m1` in the outbox and a working producer: `m1` is sent, the existing `Error` entry names `ghost`, no exception reaches the caller, and no notification is raised.

**FR-5 — A notification carries these fields and no others.**

| Field | Type | Meaning |
|---|---|---|
| Message id | `Id` | The message that failed, or that a sweep sent. `Id.Empty` only when a confirmation's result carries no id, which applies to `ConfirmationFailed` and to a `MarkDispatchedFailed` after a confirmation (`KafkaMessageProducer.cs:424-438`). |
| Topic | `RoutingKey` | The topic. `RoutingKey.Empty` only when a confirmation's result carries none, since `PublishConfirmationResult.Topic` is nullable (`PublishConfirmationResult.cs:55`). |
| Kind | enumeration | As in Terms. |
| Exception | `Exception?` | The exception that ended the attempt. Null where none exists, as below. It is the exception object itself (D-9). |
| Occurred at | `DateTimeOffset` | The time the mediator raised the notification, from its `TimeProvider`. |

| Kind | Id, topic from | Exception |
|---|---|---|
| `SendFailed`, single | `message` | Yes |
| `SendFailed`, batch | `batch.Ids()`, `batch.RoutingKey` | Yes; the same object on each notification of the batch |
| `ConfirmationFailed` | `PublishConfirmationResult` | No: the result carries none (`PublishConfirmationResult.cs:52-56`, Q-2) |
| `MarkDispatchedFailed`, after a send | `message` or the batch id and wire topic | Yes |
| `MarkDispatchedFailed`, after a confirmation | `PublishConfirmationResult` | Yes |
| `SentBySweep`, single | `message` | No: nothing failed |
| `SentBySweep`, batch | `batch.Ids()`, `batch.RoutingKey` | No |

A notification carries no message, body, header bag, `RequestContext` or span (Q-7). The full message is in hand at the send sites, including the batch sites (`:1187`), but not at a confirmation, where the callback holds only a construction-time context (`:901`) and the fields of the result. So that every kind has one shape and a notification has no field for a body, the subscriber reads the message from the outbox by id when it needs it (D-5).

> *Example.* A bulk sweep of `m1` and `m2` on `orders` fails with exception `E` at 12:00:00. Two notifications: (`m1`, `orders`, `SendFailed`, `E`, 12:00:00) and (`m2`, `orders`, `SendFailed`, `E`, 12:00:00), with the same `E` object on both.

**FR-6 — One notification per message per failure; a retried message notifies again; a notification is not final.**
- Each failed attempt raises one notification per message it covers, whatever the number of sends inside it (D-7).
- A batch of N that fails raises N notifications (D-6).
- The channel does not remember what it has raised. A message that fails on 5 sweeps raises 5 notifications, and a subscriber that wants one per message removes duplicates by id.
- A notification has no attempt number, because the outbox does not count attempts.
- Nothing signals that a message later succeeded, apart from a sweep send while FR-16 is on.

> *Example.* With a sweeper, `m1` fails at 12:00:00, 12:00:05 and 12:00:10 and is sent at 12:00:15. The subscriber receives three notifications for `m1` and nothing at 12:00:15. With FR-16 on and a producer that does not confirm publication, it also receives one `SentBySweep` for `m1` at 12:00:15.

**FR-7 — What the mediator does today stays exactly as it is, on or off.**
With the feature on or off, these are identical: what each clear returns and throws, every existing log line, each `TripTopic` call on `IAmAnOutboxCircuitBreaker`, every measurement of ADR `0084-outbox-metrics`, and the outstanding-message check. A `SendFailed` or `MarkDispatchedFailed` never counts as cleared. A `TripTopic` that throws at `:946`, `:1040`, `:1240` or `:1313` does not suppress the notifications for the same failure. The channel neither reads nor changes the tripped topics, and a tripped topic does not stop an explicit clear from attempting a send or from notifying.

> *Example.* A sync send fails with a breaker registered: one notification, and still no `TripTopic`, as at commit 14850234f.

**FR-8 — Delivery is best effort.**
Notifications are held in process memory only and are lost when the process crashes or is stopped without a chance to read them. Brighter does not persist or resend them. Each raised notification is delivered at most once. A clear's outcome never depends on delivery. The stale gauge of ADR `0085-outbox-stale-message-telemetry` remains the way to learn of a message whose notification was lost.

> *Example.* Three notifications wait to be read and the process crashes: all three are lost, the messages are still in a persistent outbox, and the next sweep attempts them.

**FR-9 — When the channel is full, a notification is lost and the loss is counted.**
Raising a notification when the channel holds capacity notifications neither waits nor fails. One undelivered notification is lost, and which one is for the ADR (P-3). Then:
- the dropped count goes up by 1, and the subscriber can read it at any time;
- a `Warning` log entry naming the dropped count is written on the first drop, and then at a limited rate while drops continue. The ADR sets the interval (P-4).

The message itself is unaffected.

> *Example.* Capacity 2, nothing reading, and notifications n1, n2 and n3 raised in order: a later read returns two of them in the order raised, and the dropped count is 1.

**FR-10 — Order follows raising.**
A notification whose raise completes before the next raise begins is delivered first, apart from any dropped. Notifications raised concurrently have no defined relative order. Order says nothing about the order on the broker: a confirmation from an earlier send can arrive after the failure of a later one. Occurred at records when the mediator raised the notification and is not monotonic across threads.

> *Example.* A sequential explicit clear of m1, m2 and m3 that all fail: the subscriber receives m1, m2, m3.

**FR-11 — A subscriber's behaviour never reaches the publishing path.**
- **One reader per notification.** A notification is delivered to one reader and never twice. Fan-out is the application's (P-5).
- **Early.** With the feature on, a subscriber may start reading before the mediator exists, as a hosted service can on the DI path, and it waits. Subscribing from a hosted service's start must be possible.
- **Late.** Notifications raised after the feature is on and before the subscriber starts are held, up to capacity.
- **Absent.** With no subscriber, FR-9 applies, and the feature holds no more than capacity notifications.
- **Slow or blocked.** Notifications wait in the channel and FR-9 applies. No publishing call waits.
- **Throwing.** An exception in subscriber code stays in the subscriber's own read loop and never reaches a clear, a producer's thread or the sweeper. If that ends the loop, it becomes an absent subscriber.

> *Example.* A subscriber blocks forever on n1. 10,000 failing `PostAsync` calls all return, and the dropped count rises.

**FR-12 — Subscriber code is never run by the raising call.**
It does not run on the calling thread during a clear, on a producer's confirmation thread or on the sweeper's dedicated thread. Producers wait on confirmation callbacks (`RmqMessageProducer.cs:584`, `KafkaMessageProducer.cs:464-469`), and the sweeper runs in a context on its own thread (`TimedOutboxSweeper.cs:242`), so subscriber code run there would hold them up.

> *Example.* Subscriber code that sleeps for 10 seconds: a failing `PostAsync` does not take 10 seconds longer, and the sweep does not either.

**FR-13 — Shutdown is defined.**
On `Dispose` of the mediator:
- notifications raised while it closes the producers (`:231`) are accepted, because a producer may report confirmations then (`KafkaMessageProducer.cs:408-409`);
- then the channel closes: the subscriber may still read what is buffered and can tell that no more will come;
- `Dispose` does not wait for the subscriber;
- a notification raised after the channel closes is discarded without an exception and added to the dropped count, with no log entry from the feature;
- reaching the channel after `Dispose` does not throw: a read returns what is buffered and then reports the end.

> *Example.* Two notifications are buffered and the subscriber is blocked: `Dispose` returns; once unblocked, the subscriber reads two notifications and then sees the end.

**FR-14 — Scope: one channel per mediator, in the process where the failure happens, for every outbox.**
- Each mediator with the feature on has its own channel and dropped count. The DI path has one such mediator per container (`ServiceCollectionExtensions.cs:416-420`, `:554`). The control bus builds mediators of its own (`ControlBusSenderFactory.cs:62`, `ControlBusReceiverBuilder.cs:177`); they have no channel, so their failures do not notify.
- A producer instance shared by two mediators calls both their confirmation handlers, so its result raises on both (`OutboxProducerMediator.cs:875-886`).
- A failure notifies only in the process that observes it. With several instances and a distributed lock, a message posted on instance B and retried by instance A notifies on A.
- The feature works at the mediator, above the outbox, so it needs no store-specific code, query or schema change, and it applies to sync and async paths alike. It behaves alike for every outbox, apart from a message with no row, where FR-3.3 says what differs.

> *Example.* Two mediators in one process with separate producer registries, and a failure on the first: only the subscriber of the first receives a notification.

**FR-15 — The feature is available through both ways of wiring a mediator.**
Both the DI path (`AddProducers`, built at `ServiceCollectionExtensions.cs:817-853`) and direct construction of `OutboxProducerMediator` (`OutboxProducerMediator.cs:137`) can turn it on and reach it.

> *Example.* A test that news up a mediator with an `InMemoryOutbox` needs no container to receive a notification.

**FR-16 — As an option, a sweep send raises a `SentBySweep` notification.**
The option is off by default, and it is set in the same step that turns the feature on. The ADR gives its form. With it on, a sweep raises one notification for each message it sends without an exception through a producer that is not `ISupportPublishConfirmation`, the same check that leads to `MarkDispatched` at `:1294` and `:1210`:
- `DispatchAsync`, when the clear source is the sweeper and the send attempt at `:1285-1292` ended without an exception;
- `BulkDispatchAsync`, one notification for each id of a batch whose send attempt at `:1201-1208` ended without an exception. Only a sweep reaches this method.

The notification is raised when the send returns and before `MarkDispatched`, in the same async flow. A `MarkDispatchedFailed` for the same id from that sweep therefore always comes after it. A `SentBySweep` is not final either: a `SendFailed` for the same id can still follow, from a `Post` or an explicit clear whose send outlived the sweeper's minimum message age, or from another instance that swept the same message. A confirming producer raises no `SentBySweep` (D-12, OOS-16): its confirmation can arrive before or after the send returns, so a sweep send there could not be ordered against a `ConfirmationFailed` for the same id. A producer that implements `ISupportPublishConfirmationAsync` and not `ISupportPublishConfirmation` passes that check. No producer in Brighter does that, and such a producer raises `SentBySweep` with no order against its confirmation.

A `SentBySweep` says nothing about what happened to the message before. A sweep also sends a message deposited and never cleared, a message whose clear failed on another instance, a message whose `MarkDispatched` failed and a message that a tripped topic held back. With the option off, a sweep raises only the failures of FR-3. An explicit clear, a `Post` and a `Call` never raise `SentBySweep`.

> *Example.* With the option on and a producer that does not confirm publication, `m1` fails in `PostAsync` at 12:00:00, and the subscriber marks the order failed. The sweep at 12:00:05 sends `m1`. The subscriber receives `SendFailed` for `m1`, then `SentBySweep` for `m1`, and can review the order.

### Non-functional Requirements

**NFR-1 — Raising never blocks.**
Raising a notification does no I/O and does not wait on the subscriber or on any unbounded operation. The exceptions are the FR-9 log entry and the log entry for a fault inside the feature, which go through the same logger factory as the mediator's entries on a failed send (`:1494`, `:1536`). What that logger does is the application's configuration. AC-28 asserts the behaviour in CI. This spec sets no timing target, because CI timing is noisy.

**NFR-2 — Raising never fails a publish.**
An exception from anything the feature does on the publishing path, including writing the FR-9 log entry, is caught and logged where the logger allows and goes no further. A clear's outcome is the same as with the feature off.

**NFR-3 — A successful clear pays nothing.**
With the feature on, a clear in which every message succeeds raises nothing and allocates no notification. The one exception is a sweep while FR-16 is on, which raises one notification per message it sends through a producer that does not confirm publication.

**NFR-4 — Turned off, the feature holds nothing.**
Turned off, no buffer exists and no notification is allocated; each raise site checks that it is off.

**NFR-5 — The number of notifications held is bounded.**
The buffer never holds more than capacity notifications, whatever the failure rate. No field of a notification holds a message or a body. A notification keeps its exception object, which Brighter does not inspect and which can hold a body: on Kafka's sync path the `ChannelFailureException` wraps a `ProduceException` whose `DeliveryResult.Message` holds the message (`KafkaMessageProducer.cs:322-329`). Capacity bounds the number of notifications, not their bytes.

**NFR-6 — The channel is safe under concurrency.**
Raising is safe from any number of threads at once: request threads, the sweeper and producers' threads. Below capacity, no notification is lost or duplicated, and the dropped count is exact.

**NFR-7 — A notification is available immediately.**
A notification can be delivered as soon as raising it returns. Brighter adds no timer, polling interval or batching delay. CI checks this with a 5 second wall-clock tolerance, which is a test tolerance and not a target.

**NFR-8 — Public API changes are additive at source level.**
- No member is added to, removed from or changed on: `IAmAnOutbox`, `IAmAnOutboxSync`, `IAmAnOutboxAsync`, `IAmAnOutboxProducerMediator`, `IAmAnOutboxProducerMediator<,>`, `IAmAMessageProducer` and its sync, async and bulk roles, `ISupportPublishConfirmation`, `ISupportPublishConfirmationAsync`, `IAmAnOutboxCircuitBreaker`, `IAmABrighterOutboxMeter` or `IAmABrighterTracer`.
- `IAmProducersConfiguration` gets no new member (Q-1).
- `PublishConfirmationResult` does not change.
- Existing application code and existing tests compile and pass unchanged.
- The one breaking change is binary: the `OutboxProducerMediator` constructor gains a trailing optional parameter, so code compiled against the old signature must be recompiled. `release_notes.md` lists it under `#### Breaking changes` as *(binary)*, beside one entry for the feature.

**NFR-9 — Time is testable.**
Occurred at and the FR-9 log interval come from the mediator's injectable `TimeProvider`.

**NFR-10 — Nothing leaves the process.**
The feature adds no export, instrument or log of a message's content. Its drop warning names the dropped count, and a fault inside the feature logs only the exception type. An exception on a notification may contain broker details and, as NFR-5 says, a body, and it reaches only application code in the same process.

**NFR-11 — The feature is documented.**
Every new public type and member has XML documentation that follows `.agent_instructions/documentation.md`. A page in BrighterCommand/Docs covers:
- how to turn it on and subscribe;
- the meaning of each kind, in particular that `MarkDispatchedFailed` means probably sent, `ConfirmationFailed` means not confirmed, and `SendFailed` does not prove the message was not sent, because a cancellation or client timeout after the broker's acceptance looks the same;
- that a notification is not final, and a sweeper may still send the message;
- that a subscriber should remove duplicates by id;
- that asking for a clear on every notification can repeat without end;
- what is lost, and when;
- that a hosted reader should pass its stopping token;
- that an exception escaping a `BackgroundService` reader stops the host by default, so a read loop handles what its own code can throw;
- what FR-4 leaves out, and that the stale gauge covers most of it;
- how to read the dropped count;
- that a batch's notifications share one exception object;
- that a confirmation for a `Call`'s message also notifies, that the message cannot be read by id, and that on a store that throws when `MarkDispatched` matches no row a successful `Call` raises `MarkDispatchedFailed` although nothing failed;
- that a notification from a confirmation can carry an empty id or topic;
- how to turn on `SentBySweep`, and that it is raised only for a producer that does not confirm publication, so a sweep through Kafka, RabbitMQ or `InMemoryMessageProducer`, which always confirms, raises none;
- that `SentBySweep` means the sweep's send returned, so a `MarkDispatchedFailed` for the same id can follow it, and so can a `SendFailed` from a `Post`, an explicit clear or another instance still sending the same message;
- that `SentBySweep` does not mean the message failed before, and that it notifies only on the instance that ran the sweep, so each instance reads its own channel and acts on records shared across instances;
- that an application which dispatches through the sweeper and a producer that does not confirm publication, with deposits it never clears, receives one `SentBySweep` per message, and that these notifications take capacity from the failures and count in the dropped count.

### Constraints and Assumptions

- **C-1.** No change to `IAmAnOutboxCircuitBreaker` or its use, the instruments and meter role of ADR `0084-outbox-metrics`, the gauges of ADR `0085-outbox-stale-message-telemetry`, the sweeper, transports, outbox stores or their schemas.
- **C-2.** `Paramore.Brighter` targets `netstandard2.0;net8.0;net9.0;net10.0` (`Directory.Build.props:42`), so a default interface member is not available as a way to add to an interface.
- **C-3.** Nothing makes `Post` or `PostAsync` throw because of this feature.
- **C-4.** Because no transport changes, the tests run against the in-memory outbox and producer and test doubles. No broker in Docker is needed.
- **C-5.** ADR before code, as `CONTRIBUTING.md:63-66` asks.
- **A-1.** A message id is how a subscriber links a notification to its own work. `DepositPost` returns the id; `Post` and `PostAsync` return none, so with them the subscriber relies on how its mapper sets the id.
- **A-2.** A subscriber that reads a message by id reads it while it is still in the outbox. That is true of an undispatched message, unless the application deleted it or a store's TTL, where one deletes, removed it. A message sent by a `Call` is never in the outbox.
- **A-3.** A producer that reports a send failure by throwing does not also report a failed confirmation for the same attempt. That holds for the Kafka and `RMQ.Async` code read for this spec (`KafkaMessagePublisher.cs:56-67`, `RmqMessageProducer.cs:228-244`). A third-party producer that does both produces two notifications, and this spec does not remove the duplicate.
- **A-4.** A bulk send that throws is treated as a failure of its whole batch. The mediator cannot see a partial success, so messages of that batch may have been delivered.

### Out of Scope

- **OOS-1.** Correcting the Post section of the Outbox Support page on BrighterCommand/Docs. Later work on issue #4489.
- **OOS-2.** The Azure Service Bus "message will not be retried" log. Later work on issue #4489.
- **OOS-3.** The cancelled token that surfaces as `ChannelFailureException` from the outbox write. Later work on issue #4489.
- **OOS-4.** Any option that makes `PostAsync` throw on a failed send.
- **OOS-5.** An attempt limit, an attempt count, or a "gave up" notification (D-1).
- **OOS-6.** Persisting notifications, delivering them to another process, or sending them through a broker.
- **OOS-7.** Failing a handler, or preventing the acknowledgement of its incoming message, because a post inside it failed.
- **OOS-8.** New instruments or attributes, including a count of notifications or drops (Q-4).
- **OOS-9.** Reporting a store's TTL deletion of an undispatched message (Q-3).
- **OOS-10.** Changes to which failures trip the circuit breaker, including the synchronous send that trips nothing today.
- **OOS-11.** A failure reason on `PublishConfirmationResult` (Q-2).
- **OOS-12.** Automatic reactions by Brighter to a notification, such as retry or deletion.
- **OOS-13.** Notifications for successes, or for a recovery after a failure, apart from the sweep send of FR-16. That includes a successful explicit clear, `Post` or `Call`, and any confirmation, including one that follows a sweep send.
- **OOS-14.** Notifications for a missing id, a topic with no usable producer, a failed batch creation or a failed fetch, unless the answer to Q-6 adds them.
- **OOS-15.** Telling a sweep send of a message that failed before from one of a message that never failed (D-11).
- **OOS-16.** A `SentBySweep` for a producer that confirms publication, such as Kafka, RabbitMQ or `InMemoryMessageProducer` (D-12). A later change could add a notification for a successful confirmation after a sweep send.

## Acceptance Criteria

**Setup unless an AC says otherwise:**
- **Mediator.** `OutboxProducerMediator` constructed directly, with the feature on at default capacity.
- **Clock.** A `FakeTimeProvider` at 2026-01-01T12:00:00Z.
- **Pipeline.** `CommandProcessor.OutboxProducer` with no retry.
- **Outbox.** `InMemoryOutbox`, holding message `m1` on topic `orders` and, where an AC says so, `m2` and `m3`. A *failing outbox* double wraps it and throws on request from `AddAsync`, `Get`, `GetAsync`, `OutstandingMessagesAsync`, `MarkDispatched` or `MarkDispatchedAsync`.
- **Producers.**
  - A throwing non-confirming producer: `FakeErroringMessageProducerSync` or `FakeErroringMessageProducerAsync`, on topic `orders`.
  - A *scripted throwing producer* double, async, that throws the exception or the sequence of exceptions the test gives.
  - A *succeeding non-confirming producer* double, sync, async and bulk as an AC requires. `InMemoryMessageProducer` does not serve here, because it implements `ISupportPublishConfirmation` (`InMemoryMessageProducer.cs:43`).
  - A *throwing confirming producer* double, sync, async and bulk as an AC requires.
  - A *sync confirming producer* double, with no async role, that raises `OnMessagePublished` with a result the test gives.
  - An *async-callback confirming producer* double, with sync and async roles and `UseAsyncPublishConfirmation = true`, that raises `OnMessagePublishedAsync` with a result the test gives.
  - `InMemoryMessageProducer` with `PublishFailurePredicate` for a failed confirmation (`InMemoryMessageProducer.cs:126`), and with `UseAsyncPublishConfirmation = true` for one on the deferred pump (`InMemoryMessageProducer.cs:112`).
- **Subscriber.** A test subscriber that records what it receives. On request it does not read, blocks, or throws. Waits for a delivery are bounded by 5 seconds of wall-clock time.
- **Logger.** A test logger that captures entries.

**AC-1 (FR-1, NFR-4).** **Given** the feature is not turned on, with `AddProducers`, `UseOutboxSweeper`, `UseOutboxStaleMessageMonitor` and `AddBrighterInstrumentation` all called, and a throwing async producer, **When** `ClearOutboxAsync(["m1"])` runs, **Then** it returns, `m1` is undispatched, nothing throws, and, where the ADR's API lets a test reach a channel, nothing can be read from it.

**AC-2 (FR-2).** **Given** a capacity of 0, then -1, then one above any maximum the ADR sets, **When** the feature is set up and the mediator constructed, **Then** each throws a `ConfigurationException` naming the setting and the value, no later than that construction. **And given** a capacity of 0 on the DI path, **When** the step runs and `IAmAnOutboxProducerMediator` is first resolved, **Then** one of them throws, with that `ConfigurationException` as the exception or as its inner exception.

**AC-3 (FR-2, FR-9).** **Given** no capacity, **Then** the documented default applies: one more undelivered notification than that default gives a dropped count of 1. **And given** a capacity of 1, **Then** construction succeeds.

**AC-4 (FR-3.1, FR-5).** **Given** a scripted throwing producer whose exception is `E`, **When** `ClearOutboxAsync(["m1"])` runs at 12:00:00, **Then** the method returns and exactly one notification has id `m1`, topic `orders`, kind `SendFailed`, exception `E`, occurred at 12:00:00, and no other field.

**AC-5 (FR-3.1).** **Given** a throwing non-confirming sync producer, **When** `ClearOutbox(["m1"])` runs, **Then** one notification of kind `SendFailed`.

**AC-6 (FR-3.1).** **Given** a throwing confirming sync producer, **When** `ClearOutbox(["m1"])` runs, **Then** one notification of kind `SendFailed` with the exception, although the mediator discards that result at `:1113` today.

**AC-7 (FR-3.1, FR-5, FR-6).** **Given** `m1`, `m2` and `m3` on `orders`, all old enough, and a throwing bulk producer, **When** `ClearOutstandingFromOutboxAsync` runs with `useBulk` true, **Then** there are three notifications, one per id, each with topic `orders`, kind `SendFailed`, and the same exception object.

**AC-8 (FR-3.1).** **Given** a throwing async producer, **When** `ClearOutstandingFromOutboxAsync` runs with `useBulk` false on `m1` and `m2`, **Then** two notifications of kind `SendFailed`, one per id.

**AC-9 (FR-3.1).** **Given** a producer whose send waits on its token, **When** `ClearOutboxAsync(["m1"])` runs and the token is cancelled, **Then** one `SendFailed` notification with an `OperationCanceledException`.

**AC-10 (FR-3.2, FR-5).** **Given** `InMemoryMessageProducer` with `UseAsyncPublishConfirmation = true` and a predicate that fails `m1`, **When** `ClearOutboxAsync(["m1"])` has returned and the pump then raises the failure, **Then** one notification with id `m1`, topic `orders`, kind `ConfirmationFailed` and no exception.

**AC-11 (FR-3.2, FR-5).** **Given** the sync confirming producer, **When** it raises `(false, Id.Empty, null, null)`, **Then** one notification with id `Id.Empty`, topic `RoutingKey.Empty` and kind `ConfirmationFailed`. **And** nothing throws on the raising thread. **And given** a `CallViaExternalBus` on the sync confirming producer, **When** it raises a failed result for that message, **Then** one `ConfirmationFailed` notification.

**AC-12 (FR-3.3).** **Given** a succeeding non-confirming producer and the failing outbox throwing `E` from mark dispatched, **When** each of `ClearOutbox(["m1"])`, `ClearOutboxAsync(["m1"])`, and a bulk sweep of `m1` and `m2` runs, **Then** there is one notification of kind `MarkDispatchedFailed` with exception `E` per message, and the message is undispatched.

**AC-13 (FR-3.3, FR-4, FR-5, FR-14).** **Given** the sync and the async-callback confirming producers, each raising `(true, "m1", "orders", null)`, and the failing outbox throwing `E` from mark dispatched, **Then** one notification with id `m1`, kind `MarkDispatchedFailed` and exception `E`. **And given** the result `(true, Id.Empty, "orders", null)`, **Then** one such notification with id `Id.Empty`. **And given** a registry with no `CommandProcessor.OutboxProducer` pipeline, **When** each producer raises `(true, "m1", "orders", null)`, **Then** one `MarkDispatchedFailed` notification with the `KeyNotFoundException` from the pipeline lookup, and nothing throws on the raising thread. **And given** a `CallViaExternalBus` on the async-callback confirming producer, which then raises success for that message, **When** the failing outbox throws `E` from `MarkDispatchedAsync`, as a store does for a missing row, **Then** one `MarkDispatchedFailed` notification with `E`. **And when** the outbox is `InMemoryOutbox`, **Then** no notification.

**AC-14 (FR-4, FR-7).** **Given** `m1` in the outbox and a working producer, **When** `ClearOutbox(["m1", "ghost"])` and, separately, `ClearOutboxAsync(["m1", "ghost"])` run, **Then** `m1` is dispatched, no exception reaches the caller, the existing `Error` log entry is written, and no notification is raised.

**AC-15 (FR-4, FR-7).** **Given** `m1` on a topic with no producer, **When** `ClearOutboxAsync(["m1", "m2"])` runs, **Then** the same `ConfigurationException` as at commit 14850234f reaches the caller, `m2` is not attempted, and no notification is raised. **And given** a sweep over the same messages, a bulk sweep where `CreateBatchesAsync` throws, and a bulk sweep where the producer is not a bulk producer, **Then** each sweep throws as it does today and no notification is raised.

**AC-16 (FR-4).** **Given** the failing outbox throwing from `Get`, `GetAsync` and `OutstandingMessagesAsync` in turn, **When** the matching clear runs, **Then** the exception propagates as at commit 14850234f and no notification is raised.

**AC-17 (FR-4, NFR-3).** **Given** each of these in turn, **Then** no notification is raised:
- a clear in which every message succeeds;
- a sweep that runs while another holds the semaphore;
- a sweep where the breaker has `orders` tripped and `m1` is on `orders`;
- a failing `AddToOutboxAsync`, which still throws `ChannelFailureException`;
- `CallViaExternalBus` with a non-confirming producer that throws;
- a confirming producer that never raises a result;
- `InMemoryMessageProducer` reporting success to a mediator whose outbox implements only the sync interface;
- a send of a clear with no `CommandProcessor.OutboxProducer` pipeline, where the `KeyNotFoundException` reaches the caller;
- a send of a clear whose logger throws inside the helper's catch, where the exception from that log call reaches the caller.

**AC-18 (FR-6).** **Given** a pipeline with 3 retries and a scripted throwing producer that throws `E1` to `E4` in turn, **When** `ClearOutboxAsync(["m1"])` runs, **Then** four sends and one notification, with `E4`.

**AC-19 (FR-6).** **Given** a non-confirming async producer that fails twice and then succeeds, **When** three sweeps run on `m1`, **Then** two notifications, both for `m1`, and none for the third sweep. **And given** the FR-16 option on, **Then** three notifications for `m1`: `SendFailed`, `SendFailed`, then `SentBySweep`.

**AC-20 (FR-7).** **Given** a counting circuit breaker and a counting meter, **When** the same scripted failures run with the feature off, on, and on with the FR-16 option (a failed sync send, a failed async send, a failed batch, a failed confirmation, a failed mark dispatched, a success), **Then** the `TripTopic` calls and the `IAmABrighterOutboxMeter` calls are identical in every run. **And** the failed sync send makes no `TripTopic` call in any.

**AC-21 (FR-7).** **Given** a circuit breaker whose `TripTopic` throws, **When** an async send fails, **Then** the breaker's exception propagates as at commit 14850234f and the notification is still raised. **And when** a confirmation fails, **Then** the notification is raised, and the exception goes to the catch at `:949` without raising a second one. **And when** a batch send fails, **Then** one notification per id is raised and the breaker's exception propagates. **And when** a failed confirmation arrives on the sync callback, **Then** the notification is raised and the exception goes to the catch at `:1043` without raising a second one.

**AC-22 (FR-8).** **Given** a subscriber that has read the notification for a failed clear of `m1`, **When** it reads again with no further failure, **Then** it does not receive that notification again, and `m1` is still returned by `OutstandingMessagesAsync`.

**AC-23 (FR-9, FR-11).** **Given** capacity 2 and a subscriber that does not read, **When** n1, n2 and n3 are raised in order and the subscriber then reads, **Then** it receives exactly two of them in the order raised, and the dropped count reads 1. The ADR's policy fixes which two (P-3).

**AC-24 (FR-9, NFR-9).** **Given** capacity 1, no reads, and the log interval the ADR sets, written I, **When** 5 notifications are raised at time T, then 1 at T + I minus one second, then 1 at T + I, **Then** exactly two `Warning` entries, one at T and one at T + I, each naming the dropped count then. **And** no such entry when nothing is dropped.

**AC-25 (FR-10).** **Given** a sequential clear of `m1`, `m2` and `m3` that all fail, **Then** the subscriber receives them in that order.

**AC-26 (FR-11).** **Given** a subscriber that starts after two failures, **When** it starts, **Then** it receives both.

**AC-27 (FR-11).** **Given** a subscriber whose read loop throws on the first notification and ends, **When** a further failure occurs, **Then** no exception reaches the clear, and the channel behaves as with an absent subscriber (AC-23).

**AC-28 (FR-11, FR-12, NFR-1).** **Given** capacity 1 and a subscriber blocked indefinitely, **When** 1,000 `ClearOutboxAsync` calls fail, **Then** all return within 30 seconds of wall-clock time and the dropped count is at least 998.

**AC-29 (FR-12).** **Given** a subscriber already waiting on an empty buffer, whose code after the read blocks until released, **When** `ClearOutboxAsync` fails, **Then** the call returns while the subscriber is still blocked. **And** a producer that raises a failed confirmation on its own thread regains control while the subscriber is still blocked.

**AC-30 (FR-13).** **Given** two buffered notifications and a blocked subscriber, **When** `Dispose` runs, **Then** it returns while the subscriber is blocked. Once released, the subscriber reads two notifications and can tell that the channel has ended. **And** a subscriber that first reaches the channel after `Dispose` reads the same way, and nothing throws.

**AC-31 (FR-13).** **Given** a producer whose `Dispose` raises a failed confirmation, **When** the mediator is disposed, **Then** that notification is delivered. **And given** a failed confirmation raised after `Dispose` has returned, **Then** nothing throws, the dropped count goes up by 1, and there is no log entry from the feature.

**AC-32 (FR-14).** **Given** two mediators with their own subscribers and separate producer registries, **When** the first has a failure, **Then** only its subscriber receives a notification, and the second's dropped count stays 0.

**AC-33 (FR-14).** **Given** the same scripted failures, **When** they run against `InMemoryOutbox` and against an outbox double that implements only the async interface, and one that implements only the sync interface, **Then** the notifications for the paths each supports are the same, and no method is called on any outbox beyond those it receives with the feature off.

**AC-34 (FR-11, FR-15).** **Given** `AddProducers` with the explicit step and a failing producer, **When** the host starts and `PostAsync` fails, **Then** a subscriber started from a hosted service receives one notification. **And given** a subscriber that starts reading before `IAmAnOutboxProducerMediator` is first resolved, **When** it is then resolved and a clear fails, **Then** that subscriber, which has waited until then, receives the notification.

**AC-35 (NFR-2).** **Given** capacity 1, no reads, and a logger that throws when the FR-9 entry is written, **When** clears fail, **Then** each returns as with the feature off, and later notifications are still accepted.

**AC-36 (NFR-5).** **Given** capacity 10 and no reads, **When** 1,000 notifications are raised, **Then** a later read returns exactly 10 and the dropped count is 990. **And** the notification type has no member exposing a `Message` or `MessageBody`.

**AC-37 (NFR-6).** **Given** a capacity of 8,000 and 8 threads each raising 1,000 failures with distinct ids, **Then** the subscriber receives 8,000 notifications with 8,000 distinct ids, and the dropped count is 0.

**AC-38 (NFR-7).** **Given** a subscriber waiting with an empty buffer and no time advanced on the fake clock, **When** a clear fails, **Then** the notification reaches the subscriber within the 5 second tolerance.

**AC-39 (NFR-10).** **Given** the feature's log entries in the tests above, **Then** none contains a message body. **And given** a fault inside the feature, including in the mediator's batch guard, **Then** its entry names the exception type and has a null `Exception`.

**AC-40 (NFR-11).** **Given** the PR that adds the feature and the page in BrighterCommand/Docs, **Then** every new public type and member has XML documentation, and the page covers each item NFR-11 lists. A person checks this by reading it.

**AC-41 (FR-16, FR-5, FR-15).** **Given** the FR-16 option on, `m1` and `m2` on `orders`, both old enough, and a succeeding non-confirming async producer, **When** `ClearOutstandingFromOutboxAsync` runs with `useBulk` false at 12:00:00, **Then** two notifications, one per id, each with topic `orders`, kind `SentBySweep`, no exception and occurred at 12:00:00, and both messages are dispatched. **And given** a succeeding non-confirming bulk producer, **When** it runs with `useBulk` true, **Then** the same two notifications. **And given** the option set on the DI path with `includeSweepSends: true`, **When** the same sweep runs with `useBulk` false, **Then** one `SentBySweep` notification for `m1` and one for `m2`, with no assertion on occurred at, because that path builds the mediator with `TimeProvider.System` (`ServiceCollectionExtensions.cs:848`).

**AC-42 (FR-16, FR-10).** **Given** the FR-16 option on, a succeeding non-confirming async producer and the failing outbox throwing `E` from `MarkDispatchedAsync`, **When** a sweep runs on `m1`, **Then** `SentBySweep` for `m1`, then `MarkDispatchedFailed` for `m1` with `E`. **And given** `InMemoryMessageProducer`, a confirming producer, and a predicate that fails `m1`, **When** a sweep runs on `m1` with `useBulk` false and, on a fresh mediator and producer, with `useBulk` true, **Then** in each run one notification, `ConfirmationFailed` for `m1`, and no `SentBySweep`.

**AC-43 (FR-16, OOS-13).** **Given** the FR-16 option on and a succeeding non-confirming producer, sync and async, **When** `ClearOutbox(["m1"])`, `ClearOutboxAsync(["m2"])` and a `CallViaExternalBus` each succeed, **Then** no notification. **And given** a succeeding confirming producer that raises `(true, "m3", "orders", null)` after a sweep sent `m3`, **Then** no notification.

**AC-44 (FR-16, NFR-3).** **Given** the feature on without the FR-16 option, on both the DI path and the direct path, and a succeeding non-confirming async producer with `useBulk` false and, separately, a succeeding non-confirming bulk producer with `useBulk` true, **When** a sweep sends `m1` and `m2` without an exception, **Then** no notification is raised.

### Requirement → Acceptance Criteria map

| Requirement | AC | Requirement | AC |
|---|---|---|---|
| FR-1 | AC-1 | FR-13 | AC-30, AC-31 |
| FR-2 | AC-2, AC-3 | FR-14 | AC-13, AC-32, AC-33 |
| FR-3.1 | AC-4 to AC-9 | FR-15 | AC-34, AC-41 |
| FR-3.2 | AC-10, AC-11 | NFR-1 | AC-28 |
| FR-3.3 | AC-12, AC-13 | NFR-2 | AC-35 |
| FR-4 | AC-13, AC-14 to AC-17 | NFR-3 | AC-17, AC-44 |
| FR-5 | AC-4, AC-7, AC-10, AC-11, AC-13, AC-41 | NFR-4 | AC-1 |
| FR-6 | AC-7, AC-18, AC-19 | NFR-5 | AC-36 |
| FR-7 | AC-14, AC-15, AC-20, AC-21 | NFR-6 | AC-37 |
| FR-8 | AC-22 | NFR-7 | AC-38 |
| FR-9 | AC-3, AC-23, AC-24 | NFR-8 | None: the build and the existing suite |
| FR-10 | AC-25, AC-42 | NFR-9 | AC-24 |
| FR-11 | AC-23, AC-26, AC-27, AC-28, AC-34 | NFR-10 | AC-39 |
| FR-12 | AC-28, AC-29 | NFR-11 | AC-40 |
| FR-16 | AC-19, AC-41 to AC-44 | | |

FR-3 is met through its sub-clauses.

## Additional Context

### Decisions settled from the code

| # | Decision | Alternative | Cost of the alternative |
|---|---|---|---|
| D-1 | One channel. Notify each failed clear. No "gave up" notification. | A second signal when the outbox gives up. | There is no such point in the code. It needs an attempt count in every store, with a schema change, and a limit that changes behaviour. |
| D-2 | The exception swallowed by `ExecuteWithResiliencePipeline(Async)` reaches the notification. How is for the ADR; those methods are private. | Leave them returning only a bool, with no exception on the notification. | A subscriber cannot tell a size limit from a broker outage, the difference that decides its reaction. |
| D-3 | Sites are named in FR-3 and are independent of `TripTopic`. | Raise from `TripTopic`. | Misses a direct sync send failure, since only a sync confirmation trips (`:1040`), and all mark-dispatched failures. |
| D-4 | A failed confirmation is a failed clear. | Report only failures inside the clear call. | Misses a broker refusal reported as `ProduceException` on the async Kafka path (`KafkaMessagePublisher.cs:56-67`), and all RabbitMQ nacks. |

### Decided in this spec

The requirements above state these. Each row gives the alternative.

| # | Decision | Alternative | Cost of the alternative |
|---|---|---|---|
| D-5 | Id and topic only, no message. | A nullable `Message`, when in hand. | Two shapes that a subscriber must handle, null at every confirmation, and a field that keeps up to capacity × body size. This avoids the cost of the field, not what an exception retains (NFR-5). Accepted cost: an extra read by id. |
| D-6 | One notification per message in a batch. | One per batch with a list of ids. | Two shapes, and capacity no longer bounds the number of ids held. Accepted cost: up to a batch's worth of notifications for one failure. |
| D-7 | Each failed attempt notifies. | Once per message id. | State per id with no bound, lost on restart, and a subscriber that missed the first hears nothing again. Accepted cost: volume against a broker that is down (FR-9). |
| D-8 | A cancelled send notifies. | Suppress `OperationCanceledException`. | The message is still undispatched and the mediator treats it as a failure, tripping the topic at `:1313`. A subscriber can filter on the exception. Accepted cost: an aborted HTTP request produces a notification. |
| D-9 | The notification keeps the exception object, with the warning in NFR-5. | Flatten it to type and message. | The subscriber loses pattern matching and the stack, which is why D-2 carries the exception at all. |
| D-10 | A `Call`'s confirmation is handled like any other, so a successful one raises `MarkDispatchedFailed` on a store that throws on a missing row (FR-3.3). | Record in-flight `Call` ids in the mediator and stay silent for them. | New state on the mediator, with a bound and eviction because a confirmation may never arrive (`RmqMessageProducer.cs:231`), and no cover for a producer shared by two mediators (FR-14). Telling the stores' exceptions apart instead needs store-specific code, against FR-14 and C-1. Accepted cost: one false alarm per successful `Call` on those stores. |
| D-11 | `SentBySweep` for every sweep send, whether or not the message failed before. | Only for ids that raised a failure earlier. | State per id with no bound, lost on restart and blind to a failure on another instance, the cost D-7 already rejects. Accepted cost: a sweep send of a message that never failed also notifies, and the subscriber matches ids against its own records. |
| D-12 | Raise `SentBySweep` only for a producer that does not confirm publication, when the send returns without an exception and before `MarkDispatched`. | Raise it for confirming producers too, in no defined order with their confirmation; or after `MarkDispatched`, or after a successful confirmation. | A confirming producer can report a `ConfirmationFailed` before or after its send returns (inline in `InMemoryMessageProducer`, on a worker in Kafka, on the client's thread in RabbitMQ), so a subscriber could see the failure first and undo a compensation for a message the broker refused. Waiting for the confirmation needs ids in flight recorded on the mediator, with a bound and eviction, the state D-10 rejects. Accepted cost: no `SentBySweep` for confirming producers (OOS-16), and a `MarkDispatchedFailed` or `SendFailed` for the same id can still follow. |

### Proposed, for the ADR

The requirements above state the needs. These are proposals for how to meet them, each with its alternative. The ADR confirms or replaces them, and a clause that cites one changes only if the ADR replaces it.

| # | Proposal | Alternative | Cost of the alternative |
|---|---|---|---|
| P-1 | A bounded channel from `System.Threading.Channels`, which `InMemoryJobChannel` also uses (`InMemoryJobChannel.cs:72`) and which can be configured with drop-oldest. It adds no dependency: the core references it (`Paramore.Brighter.csproj:30`) and already uses it (`InMemoryMessageProducer.cs:48-49`). The ADR confirms that the dropped count can be exact on every target framework. | An event, or a handler role. | A plain .NET event raised inline does not meet FR-12. A handler role means Brighter runs subscriber code and needs its own loop and exception handling. |
| P-2 | Default capacity 1,024, valid values 1 to 1,048,576. | Another default; no maximum. | A smaller default loses more in a burst. A larger one, or no maximum, retains more exceptions (NFR-5). |
| P-3 | When full, drop the oldest. | Drop the newest; or wait. | Waiting breaks NFR-1. Dropping the newest hides the current failures. Accepted cost: with no sweeper, a dropped oldest failure does not recur, and only the stale gauge can show its message. |
| P-4 | The FR-9 `Warning` at most once per 60 seconds of `TimeProvider` time. | A log entry per drop; or the count only. | One per drop floods the log during an outage. With the count only, an application that does not read the count never learns of the loss. |
| P-5 | One reader per notification, and the application fans out. A second reader competes for notifications or is rejected, as the ADR says. | Built-in fan-out. | Adds per-subscriber buffers and drop counts. An application can fan out itself. |
| P-6 | `SentBySweep` travels on the same channel as the failures, behind an option that is off by default. | On whenever the feature is on; or a channel of its own. | Always on: an application that dispatches through the sweeper and a non-confirming producer, depositing and never clearing, gets one notification per message, up to about 20 a second per sweeping instance with the sweeper's defaults, and with drop-oldest those can push failures out of a buffer nobody reads. A channel of its own: a second capacity, dropped count and reader, and a subscriber loses the order between a failure and the sweep send of the same id. |

### Open questions for the ADR and the maintainer

- **Q-1. What are the channel's type and API, and how is it turned on?** P-1 proposes the type.
  - Facts that bear on it:
    - The DI path builds the mediator through `Activator.CreateInstance` with positional arguments (`ServiceCollectionExtensions.cs:828-852`), so a new constructor parameter must also be added there.
    - `BuildOutBoxProducerMediator` takes the class `ProducersConfiguration` (`ServiceCollectionExtensions.cs:820`), so a new property, if the ADR chooses one, goes on the class, and its default means off.
    - `ServiceCollectionExtensions.cs:565` replaces an application's own implementation of `IAmProducersConfiguration` with a new `ProducersConfiguration`, so a property on the class would leave the feature off in silence for that application.
    - NFR-8 rules out a new member on `IAmAnOutboxProducerMediator`, and the DI path registers only that interface (`ServiceCollectionExtensions.cs:416`, `:554`). The subscriber therefore probably reaches the channel through an object of its own passed to the constructor.
  - Still open:
    - What a subscriber sees when the feature is off, including an application whose own `IAmProducersConfiguration` leaves it off in silence. A read that ends at once looks like a normal end and hides a forgotten step.
    - Whether one reader is enough (P-5). "Subscribe" may suggest several, and FR-11 assumes one.
    - How a waiting reader ends when no mediator was ever built. FR-13 closes the channel only on a mediator's `Dispose`, so the ADR gives such a reader a way out, at the least its own cancellation.
    - Whether two mediators on the direct path can receive the same object. FR-14 gives each mediator its own channel, so the ADR rejects sharing or says what replaces that rule.
- **Q-2. Should a failed confirmation carry a reason?** `PublishConfirmationResult` has none, and Kafka's reason reaches only a log (`KafkaMessagePublisher.cs:61`), so a message over the size limit on Kafka notifies with no exception. An additive `init` property set by the Kafka producer could fix this later. This spec leaves it out (OOS-11), because it changes a transport and a record ADR `0063-failed-delivery-context` defined.
- **Q-3. A TTL on an outbox.** With a TTL set, DynamoDB and Firestore stamp an expiry on add. Whether a row is deleted depends on the store, as "One channel or two" says. Where one is deleted, there is no signal. Is a documented limitation enough?
- **Q-4. An instrument for raised and dropped notifications?** None in this spec (OOS-8), to keep ADR `0084-outbox-metrics` untouched. It could be added later without changing this contract.
- **Q-5. Final names.** Kinds, the notification type, the step and the FR-16 option. With `SentBySweep`, one kind is not a failure, so a name that says "failure" covers the channel's main use and not every notification. Public names need a qualifier that keeps them apart from a Brighter consumer channel (`IAmAChannelSync`, `ChannelFailureException`, `IHaveAChannelFailureCount`).
- **Q-6. Is three kinds the right reading of "any failed Clear(Async)"?** This spec narrows those words. Only `SendFailed`, `ConfirmationFailed` and `MarkDispatchedFailed` notify a failure. A fetch that throws, a topic with no usable producer, a bulk sweep whose batch creation throws and an id not in the outbox do not (FR-4):
  - On an explicit clear, a failed fetch and a missing producer already reach the caller. On a sweep, those and a failed batch creation end it as `SweepOutcome.Failed`, which reaches the log and, where enabled, the sweep meter and, after consecutive failures, the sweeper's health check (`OutboxSweeperInvestigateHealthCheck.cs:93`). None of these names a message or reaches application code at the time, and the rest of that sweep is not attempted. A fetch failure also reaches no message.
  - A failed batch creation can be a broker outage. On Azure Service Bus, creating a batch needs a link on a new sender, and that happens outside the pipeline (`AzureServiceBusMessageProducer.cs:151`; `AzureServiceBusMessageBatch.cs:56`; `OutboxProducerMediator.cs:1199`). So if the broker is already unreachable at that point, an outage that notifies per message on a sweep without bulk would notify nothing on a bulk sweep. `UseBulk` is off by default (`TimedOutboxSweeperOptions.cs:57`).
  - A missing id is logged at `Error`. The doc comment at `:377-380` gives compaction as an example, so a notification could be a false alarm for a message already dispatched. The cost of leaving it out: it is the one case with nothing but a log, because it has no row and so no age.
  - Should any of these notify? Adding one before code means a further site or kind and its ACs. A notification with no known topic would carry `RoutingKey.Empty`, the way FR-5 treats a confirmation with none.
- **Q-7. Trace context on a notification?** None in this spec. A subscriber reaches `TraceParent` by reading the message by id (`MessageHeader.cs:345`).
- **Q-8. Should a `Call` notify?** A `Call` is not a clear and its message has no row, but its confirmation reaches the same handlers (FR-3.2). A failed one notifies here, for a message the subscriber cannot read by id. On DynamoDB and Firestore, with a confirming producer, a successful one raises `MarkDispatchedFailed` (FR-3.3), and that path logs an `Error` today. The mediator could record in-flight `Call` ids and stay silent (D-10), and a separate change could stop marking a `Call`'s message at all, which would also remove that log. Is the false alarm acceptable until then?
