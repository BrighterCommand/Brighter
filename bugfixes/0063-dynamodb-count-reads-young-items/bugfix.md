# Bugfix: DynamoDB outstanding count reads every outstanding item, not only stale ones

**Linked Issue**: #4684
**Status**: Confirmed

## Symptom

On DynamoDB and DynamoDB V4, `GetOutstandingMessageCountAsync(dispatchedSince, …, maxCount)` costs read capacity and round trips in proportion to **all** outstanding (undispatched) messages, not just the ones older than `dispatchedSince`. When few messages are stale and many young ones are waiting:

- Each `ScanConcurrency` segment keeps paging, because it never reaches `maxCount` matches. It reads its whole share of the `OutstandingAllTopics` index. Each page is capped at `maxCount - segmentCount` items read, so the number of pages is roughly (outstanding items in the segment) / `maxCount`. With a ceiling of 1 that is one request per outstanding item.
- `Select.COUNT` returns no body, but a scan is billed by the size of every item it reads. The index projects ALL attributes by default, body included, so capacity grows with the total body size of the young backlog.
- The whole count runs under `DynamoDbConfiguration.Timeout`, 500 ms by default, through a linked cancellation token. With a large young backlog the count can time out and keep failing for as long as the backlog lasts. Measured on DynamoDB Local, see Evidence.

The synchronous `GetOutstandingMessageCount` blocks on the async method, so it has the same problem. The count is called on a timer by `TimedOutboxStaleMessageMonitor` with `CountCeiling` (default 100), so the cost repeats on every tick.

On current master each segment gets the **whole** `maxCount` as its `Limit`, not a share of it as the issue text says. The share was the behaviour before PR #4598.

## Suspected Location

- `src/Paramore.Brighter.Outbox.DynamoDB/DynamoDbOutbox.cs`
  - `GetOutstandingMessageCount` (:838-848): sync, blocks on async
  - `GetOutstandingMessageCountAsync` (:851-889): timeout source at :858-860, one task per segment at :874-877, `Math.Min(sum, maxCount)` at :881
  - `ScanOutstandingIndexSegmentForCount` (:891-928): `ScanRequest` on `OutstandingAllTopicsIndexName` (:908), `Limit = maxCount - segmentCount` (:910), `Select = Select.COUNT` (:914), `FilterExpression = "OutstandingCreatedTime <= :olderThan"` (:919), loop condition :925
- `src/Paramore.Brighter.Outbox.DynamoDB.V4/DynamoDbOutbox.cs`: the same code at :850-860, :863-901 and :903-940
- Index key schema, `MessageItem.cs`, the same in both packages:
  - :70-73: `OutstandingCreatedTime` is the range key of `Outstanding` and the hash key of `OutstandingAllTopics`
  - :130: `MessageId` is the range key of `OutstandingAllTopics` and `DeliveredAllTopics`
  - :183-185: `TopicShard` is the hash key of `Outstanding` and `Delivered`
- Sparse index: `MarkDispatched` removes `OutstandingCreatedTime` (`DynamoDbOutbox.cs:539`), so only undispatched items are in either Outstanding index.
- Projection default: `src/Paramore.Brighter.DynamoDb/DynamoDbTableFactory.cs:86-109` (`AddGSIProjections`). Every GSI gets `ProjectionType.ALL` unless the caller passes a `DynamoGSIProjections`. V4 has the same in `src/Paramore.Brighter.DynamoDb.V4/DynamoDbTableFactory.cs`.
- Config: `DynamoDbConfiguration.cs` `Timeout`, `NumberOfShards`, `ScanConcurrency`; constructor defaults `timeout = 500, numberOfShards = 3, scanConcurrency = 3`.
- Caller: `src/Paramore.Brighter.Outbox.Hosting/TimedOutboxStaleMessageMonitor.cs:140-146` (overall count with `countCeiling`).

## Root-Cause Hypothesis

The count scans the `OutstandingAllTopics` GSI. That index cannot take a range condition on age, because `OutstandingCreatedTime` is its **partition** key (`MessageItem.cs:71`). So the age is applied as a `FilterExpression`, which DynamoDB evaluates **after** reading, and `Limit` caps items evaluated, not items matched. Three falsifiable parts:

1. **Filter after read.** Summed over all responses of one call, `ScannedCount` equals the number of outstanding items in the index whenever the stale total is below `maxCount`, while `Count` is only the stale ones. Falsified if the summed `ScannedCount` stays near the stale count while young items grow.
2. **`Limit = maxCount - segmentCount` makes round trips scale with outstanding items.** When a segment matches fewer than `maxCount` items, it pages until `LastEvaluatedKey` is empty, reading at most `maxCount - segmentCount` items per page. With `maxCount = 1` and no stale item, that is about one call per outstanding item. Falsified if the call count stays flat as young items grow.
3. **Default GSI projection ALL makes each read pay for body size.** Each item read is billed at its full size, body included, even with `Select.COUNT`. Falsified if the consumed capacity of the count does not change when only body size changes. DynamoDB Local may not report a real `ConsumedCapacity`; if so, this part rests on `DescribeTable` showing the projection plus the documented billing model.

Consequence to confirm: with a large young backlog, the count runs past the 500 ms linked timeout and throws `OperationCanceledException` on every monitor tick.

