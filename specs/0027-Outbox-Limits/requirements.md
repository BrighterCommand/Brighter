# Requirements

> **Note**: This document captures user requirements and needs. Technical design decisions and implementation details should be documented in an Architecture Decision Record (ADR) in `docs/adr/`.

**Linked Issue**: #4115

## Problem Statement

As a developer using Brighter with the default `InMemoryOutbox`, I would like to configure the entry limit and compaction behaviour, so that messages are not silently compacted and lost before they can be dispatched.

The `InMemoryBox` base class (used by both `InMemoryInbox` and `InMemoryOutbox`) has a hard-coded default limit of 2048 entries. When this limit is reached, the oldest entries are compacted (removed). While the `EntryLimit` property is publicly settable, when `InMemoryOutbox` is created automatically by `ServiceCollectionExtensions.AddProducers` (because no explicit outbox was supplied), there is no way to configure it — the defaults are baked in.

At scale, 2048 entries can be exceeded before messages are dispatched, causing:
1. Messages silently compacted from the outbox before dispatch.
2. `OutboxProducerMediator.ClearOutbox` throws a `NullReferenceException` when it cannot find compacted messages — an inappropriate exception type with a misleading message.

## Proposed Solution

Allow users to configure the `InMemoryOutbox` defaults via `ProducersConfiguration` without requiring them to construct and register an `InMemoryOutbox` themselves. Additionally, make the compaction/expiry strategy smarter for outboxes vs inboxes, and improve error handling when messages are missing.

## Requirements

### Functional Requirements

1. **Configurable default outbox via ProducersConfiguration**: Add an `InMemoryBoxConfiguration` record to collate `EntryLimit`, `EntryTimeToLive`, `ExpirationScanInterval`, and `CompactionPercentage`. Expose it as `DefaultBoxConfiguration` on `ProducersConfiguration` so users can configure the auto-created `InMemoryOutbox`.
2. **Support unlimited entries (EntryLimit = -1)**: When `EntryLimit` is set to `-1`, compaction must not run. Expiry-based cleanup should still run.
3. **Make InMemoryBox abstract**: `InMemoryBox` should become an abstract class with `RemoveExpiredMessages` and `Compact` as abstract methods, forcing each subclass to define its own expiry and compaction strategies.
4. **Outbox-aware eviction**: `InMemoryOutbox` should override both `RemoveExpiredMessages` and `Compact` to only remove entries where `TimeFlushed` is not `DateTimeOffset.MinValue` (i.e. the message has been dispatched). Undispatched messages must never be removed by either expiry or compaction, regardless of age.
5. **Inbox retains existing behaviour**: `InMemoryInbox` should override `RemoveExpiredMessages` and `Compact` with the existing algorithms (remove entries older than `EntryTimeToLive` for expiry; remove oldest by `WriteTime` for compaction), unless `EntryLimit` is `-1`.
6. **Graceful handling of missing messages**: When `OutboxProducerMediator.ClearOutbox` cannot find a message in the outbox, it should log an error with actionable context rather than throwing a `NullReferenceException`.

### Non-functional Requirements

- **Backwards compatible**: Existing behaviour must be preserved when no new configuration is provided. The default `EntryLimit` remains 2048.
- **Thread safety**: All changes to compaction and expiry must remain thread-safe, consistent with the existing `Monitor`-based locking in `InMemoryBox`.
- **Performance**: Expiry scans in `InMemoryOutbox` should not introduce measurable overhead compared to the current implementation.

### Constraints and Assumptions

- The `InMemoryOutbox` is intended for development, testing, and low-to-moderate throughput production scenarios. Users with very high throughput should use a persistent outbox (SQL, DynamoDB, etc.).
- `InMemoryBox` is currently concrete with a non-virtual `RemoveExpiredMessages` — it will become abstract, with `RemoveExpiredMessages` as a `protected abstract` method. Both `InMemoryOutbox` and `InMemoryInbox` must provide implementations.
- The fix for `NullReferenceException` in `OutboxProducerMediator` is a separate, independent change that should not be gated on the configuration work.

### Out of Scope

- Configuring the `InMemoryInbox` via DI extensions (can be a follow-up).
- Adding persistent outbox implementations.
- Changing the compaction algorithm itself (e.g. LRU eviction).
- Metrics or health checks for outbox capacity.

## Acceptance Criteria

1. A user can set `DefaultBoxConfiguration` on `ProducersConfiguration` with an `InMemoryBoxConfiguration` record and have those values applied to the auto-created `InMemoryOutbox`.
2. Setting `EntryLimit = -1` disables compaction; expiry still removes dispatched messages.
3. `InMemoryOutbox` never removes undispatched messages during expiry scans.
4. `InMemoryInbox` behaviour is unchanged.
5. When `OutboxProducerMediator` cannot find a message, it logs an error (not throws `NullReferenceException`).
6. All existing tests continue to pass.
7. New tests cover: configurable limits via DI, `-1` entry limit, outbox-aware expiry, missing-message error handling.

## Additional Context

Relevant source files:
- `src/Paramore.Brighter/InMemoryBox.cs` — base class with `EntryLimit` (default 2048), compaction, and expiry logic
- `src/Paramore.Brighter/InMemoryOutbox.cs` — outbox implementation with `OutboxEntry.TimeFlushed`
- `src/Paramore.Brighter/InMemoryInbox.cs` — inbox implementation
- `src/Paramore.Brighter/ProducersConfiguration.cs` — configuration class that needs new properties
- `src/Paramore.Brighter.Extensions.DependencyInjection/ServiceCollectionExtensions.cs` (lines ~296, ~430) — where default `InMemoryOutbox` is created
- `src/Paramore.Brighter/OutboxProducerMediator.cs` (lines ~311-315, ~375-379) — where `NullReferenceException` is thrown for missing messages
