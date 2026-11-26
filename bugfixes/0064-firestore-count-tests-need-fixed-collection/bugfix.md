# Bugfix: Firestore outstanding count facts query a per-case collection that has no composite index

**Linked Issue**: issue #4738 (the fix was promised in issue #4689, issuecomment-6866583412)
**Status**: Verified

## Symptom

On master, the `gcp-ci` job of the `ci.yml` run 44041525543 (commit 2cd84b07c, 2026-11-17) fails three Firestore facts in `Paramore.Brighter.Gcp.Tests`. The job reports 118 passed and 3 failed:

- `WhenCountingOutstandingMessagesWithACeilingShouldStopAtTheCeiling`, the ceiling-of-three and ceiling-of-five facts.
- `WhenCountingOutstandingMessagesOlderThanTheAgeShouldCountOnlyStaleOnes`.

Each fails with `Grpc.Core.RpcException`, status `FailedPrecondition`, "The query requires an index", for a collection named `outbox_<32 hex>`, a different one per test. The index asked for is `IsDispatched` ascending, `TimeStamp` descending, `__name__`.

The facts came in with PR #4598 (merge d1d413b88, 2026-11-04). The fourth fact of the same suite, `WhenAnOutstandingCountIsCancelledShouldEndTheCall`, is not in the failure list. Three things kept this off the PR: `gcp-ci` skips fork PRs (`.github/workflows/ci.yml:839-842`), step 13 of the ADR ran the facts only against the emulator, and the emulator does not enforce indexes.

## Suspected Location

- The count query: `FirestoreOutbox.GetOutstandingMessageCountAsync`, `src/Paramore.Brighter.Outbox.Firestore/FirestoreOutbox.cs:1193-1286`. It reads `CollectionId = _outboxCollection.Name` (:1212), filters `IsDispatched == false` AND `TimeStamp < now - dispatchedSince`, orders by `TimeStamp` descending (:1246-1247) and wraps that in a count aggregation. This is the index shape the error asks for. `OutstandingMessagesAsync` (:965-1006) has the same shape.
- Before commit 18ed265f8 ("fix: make the Firestore overall count honour the age, the ceiling and the token", inside merge d1d413b88) the async count filtered only on the dispatched flag, which needs no composite index (`docs/adr/0085-outbox-stale-message-telemetry.md:919`).
- The collection choice: `tests/Paramore.Brighter.Gcp.Tests/Firestore/Configuration.cs:23-35` (`Name = collection ?? OutboxCollection`, :31; `OutboxCollection = "outbox"`, :65) and `tests/Paramore.Brighter.Gcp.Tests/Outbox/Firestore/FirestoreOutboxProvider.cs`, where `_outstandingCountCollection = $"outbox_{Uuid.New():N}"` (:18) feeds `DeleteOutstandingCountStore` (:104), `CreateOutstandingCountStore` (:119) and `PrepareCancellationAsync` (:124).
- The generated facts call the provider from `tools/Paramore.Brighter.Test.Generator/Templates/Outbox/OutstandingCount/*.cs.liquid` (the ceiling template at :34 and :37, the age template at :27 and :30, the cancellation template at :19), with the contract in `IAmAnOutstandingCountOutboxProvider.cs.liquid:16-33`. Generated output is under `tests/Paramore.Brighter.Gcp.Tests/Outbox/Firestore/Generated/OutstandingCount/`.
- Why a separate store exists: ADR 0085-outbox-stale-message-telemetry (about :648 and :978) says the count suite counts every undispatched message, so the store must hold only the case's messages, and that the Firestore provider shares one collection. The provider change is commit 84627086a.

## Root-Cause Hypothesis

Firestore composite indexes belong to a collection ID, and the count query is scoped to one collection (`FirestoreOutbox.cs:1212`). A collection named `outbox_<uuid>` that is created for each provider instance cannot have the (`IsDispatched` ascending, `TimeStamp` descending) index in advance, so real Firestore rejects `GetOutstandingMessageCountAsync` with `FailedPrecondition` before it counts anything. The emulator does not enforce indexes, which is why the emulator run passed.

The hypothesis predicts that the fixed `outbox` collection already has the index. The generated Deleting facts call `OutstandingMessages(Async)` on `outbox` with the same query shape (for example `Generated/Async/When_Deleting_Multiple_Messages_They_Should_Be_Removed_From_Outbox_Async.cs:57-59`), and they are not among the three failures.

It is false if the Deleting facts also fail in that run, if the project has an index that should match and does not, or if the failing collection IDs are not the provider's `outbox_<uuid>` names.

