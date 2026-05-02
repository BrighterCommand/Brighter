# Resume Point: Span<T> Performance Implementation

## Current State

**Spec**: `specs/0027-span-based-performance`
**Branch**: `use_spans`
**Issue**: #4112
**ADR**: `docs/adr/0057-span-based-performance.md` (Accepted)

## Completed Tasks

### Phase 1: Core — MessageBody Refactor (FR-1, FR-2, FR-3) ✅ COMPLETE
- [x] TIDY: Add System.Memory package reference (commit `8b4a86856`)
- [x] TEST+IMPLEMENT: MessageBody from ReadOnlyMemory does not copy (commit `df7350057`)
- [x] TEST+IMPLEMENT: MessageBody.Value caches string representation (commit `ae3e3f0c8`)
- [x] TEST+IMPLEMENT: MessageBody.Equals compares without allocating byte arrays (test locks in existing span-based behavior)

## Next Action

Start **Phase 2: Core — JSON Message Mappers (FR-4, FR-5)**.

Run `/test-first when mapping a request to message should serialize directly to UTF8 bytes without intermediate string`

Then continue with the remaining Phase 2 task and onward per `specs/0027-span-based-performance/tasks.md`.

## Key Implementation Details

- `MessageBody` internal storage is now `ReadOnlyMemory<byte> _memory`
- `Memory` property: zero-copy access
- `Bytes` property: backward-compat, returns `_memory.ToArray()` (copies)
- `Value` property: cached via `Volatile.Read`/`Volatile.Write` on `_cachedValue`
- `Equals`: uses `_memory.Span.SequenceEqual()`
- `GetHashCode`: hashes from `_memory.Span`
- `#if NETSTANDARD2_0` guards needed for `Encoding.GetString(ReadOnlySpan<byte>)` and `Convert.ToBase64String(ReadOnlySpan<byte>)` which are not available on netstandard2.0