Fix directions from the issue, all **UNVERIFIED, to be proven or refuted in /bugfix:confirm**: a new GSI with the time as its sort key (keys only); a query per topic shard on `Outstanding` (needs the list of topics, which the outbox does not have); dropping `Limit` from the count scan (fewer round trips, same read capacity).

## Confirmed Root Cause
The overall count scans `OutstandingAllTopics`, whose **partition** key is `OutstandingCreatedTime`
(`MessageItem.cs:71`). No index in the table has the age as a sort key without also needing the
topic in the partition key (`Outstanding` is `TopicShard` + `OutstandingCreatedTime`), so the age can
only be applied as a `FilterExpression` (`DynamoDbOutbox.cs:919`). DynamoDB applies that filter after
reading. `Limit` (`:910`) caps items read per page, not items matched. When the stale total is below
`maxCount`, each segment pages through its whole share of the index (`:925`). So one call reads every
outstanding message, young ones included. With the default projection ALL
(`DynamoDbTableFactory.cs:86-109`), each item read is billed at its full size, body included, even with
`Select.COUNT`. The whole call runs under the 500 ms default `Timeout` (`:858-860`), so with a large
young backlog it can throw `OperationCanceledException` on every monitor tick. V4 is the same code
(`Paramore.Brighter.Outbox.DynamoDB.V4/DynamoDbOutbox.cs:863-940`).

## Evidence
- [x] Code-trace: as above, confirmed by an independent read-only trace of V3 and V4, with no
  refutation.
- [x] Measurement on DynamoDB Local (docker `amazon/dynamodb-local`, master 3201ee559). A
  `DelegatingHandler` counted the Scan requests and summed `ScannedCount` and `Count` from each
  response. Setup: 5 stale messages (10 min old), N young ones (1 min old), 2 KB bodies, 4 topics,
  `ScanConcurrency` 3, `dispatchedSince` 5 min. The last column is the same call with the default
  500 ms timeout:

  | young | maxCount | result | scan requests | Σ ScannedCount | Σ Count | ms (60 s timeout) | default 500 ms |
  |---:|---:|---:|---:|---:|---:|---:|---|
  | 0 | 100 | 5 | 3 | 5 | 5 | 100 | ok=5 |
  | 300 | 100 | 5 | 5 | 305 | 5 | 82 | ok=5 |
  | 3000 | 100 | 5 | 32 | 3005 | 5 | 18060 | CANCELLED |

  - Part 1 holds: the items read grow with young items (5 → 305 → 3005) while the matches stay at 5.
  - Part 2 holds: requests grow as about (items read) / `maxCount` per segment (3 → 5 → 32).
  - The consequence holds: at 3000 young the count is cancelled by the default timeout.
  - DynamoDB Local timings are not AWS latency. The request count and items read are what carry over.
  - The `maxCount = 1` rows are left out. DynamoDB Local seems to scan in key order, so the oldest
    (stale) items come first. Real DynamoDB orders by the hash of the partition key, so that row is not
    representative.
- [ ] Part 3 (capacity grows with body size) was not measured. DynamoDB Local does not report real
  consumed capacity. It rests on the projection default in `DynamoDbTableFactory.cs:86-109`, and on
  DynamoDB billing a Scan by the size of the items read before the filter. As a rough figure for the
  3000 row: 3005 items × 2 KB ≈ 6 MB per call, about 750 RCU with eventually consistent reads, on
  every monitor tick.
- The numbers come from a throwaway probe test outside the repository, since deleted.

## Suggested-Fix Assessment
- **New GSI with the time as sort key, keys only: PARTIAL.** It is the only option that stops reading
  young items. It is not one index, though:
  - It needs a new partition attribute. A constant partition would put every write on one GSI
    partition, so it has to be bucketed, and the count queries each bucket.
  - The attribute has to be written on Add and set again on replay when missing. `MarkDispatched`
    removes `OutstandingCreatedTime`, which drops the item from the index.
  - Existing tables need the index added by hand.
  - Existing items lack the new attribute, and the old stale items are exactly the ones the count must
    see. That means a backfill, or a runtime check (like `SupportsCausationTrackingAsync`, via
    DescribeTable) that falls back to today's scan.
  - The claim comment promised an ADR first if we take this route.
- **Query per topic shard on `Outstanding`: WRONG for the overall count.** The outbox has no list of
  topics. It fits the per-topic count, which is #4688.
- **Drop `Limit` from the count scan: PARTIAL, and it only fixes round trips.** Pages go up to 1 MB,
  so the 3000 row would drop from 32 requests to a handful. The items read and the capacity stay the
  same.

## Scope Notes
- The sweeper's outstanding read (`ScanOutstandingIndexSegmentForMessages`, `DynamoDbOutbox.cs:991-1039`)
  and the dispatched read (`ScanDispatchedIndexSegment`, `:1209-1263`) use the same scan, filter and
  `Limit` pattern. They are out of scope for #4684 and could be follow-ups.
- `GetShardNumber` can return a negative shard. That belongs to #4688 (per-topic count), not here.
- The boundary is `<=` here and `<` in the per-topic key condition. Keep `<=`: changing the
  boundary is a behaviour change outside this bug.
- Overlap with #4688 only if the fix becomes a per-shard query helper. The recommended fix does not.

## Regression Test
_(left blank, filled by /bugfix:test)_

## Fix
_(left blank, filled by /bugfix:fix)_