**UNVERIFIED, to be proven or refuted in /bugfix:confirm:** moving these facts to the existing fixed `outbox` collection. Doing so has a cost: the age fact asserts exactly 5 and the ceiling facts assert exactly the ceiling (`tests/Paramore.Brighter.Base.Test/Outbox/OutstandingCountOutboxBaseTests.cs:94`, :110, :126), so any undispatched document left in `outbox` by another test or an earlier run changes the count. `DeleteOutstandingCountStore` (:107-110) deletes every document it reads, which on `outbox` would delete other tests' documents. The generated suites for `outbox` share `[Collection("FirestoreOutbox")]` (`test-configuration.json:52-57`) and run one after another, but `ci.yml` has no `concurrency:` key, so separate gcp-ci runs could overlap.

## Confirmed Root Cause

`FirestoreOutboxProvider` gives each provider instance, and so each fact, a collection named `outbox_{Uuid.New():N}` (`FirestoreOutboxProvider.cs:18`), and `CreateOutstandingCountStore` (:119) points the count at it. `GetOutstandingMessageCountAsync` runs a count aggregation over one collection (`FirestoreOutbox.cs:1212`) with `IsDispatched == false`, `TimeStamp < now - A` and an order on `TimeStamp` descending (:1221-1248). An equality on one field with a range and order on another needs a composite index on that collection ID, and an index cannot exist in advance for a name made at run time. Real Firestore rejects the query with `FailedPrecondition`.

The query shape came with commit 18ed265f8; the per-case collections came with commit 84627086a. Both reached master in merge d1d413b88. `specs/0038-outbox-stale-message-telemetry/tasks.md:1761` reasoned that the count uses the same filter and order as `OutstandingMessagesAsync`, which gcp-ci already ran on Google Cloud. That is true of the shape, but the index belongs to the collection ID, and those runs used `outbox`.

## Evidence

- [x] Code trace.
  - `OutstandingMessagesAsync` sends the same structured query: the same collection selector (`FirestoreOutbox.cs:967`), the same filters (:979-990) and the same order on `TimeStamp` descending (:1000-1001). Only offset and limit differ, and they do not change the index. The sync `OutstandingMessages` delegates to it (:326-330).
  - The generated Deleting facts run that query on `outbox` (`Generated/Async/When_Deleting_Multiple_Messages_They_Should_Be_Removed_From_Outbox_Async.cs:57-58`, `Generated/Sync/When_Deleting_One_Message_It_Should_Be_Removed_From_Outbox.cs:73`) and assert on the rows returned. In run 44041525543 they passed (118 passed, 3 failed). So `outbox` has an index that serves this shape, and only the per-case collections lack one.
  - A count aggregation needs the index of the query it wraps. The failing call is the aggregation, and the index it asks for has the same fields.
  - The hand-written timeout tests also use `outbox_{Uuid}` collections, but they hold the replies and assert `DeadlineExceeded` (`When_the_async_outstanding_count_is_held_should_give_up_after_the_default_timeout.cs:60-71`), so they pass with or without the index. Their warm-up `GetAsync(Id)` and the provider cleanup `Get(pageSize: 1_000)` (`FirestoreOutbox.cs:348-361`) need no composite index.
  - The cancellation fact sets `storeHeld: false` and accepts any `RpcException` (`FirestoreOutboxProvider.cs:127-128`, `OutstandingCountCancellationOutboxBaseTests.cs:42-43`, :66-69), so it cannot detect this bug.
- [ ] Red repro on real Firestore: not run. It needs the GCP project's credentials, and the emulator does not check indexes. The CI log of run 44041525543 stands in for it.

## Suggested-Fix Assessment

PARTIAL. A fixed collection ID is the only test-side way to have an index, so the direction in issuecomment-6866583412 is right. Moving the facts onto `outbox` itself is wrong:
- The count has no narrowing filter (`FirestoreOutbox.cs:1214-1241`), so other suites' leftovers, crashed runs and overlapping gcp-ci runs (no `concurrency:` key in `ci.yml`) all add to the age fact's exact 5 (`OutstandingCountOutboxBaseTests.cs:91-94`).
- `DeleteOutstandingCountStore` deletes every document it reads (`FirestoreOutboxProvider.cs:107-110`), which on `outbox` would delete other tests' documents.

A fixed collection used only by the count suites works within a run, because the generated count classes share `[Collection("FirestoreOutbox")]` (`test-configuration.json:52-57`) and run one at a time. It needs one composite index created once in the GCP project (database `brighter-firestore-database`, collection scope, `IsDispatched` ascending, `TimeStamp` descending). No file in the repository defines Firestore indexes, so whoever owns the project has to create it. Until then gcp-ci stays red for these facts.

Keeping per-case isolation with a fixed leaf ID under a per-case parent would need `FirestoreOutbox` to accept a parent other than `{DatabasePath}/documents` (`FirestoreOutbox.cs:1025`, :1094, :1264). That is a production change and is out of scope here.

