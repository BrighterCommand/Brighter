# Universal DLQ — Cross-Spec Review Findings

**Date**: 2026-02-22
**Branch**: `universal_dlq`
**Specs Reviewed**: 0001, 0002, 0010, 0011, 0012, 0013, 0014, 0015
**Scope**: Design (ADRs), implementation (code), test coverage, cross-transport consistency

---

## Summary

Overall the implementation is **high quality and architecturally consistent** across all 8 specs. The Kafka reference pattern (spec 0001) has been faithfully replicated in every transport with appropriate transport-specific adaptations. No critical runtime bugs were found, but several issues warrant attention before merge.

| Severity | Count |
|----------|-------|
| High     | 2     |
| Medium   | 3     |
| Low      | 7     |

---

## High Severity

### H1. Redis: DLQ producers not disposed in `Dispose()`/`DisposeAsync()`

- [x] **Spec**: 0011 (Redis) — **FIXED**
- **File**: `src/Paramore.Brighter.MessagingGateway.Redis/RedisMessageConsumer.cs`
- **Lines**: ~53-54 (Dispose), ~129-140 (DisposeAsync)
- **Issue**: `Dispose()` and `DisposeAsync()` only call `DisposePool()` but never dispose the lazy-created `_deadLetterProducer` and `_invalidMessageProducer`. If a rejection occurs, the producer is created but never cleaned up.
- **Impact**: Leaks Redis connections held by `RedisManagerPool`. Will cause connection pool exhaustion in long-running applications.
- **Fix**: Add `IsValueCreated` checks and dispose producers, matching the Kafka pattern:
  ```csharp
  if (_deadLetterProducer?.IsValueCreated == true)
      (_deadLetterProducer.Value as IDisposable)?.Dispose();
  if (_invalidMessageProducer?.IsValueCreated == true)
      (_invalidMessageProducer.Value as IDisposable)?.Dispose();
  ```

### H2. MQTT: Missing `ClientID` in DLQ producer configuration

- [x] **Spec**: 0015 (MQTT) — **FIXED**
- **File**: `src/Paramore.Brighter.MessagingGateway.MQTT/MQTTMessageConsumer.cs`
- **Lines**: ~346-354 (`CreateDeadLetterProducer`), ~371-379 (`CreateInvalidMessageProducer`)
- **Issue**: When building `MqttMessagingGatewayProducerConfiguration` for DLQ/invalid producers, `ClientID` is not copied from `_configuration`. DLQ producers connect with a `null` ClientID.
- **Impact**: Broker may reject the connection or cause session conflicts when multiple clients connect without explicit ClientIDs.
- **Fix**: Add `ClientID = _configuration.ClientID` (or with a suffix like `"-dlq"` / `"-invalid"` to avoid MQTT session conflicts).

---

## Medium Severity

### M1. Backstop Error Handler: Missing logging per ADR 0037

- [x] **Spec**: 0002 (Backstop Error Handler) — **FIXED**
- **Files**:
  - `src/Paramore.Brighter/Reject/Handlers/RejectMessageOnErrorHandler.cs`
  - `src/Paramore.Brighter/Reject/Handlers/RejectMessageOnErrorHandlerAsync.cs`
- **Issue**: Neither handler logs the caught exception before throwing `RejectMessageAction`. ADR 0037 specifies `Log.UnhandledExceptionRejectingMessage(...)` using source-generated logging.
- **Impact**: Exception details may be lost if not captured elsewhere. Reduces observability of why messages were rejected.
- **Fix**: Add `ILogger` field and `[LoggerMessage]` partial methods following the `RequestLoggingHandler` pattern.

### M2. MQTT: `ResolveRejectionProducer` logs wrong message on producer creation failure

- [x] **Spec**: 0015 (MQTT) — **FIXED**
- **File**: `src/Paramore.Brighter.MessagingGateway.MQTT/MQTTMessageConsumer.cs`
- **Lines**: ~281-282
- **Issue**: If `DetermineRejectionRoute()` returns `hasProducer=true` but the `Lazy<T>` factory returned `null` (producer creation failed), the code logs "No channels configured for rejection" — which is misleading. The channels *are* configured; the producer just failed to initialize.
- **Fix**: Change to a distinct log message like "DLQ producer creation failed" or remove the redundant check (the factory methods already log the error).

