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

Core regression: 720/720 tests pass on both net9.0 and net10.0.

### Phase 8: Benchmarks (NFR-3) ✅ COMPLETE

Benchmark results (Apple M1 Pro, .NET 10.0.0, BenchmarkDotNet v0.14.0):

| Method | PayloadSize | Mean | Allocated |
|---|---|---|---|
| MapToMessage | 1,000 | 567 ns | 2.67 KB |
| MapToMessage_ThenCompress | 1,000 | 6,049 ns | 4.03 KB |
| FullRoundTrip | 1,000 | 8,056 ns | 8.38 KB |
| MapToMessage | 10,000 | 1,928 ns | 11.46 KB |
| MapToMessage_ThenCompress | 10,000 | 10,117 ns | 12.82 KB |
| FullRoundTrip | 10,000 | 17,347 ns | 43.54 KB |
| MapToMessage | 100,000 | 25,413 ns | 99.38 KB |
| MapToMessage_ThenCompress | 100,000 | 67,286 ns | 100.74 KB |
| FullRoundTrip | 100,000 | 132,871 ns | 395.26 KB |

### Phase 9: Final Regression Verification ✅ COMPLETE

| Suite | net10.0 | net9.0 | Notes |
|---|---|---|---|
| Core Tests | 720/720 pass | 719/720 pass | 1 flaky DispatcherRestart race (pre-existing) |
| ASB Tests | 104/104 pass | 104/104 pass | 10 infra tests fail (no Azure credentials) |
| RMQ Sync | 26/26 pass | — | 24 fail: mTLS + delayed-message plugin not installed |
| RMQ Async | — | 44/44 pass | 44 fail: same mTLS + plugin gaps |
| Kafka | 34/34 pass | 37/37 pass | 28-31 fail: consumer-group coordination flakiness |

All failures are pre-existing infrastructure/config gaps (mTLS certs, RMQ delayed-message plugin, Kafka consumer-group races). No regressions from Span<T> changes.

## All Phases Complete

The Span<T> performance implementation is complete. Ready for PR to master.

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
- `#if NETSTANDARD2_0` guards needed for `Encoding.GetString(ReadOnlySpan<byte>)`, `Convert.ToBase64String(ReadOnlySpan<byte>)`, and `BitConverter.ToUInt16(ReadOnlySpan<byte>)` which are not available on netstandard2.0