## Scope Notes

- The provider should empty the fixed collection after each case, looping until empty, and clear what an earlier run left before the first case, so a crashed run's documents do not carry over.
- Across runs the collection is still shared. A `concurrency:` group on the gcp-ci job would serialise CI runs. That is a separate change to propose, not part of this fix.
- The text that says each case gets a store of its own must follow the fix: ADR 0085-outbox-stale-message-telemetry about :648 and :978, the provider contract in `IAmAnOutstandingCountOutboxProvider.cs.liquid:28`, `OutstandingCountOutboxBaseTests.cs:19` and `OutstandingCountCancellationOutboxBaseTests.cs:22`. The reasoning in `specs/0038-outbox-stale-message-telemetry/tasks.md:1761` and `requirements.md:229` is incomplete, and no text there is changed after approval; the correction goes in the commit message and the PR.
- Issue #4689: the topic-counting suite uses the same `CreateOutstandingCountStore` (`TopicCountingOutboxTests.cs.liquid:50`, :53), so it inherits the fixed collection, and the per-topic query will need its own index on that collection. The topic counting PR has to name the fields.
- A deployment that uses the Firestore outbox with the stale-message monitor needs the same index on its own outbox collection. A deployment that runs the sweeper already needs it. No document says so.
- The regression check for this bug is the existing age and ceiling facts on real Firestore, which only gcp-ci on master runs. The emulator cannot go red on the missing index.

Open points from triage:
- The cancellation fact passes whether or not the index exists: it accepts any `RpcException` (`OutstandingCountCancellationOutboxBaseTests.cs:42-43`, :66-69). It cannot tell a missing index from a cancellation, so it is not a check for this bug.
- The four hand-written timeout tests (`Outbox/Firestore/When_the_*outstanding_count*.cs`) build their own `outbox_{Uuid}` collection, but their warm-up read needs no composite index, so they are not affected.
- No file in the repository defines a Firestore index (no indexes JSON, no Terraform, no gcloud step in `ci.yml:835-875`). The index on `outbox` was presumably created by hand in the GCP project and cannot be checked from here.
- No TTL policy on `outbox` could be confirmed.

## Regression Test

`tests/Paramore.Brighter.Gcp.Tests/Outbox/Firestore/When_a_second_provider_counts_outstanding_messages_should_see_the_first_providers_messages.cs` (`OutstandingCountSharedCollectionTests`, `[Collection("FirestoreOutbox")]`).

Two `FirestoreOutboxProvider` instances: a message ten minutes old is added through the first one's `CreateOutstandingCountStore()`, and the second one's count store counts messages older than one minute. The test expects 1. On the emulator it fails today with `Expected: 1, Actual: 0`, because each provider points at its own `outbox_<uuid>` collection. The emulator does not check indexes, so the test pins that every provider instance counts the same collection. Only `gcp-ci` can prove the index. The four generated count facts and the two held-reply timeout tests pass on the emulator.

## Fix

The Firestore count suites now use one fixed collection, `outbox_outstanding_count`, which a composite index can exist for.

- `tests/Paramore.Brighter.Gcp.Tests/Firestore/Configuration.cs`: `CreateOutstandingCountOutbox()` over the fixed collection.
- `tests/Paramore.Brighter.Gcp.Tests/Outbox/Firestore/FirestoreOutboxProvider.cs`: the per-instance `outbox_{Uuid}` name is gone. `DeleteOutstandingCountStore` deletes in a loop until the collection is empty. `CreateOutstandingCountStore` clears what an earlier run left, once per test run. Clearing on every call would delete what another provider instance had just added.
- Contract text: "a store of its own that no other test writes to" became "a store that holds only the case's messages" in `IAmAnOutstandingCountOutboxProvider.cs.liquid`, `OutstandingCountOutboxBaseTests.cs`, `OutstandingCountCancellationOutboxBaseTests.cs`, `.agent_instructions/generated_tests.md` and the contract rows of ADR 0085-outbox-stale-message-telemetry. The 14 generated copies were regenerated, and only that comment line changed.
- ADR 0085-outbox-stale-message-telemetry also: the AC-16 store paragraph and the consequence line now describe the fixed collection, the index it needs, and the shared-run limit.

On the emulator the regression test, the four generated count facts and the two held-reply timeout tests pass (7 of 7), and so do the 47 Firestore tests outside the inbox. The inbox tests do not use the emulator, because `Configuration.CreateInbox` has no emulator hook, as on master. The index itself still has to be created in the Google Cloud project (database `brighter-firestore-database`, collection `outbox_outstanding_count`, `IsDispatched` ascending, `TimeStamp` descending).