### M3. RocketMQ: `throw` in ACK `finally` block can mask DLQ result

- [x] **Spec**: 0014 (RocketMQ) — **FIXED**
- **File**: `src/Paramore.Brighter.MessagingGateway.RMQ/RocketMessageConsumer.cs`
- **Lines**: ~156-166
- **Issue**: If `AckSourceMessageAsync()` fails in the `finally` block, the `throw` re-throws the ACK exception, potentially masking whether the DLQ send succeeded or failed.
- **Impact**: In the rare case where DLQ send succeeds but ACK fails, the exception propagates and the caller sees only the ACK failure. The message will reappear after the visibility timeout (acceptable per ADR), but logging is confusing.
- **Fix**: Consider catching the ACK exception without re-throwing (just log it), or only re-throw if the DLQ send also failed. This is documented as an accepted tradeoff in ADR 0042, so may be intentional — confirm design intent.

---

## Low Severity

### L1. Kafka: Fragile header removal list in `RefreshMetadata()`

- [ ] **Spec**: 0001 (Kafka)
- **File**: `src/Paramore.Brighter.MessagingGateway.Kafka/KafkaMessageConsumer.cs`
- **Lines**: ~944-956
- **Issue**: Removes 12+ specific header keys by name. If new Kafka headers are added to the transport, this list must be manually updated.
- **Suggestion**: Consider a predicate-based filter or explicit allowed-list instead of a deny-list.

### L2. No `HeaderNames` constants for DLQ metadata keys

- [ ] **Specs**: All (0001, 0010-0015)
- **Issue**: DLQ metadata keys (`originalTopic`, `rejectionReason`, `rejectionTimestamp`, `originalMessageType`, `rejectionMessage`) are hardcoded strings in each transport's `RefreshMetadata()`. No shared constants exist in `HeaderNames.cs`.
- **Impact**: If a key name needs to change, every transport must be updated independently.
- **Suggestion**: Add constants to `HeaderNames` or a new `DlqHeaderNames` class for shared DLQ metadata keys.

### L3. Inconsistent metadata key casing between Kafka and other transports

- [x] **Specs**: 0001 vs 0010-0015 — **INVESTIGATED: Leave as-is**
- **Issue**: Kafka uses PascalCase values (`OriginalTopic`) via `HeaderNames` constants, while other transports use camelCase (`originalTopic`) as inline strings.
- **Investigation**: Kafka uses `IKafkaMessageHeaderBuilder` which allows users to customize header naming. Changing Kafka's constants would be a breaking change for existing consumers. DLQ messages don't cross transport boundaries, so the inconsistency has no functional impact.
- **Decision**: Leave as-is. Each transport's DLQ metadata is self-consistent within its own ecosystem.

### L4. MsSql: Docker Compose image doesn't use `azure-sql-edge` for ARM

- [ ] **Spec**: 0012 (MsSql)
- **File**: `docker-compose-mssql.yaml`
- **Issue**: Uses `mcr.microsoft.com/mssql/server` (implicit `latest` tag) instead of `mcr.microsoft.com/azure-sql-edge:latest` as documented in ADR 0040 for ARM Mac developers.
- **Impact**: Tests may fail or be slow on ARM Macs running via Rosetta.
- **Suggestion**: Update docker-compose or add a note for ARM Mac developers.

### L5. No tests for producer creation failure scenarios

- [ ] **Specs**: 0011 (Redis), 0012 (MsSql), 0014 (RocketMQ)
- **Issue**: No transport tests verify behavior when `CreateDeadLetterProducer()` or `CreateInvalidMessageProducer()` throws during lazy initialization. The code handles this correctly (logs error, returns null, falls back to "no channels"), but it's not tested.
- **Suggestion**: Add a targeted test per transport for this edge case.

### L6. RocketMQ: Producer caching fields lack synchronization comment

- [x] **Spec**: 0014 (RocketMQ) — **FIXED**
- **File**: `src/Paramore.Brighter.MessagingGateway.RMQ/RocketMessageConsumer.cs`
- **Lines**: ~39-40
- **Issue**: `_deadLetterProducer` and `_invalidMessageProducer` are accessed without synchronization via null-coalescing assignment (`??=`). This is safe because message pumps are single-threaded per consumer, but no code comment explains this invariant.
- **Suggestion**: Add a comment referencing the single-threaded pump guarantee.

