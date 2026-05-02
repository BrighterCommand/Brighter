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

## Next Action: Phase 8 — Benchmarks (NFR-3)

Create a BenchmarkDotNet project to measure the allocation reduction achieved by the Span<T> changes.

### What to do

1. Create `benchmarks/Paramore.Brighter.Benchmarks/` as a console app (BenchmarkDotNet requires this)
2. Add `BenchmarkDotNet` package reference to `Directory.Packages.props`
3. Create `MessageRoundTripBenchmark.cs` with `[MemoryDiagnoser]`
4. Benchmark the full round-trip pipeline:
   - `JsonMessageMapper.MapToMessage` (serialize to UTF-8 bytes)
   - `CompressPayloadTransformer.Wrap` (compress using Memory)
   - `CompressPayloadTransformer.Unwrap` (decompress)
   - `JsonMessageMapper.MapToRequest` (deserialize from Memory.Span)
5. Run with `dotnet run -c Release` to produce allocation metrics
6. The benchmark results document the allocation reduction (AC-6)

### After Phase 8

Phase 9: Final regression verification across transport test suites (requires running RabbitMQ, Kafka, Azure Service Bus infrastructure).

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
