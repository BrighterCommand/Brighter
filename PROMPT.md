# Resume Point: Span<T> Performance Implementation

## Current State

**Spec**: `specs/0027-span-based-performance`
**Branch**: `use_spans`
**Issue**: #4112
**ADR**: `docs/adr/0057-span-based-performance.md` (Accepted)

## Completed Tasks

### Phase 1: Core — MessageBody Refactor (FR-1, FR-2, FR-3) ✅ COMPLETE
### Phase 2: Core — JSON Message Mappers (FR-4, FR-5) ✅ COMPLETE
### Phase 3: Core — CompressPayloadTransformer (FR-6) ✅ COMPLETE
### Phase 4: Core — ClaimCheck and CharacterEncoding (FR-7, FR-8) ✅ COMPLETE
### Phase 5: Transport — RMQ (FR-9) ✅ COMPLETE (already working via Phase 1)
### Phase 6: Transport — Kafka (FR-9) ✅ COMPLETE
### Phase 7: Transport — Azure Service Bus (FR-9) ✅ COMPLETE

## Remaining Tasks

### Phase 8: Benchmarks (NFR-3) — NOT STARTED
- [ ] SETUP: Create BenchmarkDotNet project for allocation measurement

### Phase 9: Final Regression Verification — PARTIALLY DONE
- [x] Core tests: 720 passed (net9.0 + net10.0)
- [ ] RMQ Async tests (requires running RabbitMQ)
- [ ] RMQ Sync tests (requires running RabbitMQ)
- [ ] Kafka tests (requires running Kafka)
- [ ] Azure Service Bus tests (requires running ASB)

## Next Action

Phase 8: Create BenchmarkDotNet project, or Phase 9: run integration tests if infrastructure available.

## Key Implementation Details

- `MessageBody` internal storage: `ReadOnlyMemory<byte> _memory`
- `Memory` property: zero-copy access
- `Bytes` property: backward-compat, returns `_memory.ToArray()` (copies)
- `Value` property: cached via `Volatile.Read`/`Volatile.Write` on `_cachedValue`
- `Equals`/`GetHashCode`: span-based
- `ReadOnlyMemoryStream`: zero-copy Stream adapter over ReadOnlyMemory<byte>
- `JsonMessageMapper`/`CloudEventJsonMessageMapper`: `SerializeToUtf8Bytes` + `Deserialize(Memory.Span)`
- `CompressPayloadTransformer`: `ReadOnlyMemoryStream` input, `TryGetBuffer` output, span lead-byte checks
- `ClaimCheckTransformer`: `ReadOnlyMemoryStream` wrap, byte reads unwrap
- `CharacterEncodingExtensions.ToCharacterEncoding`: `OrdinalIgnoreCase` (no allocation)
- Kafka `StringTools`: UTF-8 instead of ASCII
- ASB: `MessageBodyMemory` property + `Encoding.UTF8` instead of `Encoding.Default`