### L7. MQTT: `GetProducerRoutingKey()` uses null-forgiving operator without guard

- [x] **Spec**: 0015 (MQTT) — **FIXED**
- **File**: `src/Paramore.Brighter.MessagingGateway.MQTT/MQTTMessageConsumer.cs`
- **Lines**: ~287-292
- **Issue**: Uses `_invalidMessageRoutingKey!.Value` and `_deadLetterRoutingKey!.Value` with null-forgiving operators. These are only called after `ResolveRejectionProducer()` succeeds, so they're safe in practice, but the code could be more defensive.
- **Suggestion**: Return the routing key from `ResolveRejectionProducer()` to avoid separate derivation.

---

## Cross-Transport Consistency Notes (Informational)

These are not bugs but architectural observations for awareness:

### Sync/Async delegation strategy varies

| Transport | Strategy |
|-----------|----------|
| Kafka | Independent sync and async implementations |
| Redis | Sync wraps async via `BrighterAsyncContext.Run()` |
| MsSql | Independent implementations |
| PostgreSQL | Independent implementations |
| SQS | Independent implementations with async internals |
| RocketMQ | Sync delegates to async via `BrighterAsyncContext.Run()` |
| MQTT | Separate sync/async due to nested sync-over-async deadlock risk |

This variation is appropriate — each transport has different threading constraints.

### Source cleanup timing varies by transport model

| Transport | Cleanup Method | Pattern |
|-----------|---------------|---------|
| Kafka | Offset commit | After reject (stream is immutable) |
| Redis | None needed | Message already popped on receive |
| MsSql | Explicit DELETE in finally | After reject |
| PostgreSQL | Explicit DELETE in finally | After reject (visibility timeout) |
| SQS | DeleteMessage via receipt handle | In finally block |
| RocketMQ | Ack(MessageView) in finally | After reject |
| MQTT | None needed | Fire-and-forget model |

### What's working well

- **Routing logic** (`DetermineRejectionRoute`) is identical across all transports
- **Interface-based DLQ opt-in** (`IUseBrighterDeadLetterSupport` / `IUseBrighterInvalidMessageSupport`) is clean
- **Factory extraction pattern** (duck-typing via interface cast) is consistent
- **Lazy producer initialization** prevents overhead when DLQ not configured
- **Error handling philosophy** (always acknowledge, log failures, don't block consumer) is uniform
- **Test coverage** is comprehensive across all transports

---

## Checklist Summary

### Must Fix
- [x] H1 — Redis: Dispose DLQ producers *(fixed: added IsValueCreated checks in Dispose/DisposeAsync)*
- [x] H2 — MQTT: Copy ClientID to DLQ producer config *(fixed: added ClientID with `-dlq`/`-invalid` suffix)*

### Should Fix
- [x] M1 — Backstop: Add logging per ADR 0037 *(fixed: added source-generated logging in both sync/async handlers)*
- [x] M2 — MQTT: Fix misleading log message in ResolveRejectionProducer *(fixed: removed redundant null check)*
- [x] M3 — RocketMQ: Review throw-in-finally design *(fixed: replaced with AckSourceMessageSafeAsync that logs but doesn't re-throw)*

### Nice to Have
- [ ] L1 — Kafka: Refactor header removal to use predicate
- [ ] L2 — All: Add HeaderNames constants for DLQ metadata
- [x] L3 — All: Verify metadata key casing consistency *(investigated: Kafka uses PascalCase via IKafkaMessageHeaderBuilder; other transports use camelCase. Intentional — leave as-is to avoid breaking change)*
- [ ] L4 — MsSql: Update Docker image for ARM
- [ ] L5 — Redis/MsSql/RocketMQ: Add producer creation failure tests
- [x] L6 — RocketMQ: Add thread-safety comment *(fixed: added comment explaining single-threaded pump guarantee)*
- [x] L7 — MQTT: Improve null safety in GetProducerRoutingKey *(fixed: refactored ResolveRejectionProducer to return tuple, eliminated GetProducerRoutingKey)*
