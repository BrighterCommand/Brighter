# Bugfix: MongoDb and Firestore outstanding counts have no time bound of their own, so a stuck count freezes the mediator's limit check

**Linked Issue**: issue #4584
**Status**: Verified

## Symptom

**Observed.** `OutboxProducerMediator` counts outstanding messages on a background task, one check at a time. The in-flight flag is released only when the count ends. While a count is stuck, the mediator never counts again, and `CheckOutboxOutstandingLimit` keeps comparing every post against the last cached count. Either every post throws `OutboxLimitReachedException`, or no post ever throws, however much the outbox grows.

PR #4597 removed the static semaphore, so the freeze no longer spreads to other mediators. PR #4613 gave the relational outboxes' count a timeout (15 s unless `OutstandingCountTimeout` is set). The DynamoDB count already gives up after `DynamoDbConfiguration.Timeout` (500 ms by default). Brighter sets no bound on the count of two outboxes it ships:

- `MongoDbOutbox`: when the server holds the count's reply while it keeps answering the driver's monitor, neither Brighter nor the driver ends the count. The driver's socket timeout is 0, so the read waits as long as the reply is held. Measured: still running after 150 s, past the ~70 s at which the full-path stall ended.
- `FirestoreOutbox`: when the server's replies stop, the count ends only at the client library's default deadline for this call, 300 s.

**Expected.** The count gives up after a bounded time and throws. The mediator's existing catch reads the count as 0, so the limit fails open, as it does for DynamoDB and the relational outboxes.

**Reproduction.** Run a count against a server whose reply never comes. For MongoDb, use a `failCommand` failpoint with `blockConnection` on `aggregate`, filtered by the client's `appName`. Heartbeats are `hello` commands, which the failpoint does not list, so the monitor keeps getting answers; the `appName` filter leaves other clients, such as the slow-count clients, unaffected. For Firestore, the probes used a TCP proxy that stops forwarding replies after warm-up; the regression test drops the replies inside the client's HTTP handler instead. Measured results are under Evidence.

## Suspected Location

Line numbers in this file are at 307da3eaf, before the fix.

Mediator (shared path, not the defect itself):
- `src/Paramore.Brighter/OutboxProducerMediator.cs:193-194`: an outbox that implements both interfaces sets both `_outBox` and `_asyncOutbox`.
- `src/Paramore.Brighter/OutboxProducerMediator.cs:822-867` (`CheckOutstandingMessages`): the flag is claimed at `:842` and released only in the `finally` of the `Task.Run` at `:853-863`.
- `src/Paramore.Brighter/OutboxProducerMediator.cs:1422-1473` (`OutstandingMessagesCheck`): the sync outbox is tried first (`:1431-1442`), with no token. The catch at `:1463-1468` sets the count to 0.

MongoDb:
- `src/Paramore.Brighter.Outbox.MongoDb/MongoDbOutbox.cs:984-985`: `CountDocuments(..., new CountOptions { Limit = ... })`, with no `MaxTime` and no token. `:1005-1007` (async) passes only the caller's token.
- `src/Paramore.Brighter.MongoDb/MongoDbConfiguration.cs:32-35`: `new MongoClient(connectionString)`, with no timeout added.

Firestore:
- `src/Paramore.Brighter.Outbox.Firestore/FirestoreOutbox.cs:451`: `client.RunAggregationQuery(request)`, with no `CallSettings`. `:1267` (async) passes `CallSettings.FromCancellationToken` only.
- `src/Paramore.Brighter.Firestore/FirestoreConnectionProvider.cs:19-21`: the client is built with no `Settings` unless the application sets them through `FirestoreConfiguration.Configure` (invoked at `:20`), so the library defaults apply by default.

## Root-Cause Hypothesis

Neither outbox puts a deadline of its own on the outstanding count, and the mediator calls the sync count with no token. So the count lasts as long as the client library allows.

- MongoDb: the driver's socket timeout is infinite by default, so neither Brighter nor the driver bounds a count whose reply the server holds while the monitor stays healthy. Falsifiable: under a `failCommand` `blockConnection` on `aggregate`, the count either ends on its own before the probe stops waiting (refuted) or is still running when it stops, at 90 s, past the ~70 s at which the full-path stall ended (confirmed).
- Firestore: the default deadline for `RunAggregationQuery` is 300 s, and it also applies when the async path passes a token-only `CallSettings`.

My comment on issue #4584 said that a MongoDb count that never returns "would still freeze". Run 1 below showed that this does not hold when every reply from the server stops. It does hold while only the count's reply is held, which is the case above. The same comment said a stuck Firestore count "should end in the catch too, just five minutes late". It does (F1, F2), but the limit check stays frozen for those five minutes, which is what this fix bounds.

Suggested direction, from the PR #4613 precedent, where the maintainer asked for a configurable timeout with a sensible default: give each count its own bound. **UNVERIFIED, to be proven or refuted in /bugfix:confirm.**

## Confirmed Root Cause

Both outboxes leave the count unbounded, and the mediator adds no bound of its own. While the count runs, the in-flight flag stays claimed and `CheckOutboxOutstandingLimit` uses a stale count.

- **MongoDb, confirmed.** `CountDocuments` is sent with no `MaxTime` and no token. The driver's `SocketTimeout` defaults to 0, which means infinite. `MongoClientSettings.Timeout` and `CountOptions.Timeout` (client-side operation timeouts) are not public in MongoDB.Driver 3.12.0. With the count's reply held and the monitor answering (inferred: the failpoint lists only `aggregate` and no count threw in 150 s; heartbeat and pool events were not recorded), both Brighter counts were still running after 150 s. `timeoutMS` in the connection string is parsed but did not bound the count in the full-path stall (M4). When every server reply stops, heartbeats included, the counts threw about 70 s after the replies stopped, consistent with the driver's monitor clearing and pausing the pool (M1, M2: `MongoConnectionException`; M4: `MongoConnectionPoolPausedException`). That bound depends on the monitor's own timeouts; it is not one the count sets.
- **Firestore, confirmed.** `RunAggregationQuerySettings` has a 300 s default with no retry (`Google.Cloud.Firestore.V1` 4.4.0). GAX merges per-call settings so that null values do not override the default, so the async path keeps the 300 s too. Both Brighter counts ended at 300 s.

## Evidence

- [x] **Code-trace**
  1. `OutboxProducerMediator.cs:842` claims the flag. `:853-863` releases it in a `finally`. `:1431-1442` runs the sync count through an overload with no token (`IAmAnOutboxSync.cs:139`). `:1463-1468` turns any exception into 0.
  2. `MongoDbOutbox.cs:984-985` and `:1005-1007`; `FirestoreOutbox.cs:451` and `:1267`, as cited above.
  3. MongoDB.Driver 3.12.0, read by reflection: `SocketTimeout` 0, `ConnectTimeout` 30 s, `ServerSelectionTimeout` 30 s, `HeartbeatInterval` 10 s, `RetryReads` true. `Timeout` exists on `MongoClientSettings`, `MongoUrl` and `CountOptions`, but its getter is not public. `CountOptions.MaxTime` is public. Two clients built from the same connection string share one cluster and one pool. A different `appName` gives a separate cluster; a different `timeoutMS` does not.
  4. Reading the driver's IL: a cancelled token makes the sync socket read dispose the stream, and a heartbeat that times out clears the pool. The probes saw only the end result of each (B3, M4), not these steps in isolation.

- [x] **Probes (executed)**: a throwaway console app outside the repository, since deleted, built against `307da3eaf`. The probe called `MongoDbOutbox` and `FirestoreOutbox` directly, with `TimeSpan.Zero` and default arguments, not through the mediator loop, on net9.0 (the driver's net472 build was not run). There were three runs against the Firestore emulator and `mongo:7`: run 1, the full-path stall, on a plain `mongo:7`; runs 2 and 3, the count-only and slow-count cases, on `mongo:7` started with `enableTestCommands`.

  Count-only stall on MongoDb: a `failCommand` failpoint blocks `aggregate` from `appName=blocked` for 600 s. Heartbeats are `hello`, not `aggregate`, so the monitor keeps getting answers. Each case had its own cluster, and `CommandStartedEvent` showed an `aggregate` was sent for every case. The B cases ran in runs 2 and 3, with the probe waiting 150 s and then 90 s, and the runs agree. "Still running" means the call had not returned when the probe stopped waiting.

  | Case | Result | Time |
  |---|---|---|
  | B1 Brighter sync count (mediator path) | still running | 150 s and 90 s |
  | B2 Brighter async count, no token | still running | 150 s and 90 s |
  | B3 `CountDocuments` with a token cancelled after 3 s | `OperationCanceledException` | 3.0 s |
  | B4 `CountDocuments` with `MaxTime` 3 s | still running | 150 s and 90 s |

  Slow count on a healthy MongoDb server: the filter runs a 60 s server-side JavaScript `sleep` on one document. These cases ran once, in run 3 (run 2's `$where` filter is not allowed in `aggregate`).

  | Case | Result | Time |
  |---|---|---|
  | S1 no bound | returns 1 | 60.0 s |
  | S2 `MaxTime` 3 s | `MongoExecutionTimeoutException` | 60.0 s |
  | S3 token cancelled after 3 s | `OperationCanceledException` | 3.0 s |

  S2 shows the server applies `MaxTime`, but only once the uninterruptible sleep returns. B4 is the same kind of case: a failpoint block that `maxTimeMS` did not interrupt. Neither shows how soon `MaxTime` stops a real scan.

  Full-path stall: a TCP proxy that drops every server reply after warm-up (for MongoDb, heartbeats included). M1 to M5 and F1 to F4 come from run 1, F5 from run 2.

  | Case | Result | Time |
  |---|---|---|
  | M1 to M3: Brighter sync and async counts, and `CountDocuments` with `MaxTime` 3 s (shared cluster) | `MongoConnectionException` | ~70 s |
  | M4 `timeoutMS=3000` in the connection string | `MongoConnectionPoolPausedException` | 69.9 s |
  | M5 `CountDocuments` with a token cancelled after 3 s | `OperationCanceledException` | 3.0 s |
  | F1 Brighter Firestore sync count (mediator path) | `RpcException(DeadlineExceeded)` | 300.0 s |
  | F2 Brighter Firestore async count, token-only `CallSettings` | `RpcException(DeadlineExceeded)` | 300.0 s |
  | F3 same client call, `CallSettings.FromExpiration(3 s)` | `RpcException(DeadlineExceeded)` | 3.1 s |
  | F4 expiration 3 s combined with a live token | `RpcException(DeadlineExceeded)` | 3.1 s |
  | F5 expiration 30 s combined with a token cancelled after 1 s | `RpcException(Cancelled)` | 1.0 s |

  M1 to M5 shared one cluster, so the ~70 s of M1 to M4 is one event of the driver's monitor, not four measurements. F3 to F5 read the stream with `Task.Run`, not with `BrighterAsyncContext.Run`. The gRPC deadline belongs to the call, so the reader should not matter, but the red test must go through `FirestoreOutbox`.

## Suggested-Fix Assessment

- **Firestore: CONFIRMED.** A per-call expiration bounds the call the sync count makes (F3). On the async path it can be combined with the token and both still work (F4, F5).
- **MongoDb: PARTIAL.** A configurable bound with a default fits. The bound has to be on the client side, since a server-side limit cannot end a wait for a reply that does not arrive. Only a token ended the count within the bound, whether its reply was held (B3) or the server was slow (S3); both are sync calls, and the async count with a token was not measured. B4 and S2 do not show what `MaxTime` does on a real scan: the server did not interrupt either wait (a failpoint block, a JavaScript sleep). `CountOptions.Timeout` is not available in 3.12.0.

## Scope Notes

Decided at the Confirm gate (2026-11-11): the MongoDb setting goes on `IAmAMongoDbConfiguration` as `OutstandingCountTimeout` (a source break, named in the release note), and the default is 15 s.

Chosen from the evidence:
- **MongoDb mechanism:** a `CancellationTokenSource` built from the bound through `Configuration.TimeProvider`, linked to the caller's token and passed to `CountDocuments` and `CountDocumentsAsync`, following the pattern of `src/Paramore.Brighter.Outbox.DynamoDB/DynamoDbOutbox.cs:858-861, 1337-1345`, which takes its `TimeProvider` from the constructor. `MaxTime` could be set to the same value so the server also stops its work, but the probes do not show what that adds. Only the token ended the count within the bound (B3, S3, M5); `MaxTime` did not end B4 or S2, and the CSOT `Timeout` is not public in MongoDB.Driver 3.12.0. The regression tests therefore expect an `OperationCanceledException`.
- **Cost of where the MongoDb setting lives:** `IAmAMongoDbConfiguration` is public and Mongo targets net472, which has no default interface members. Adding the member is a source break for custom implementations; the repo has none. Bugfix 0060-relational-outstanding-count-timeout (PR #4613) accepted the same kind of break. The alternative is `MongoDbCollectionConfiguration`, a concrete class. Both are shared with the inbox and locking (`MongoDbInbox.cs:23`, `MongoDbLockingProvider.cs:21`; the class is the type of the `Outbox`, `Inbox` and `Locking` collections, `IAmAMongoDbConfiguration.cs:49-63`), so an option on either would also appear on the inbox and locking configuration, where it has no effect.

Decided at the Firestore test gate (2026-11-11):
- **Firestore:** a `TimeSpan?` on `FirestoreConfiguration` (a concrete class, so not a break), where `null` means the 15 s default; it is also shared with the inbox and locking (`FirestoreConfiguration.cs:28, 38`). The bound is a per-call deadline: `CallSettings.FromExpiration` on the sync call, and the expiration combined with the caller's token on the async call, so a timeout surfaces as `RpcException` with `DeadlineExceeded`. A token of our own would surface as `Cancelled`, like a caller's cancellation (F5).

Also in the fix:
- **Range:** the setters reject zero and negative values (which covers `Timeout.InfiniteTimeSpan`), and values above `int.MaxValue / 1000` seconds, as in bugfix 0060-relational-outstanding-count-timeout. Neither API needs whole seconds. Unlike the relational outbox, `MongoDbOutbox` does not check the value a custom `IAmAMongoDbConfiguration` returns: zero, or a negative value other than `Timeout.InfiniteTimeSpan`, makes every count fail, and `Timeout.InfiniteTimeSpan` leaves the count with no bound of its own.
- **Release note:** a Master entry for issue #4584 that names the source break on `IAmAMongoDbConfiguration`.

Related, not in this fix:
- The sync `FirestoreOutbox.GetOutstandingMessageCount` (`:404-470`) filters only `IsDispatched == false`. It ignores `dispatchedSince` and `maxCount`, which the async count applies (`:1228-1236`, `:1248`). The mediator uses the sync path, so Firestore counts every undispatched message with no ceiling. ADR 0085-outbox-stale-message-telemetry kept that query on purpose, so the stale message count would not change the producer's check; this fix leaves it as it is. With a 15 s bound, a large healthy outbox could hit the bound and fail open.
- PR #4730 (open) adds a per-topic count to `MongoDbOutbox` and changes the Mongo test setup (`MongoDbOutboxProvider`, `OutstandingCountCancellationOutboxBaseTests`, `test-configuration.json`, the test generator). Its per-topic count is called only by the stale message monitor, which passes its own token and stops waiting after `MeasurementTimeout`. If it merges first, this branch merges master and resolves the conflict in `MongoDbOutbox.cs`.
- Tests:
  - The generated OutstandingCount suites drive only the async count, and both providers use `storeHeld: false`, so no test can make these counts hang today.
  - `docker-compose-mongodb.yaml` does not enable test commands, so `failCommand` is not available as configured.
  - A MongoDb listener that never replies would most likely fail at server selection (30 s), not in the count (inferred).
  - A proxy that freezes after warm-up makes the count throw at about 70 s before the fix.
  - Unlike MongoDb, Firestore needs no compose change: replies can be dropped inside a connection handler injected through `FirestoreConfiguration.Configure`.

## Regression Test
MongoDb, approved at the test gate (2026-11-11):
- `tests/Paramore.Brighter.MongoDb.Tests/Outbox/When_the_outstanding_count_is_held_should_give_up_after_the_default_timeout.cs`
- `tests/Paramore.Brighter.MongoDb.Tests/Outbox/When_the_async_outstanding_count_is_held_should_give_up_after_the_default_timeout.cs`
- Test double: `tests/Paramore.Brighter.MongoDb.Tests/TestDoubles/HeldReplyProxy.cs`, a loopback TCP proxy that relays to the compose MongoDb and, once told to hold, drops every reply.

Each test warms the connection with one count, holds the replies, and counts again. It asserts the count ends between 14 s and 18 s with an `OperationCanceledException` (the async path throws `TaskCanceledException`, a subclass). The proxy drops heartbeat replies too, so the tests reproduce the full-path stall (M1 to M5), not a reply held for the count alone (B1 to B4); the compose file is unchanged. Before the fix both fail on `Assert.InRange` at 69.99 s, when the driver gives up on the connection with `MongoConnectionException`. Each MongoDb test class takes about 40 s whatever the count's timeout, while the driver waits on its own timeouts.

Firestore, approved at the test gate (2026-11-11):
- `tests/Paramore.Brighter.Gcp.Tests/Outbox/Firestore/When_the_outstanding_count_is_held_should_give_up_after_the_default_timeout.cs`
- `tests/Paramore.Brighter.Gcp.Tests/Outbox/Firestore/When_the_async_outstanding_count_is_held_should_give_up_after_the_default_timeout.cs`
- Test doubles: `tests/Paramore.Brighter.Gcp.Tests/TestDoubles/HeldReplyConnections.cs`, a `SocketsHttpHandler` whose `ConnectCallback` wraps each connection, and `HeldReplyStream.cs`, which, once told to hold, reads and drops whatever the server sends. The handler goes in through `FirestoreConfiguration.Configure` (`GrpcNetClientAdapter.WithAdditionalOptions`), so the TLS of a real Firestore endpoint is done above the held stream.

Each test warms the connection (the sync test with one count, the async test with a get by id, which needs no composite index on a real Firestore), holds the replies, and counts again. It asserts the count ends between 14 s and 18 s with an `RpcException` whose status is `DeadlineExceeded`, so it expects a per-call deadline with a 15 s default; a cancellation token gives `Cancelled` instead (F5). Before the fix both fail on `Assert.InRange` at about 300 s (300.02 s and 300.03 s), the library default; the run stops at that assertion, so it does not observe the exception type. Run locally against the Firestore emulator. The `gcp-ci` job does not run for pull requests from forks, and no CI job runs the Firestore emulator, so CI runs these tests only after a merge.

Configuration, approved at the test gate (2026-11-11). These pass on arrival, because the code they cover was written with the fix, so each was checked red under a mutation of the production code, then green again with the mutation reverted:
- `tests/Paramore.Brighter.MongoDb.Tests/When_the_outstanding_count_timeout_is_out_of_range_should_throw.cs` and `tests/Paramore.Brighter.Gcp.Tests/Firestore/When_the_outstanding_count_timeout_is_out_of_range_should_throw.cs`: zero, -1 s, `Timeout.InfiniteTimeSpan`, 2147483.5 s, 2147484 s and `TimeSpan.MaxValue` throw `ArgumentOutOfRangeException`. Red with the range check removed (zero, -1 s, `Timeout.InfiniteTimeSpan`, 2147484 s and `TimeSpan.MaxValue` fail on `Assert.IsType`; that run came before the 2147483.5 s case was added), and with the cap compared in whole seconds (2147483.5 s fails).
- `tests/Paramore.Brighter.MongoDb.Tests/When_the_outstanding_count_timeout_is_the_largest_allowed_should_be_accepted.cs` and its Firestore twin in `tests/Paramore.Brighter.Gcp.Tests/Firestore/`: 2147483 s is accepted. Red with the cap check changed from `>` to `>=` (`Assert.Null` fails).
- `When_the_outstanding_count_timeout_is_configured_should_give_up_after_it.cs` and `When_the_async_outstanding_count_timeout_is_configured_should_give_up_after_it.cs`, in `tests/Paramore.Brighter.MongoDb.Tests/Outbox/` and `tests/Paramore.Brighter.Gcp.Tests/Outbox/Firestore/`: with 3 s configured, the held count ends between 2.5 s and 6 s. Red with the configured value ignored (`Assert.InRange` fails at about 15 s).

## Fix
Both counts now give up after `OutstandingCountTimeout`, 15 s when it is `null`.
- `src/Paramore.Brighter.MongoDb/IAmAMongoDbConfiguration.cs`, `MongoDbConfiguration.cs`: new `TimeSpan? OutstandingCountTimeout`; the class rejects values not greater than zero or above `int.MaxValue / 1000` seconds.
- `src/Paramore.Brighter.Outbox.MongoDb/MongoDbOutbox.cs`: the sync count passes a token from `Configuration.TimeProvider.CreateCancellationTokenSource(timeout)`; the async count links it with the caller's token.
- `src/Paramore.Brighter.Firestore/FirestoreConfiguration.cs`: new `TimeSpan? OutstandingCountTimeout`, same range.
- `src/Paramore.Brighter.Outbox.Firestore/FirestoreOutbox.cs`: the sync count passes `CallSettings.FromExpiration`; the async count adds `WithExpiration` to its token-only `CallSettings`.

After the fix, net9.0, local MongoDb and Firestore emulator: the MongoDb suite passes 238 of 238. On both backends the regression and configuration tests pass, 11 test cases each; on Firestore, the Firestore category fails 15 `Firestore.Inbox.*` tests with `Unauthenticated`, the same 15 that fail at 307da3eaf. Each held-count MongoDb test class takes about 40 s; each held-count Firestore test takes about 15 s with the default.
