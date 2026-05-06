# 57. Reduce Heap Allocations with Span\<T\> and Memory\<T\>

Date: 2026-05-01

## Status

Accepted

## Context

**Parent Requirement**: [specs/0027-span-based-performance/requirements.md](../../specs/0027-span-based-performance/requirements.md)

**Scope**: This ADR covers the adoption of `Span<T>`, `ReadOnlySpan<T>`, `Memory<T>`, and `ReadOnlyMemory<T>` across Brighter's core message pipeline and transport gateways to reduce heap allocations on hot paths.

### The problem

Brighter's message pipeline allocates heavily on every message processed:

1. **`MessageBody` (core)** — The `ReadOnlyMemory<byte>` constructor calls `.ToArray()` (line 165), discarding the zero-copy memory the transport already provided. The `Value` property re-computes `Encoding.GetString()` on every access with no caching.

2. **`JsonMessageMapper` (core)** — Serialises to `string` via `JsonSerializer.Serialize()`, then the `MessageBody(string)` constructor encodes that string to `byte[]`. On deserialisation, `message.Body.Value` converts `byte[]` back to `string` before `JsonSerializer.Deserialize<T>()` parses it. This is three allocations where one suffices.

3. **`CompressPayloadTransformer` (core)** — Creates two `MemoryStream` instances plus a compression stream per wrap/unwrap, then calls `.ToArray()` to produce the final `byte[]`.

4. **RMQ transport** — `RmqMessageCreator` calls `Encoding.UTF8.GetString(byte[])` ~15 times per consumed message, once per header field, each allocating a new string.

5. **Kafka transport** — `KafkaDefaultMessageHeaderBuilder` calls `Encoding.ASCII.GetBytes()` once per header field on produce (~20 fields). `KafkaMessageCreator` does the reverse on consume. There is also an encoding mismatch: produce uses ASCII, consume uses UTF-8 for bag entries.

6. **Azure Service Bus transport** — `BrokeredMessageWrapper.MessageBodyValue` calls `BinaryData.ToArray()` allocating a `byte[]`, then `AzureServiceBusMesssageCreator` calls `Encoding.Default.GetString()` (platform-dependent — a bug) to produce a string. Both are immediately discarded.

We previously avoided `Span<T>` because the project targets `netstandard2.0`. The [System.Memory](https://www.nuget.org/packages/System.Memory/) NuGet package provides a polyfill, removing this constraint. On `net8.0`+, `Span<T>` is a first-class runtime feature with optimal performance.

### Forces

- Every unnecessary allocation adds GC pressure in high-throughput messaging scenarios.
- `Span<T>` cannot be used in `async` methods, as class fields, or as generic type arguments on `netstandard2.0`. `Memory<T>` must be used in those contexts.
- The `System.Memory` polyfill on `netstandard2.0` is slower than native `Span<T>` on `net8.0`+, but still eliminates heap allocations.
- The public API surface (`IAmAMessageMapper<T>`, `MessageBody.Bytes`, `MessageBody.Value`) must remain backward-compatible.
- SQS/SNS and Redis transports are string-native on the wire and do not have byte-encoding hotspots.

## Decision

We will adopt `Span<T>` and `Memory<T>` to reduce allocations in three phases: core pipeline, then transports with byte-level hotspots, then benchmarking. The key design choices are:

### 1. `MessageBody` — store `ReadOnlyMemory<byte>` internally, expose `byte[]` via property (FR-1, FR-2, FR-3)

**Responsibilities** — `MessageBody` is an *information holder*: it *knows* the body content and encoding, and *decides* how to present the content as bytes or string.

Change the internal storage from `byte[] Bytes` to `ReadOnlyMemory<byte>`, and add a lazy-cached `string` for `Value`:

```
MessageBody
├── _memory : ReadOnlyMemory<byte>     (primary storage, no copy on construction)
├── _cachedValue : string?              (lazy, computed once on first Value access)
├── Bytes : byte[]                      (property — calls _memory.ToArray() for backward compat)
├── Memory : ReadOnlyMemory<byte>       (new property — zero-copy access)
├── Value : string                      (cached — computed once, not on every access)
└── ContentType, CharacterEncoding      (unchanged)
```

- The `ReadOnlyMemory<byte>` constructor no longer calls `.ToArray()`. The memory flows through from the transport without copying.
- A new `Memory` property exposes `ReadOnlyMemory<byte>` directly for callers that can work with it (transforms, mappers, transports).
- The existing `Bytes` property remains for backward compatibility but now calls `_memory.ToArray()` — callers that currently use `Bytes` work unchanged, but can migrate to `Memory` to avoid the copy.
- `Value` is computed once and cached in `_cachedValue`. Thread safety for the cache field is handled via `Volatile.Read`/`Volatile.Write` (the same lazy pattern used by `System.Lazy<T>` in publication-only mode — if two threads race, both compute the same value and one wins; no lock needed). Note: `MessageBody` is not generally thread-safe (e.g., `ContentType` has a public setter), so `Volatile` here is specifically to prevent torn reads on the cached string reference, not to provide full thread safety.
- `Equals` will be updated to compare `_memory.Span.SequenceEqual(other._memory.Span)` instead of `Bytes.SequenceEqual(other.Bytes)`, avoiding two `byte[]` allocations per equality check.

### 2. `JsonMessageMapper` — serialise to UTF-8 bytes directly (FR-4, FR-5)

**Responsibilities** — `JsonMessageMapper` is a *service provider*: it *does* serialisation/deserialisation.

```
Serialisation (MapToMessage):
  Before:  JsonSerializer.Serialize(request) → string → Encoding.UTF8.GetBytes(string) → byte[]
  After:   JsonSerializer.SerializeToUtf8Bytes(request) → byte[]

Deserialisation (MapToRequest):
  Before:  message.Body.Bytes → Encoding.UTF8.GetString() → string → JsonSerializer.Deserialize<T>(string)
  After:   message.Body.Memory.Span → JsonSerializer.Deserialize<T>(ReadOnlySpan<byte>)
```

`SerializeToUtf8Bytes()` is available in `System.Text.Json` on all supported targets. `JsonSerializer.Deserialize<T>(ReadOnlySpan<byte>)` was introduced in System.Text.Json 6.0 and is available on all targets including `netstandard2.0` when consuming the NuGet package (the project uses System.Text.Json 10.0.6). No conditional compilation is needed — both targets use the same `Deserialize<T>(ReadOnlySpan<byte>)` call directly.

The same pattern applies to `CloudEventJsonMessageMapper`.

### 3. `CompressPayloadTransformer` — use `ArrayPool<byte>` for output buffers (FR-6)

**Responsibilities** — `CompressPayloadTransformer` is a *service provider*: it *does* compression/decompression.

Replace `new MemoryStream()` for the output buffer with a pooled approach:

```
Before:  message.Body.Bytes → new MemoryStream(bytes) → compress → .ToArray() → new MessageBody(byte[])
After:   message.Body.Memory → ReadOnlyMemoryStream(memory) → compress into pooled MemoryStream → new MessageBody(ReadOnlyMemory<byte>)
```

On `net8.0`+, we can use `MemoryStream.GetBuffer()` with the known length to avoid the `.ToArray()` copy. On `netstandard2.0`, `MemoryStream.TryGetBuffer(out ArraySegment<byte>)` provides the same benefit.

The input `MemoryStream(bytes)` wrapping the source data can be replaced with a direct stream over `ReadOnlyMemory<byte>` using a thin `ReadOnlyMemoryStream` adapter (a `Stream` subclass wrapping `ReadOnlyMemory<byte>` — no copy, no allocation beyond the adapter object itself).

All accesses to `message.Body.Bytes` in `CompressPayloadTransformer` (length checks in `Wrap`/`WrapAsync`, input to `MemoryStream`, and compression-detection checks in `IsCompressed`) must migrate to `message.Body.Memory` to avoid the copy-on-access that the backward-compatible `Bytes` property now performs. For `IsCompressed`, use `message.Body.Memory.Span` to check lead bytes without allocation.

### 4. Transport-specific changes (FR-9)

#### RMQ — `RmqMessageCreator`

**Responsibilities** — `RmqMessageCreator` is an *interfacer*: it *does* translation between the AMQP wire format and Brighter's `Message`.

The ~15 `Encoding.UTF8.GetString(byte[])` calls per message on header fields can be reduced:

- For fields that are parsed immediately (timestamps, integers): use `Encoding.UTF8.GetString(ReadOnlySpan<byte>)` on `net8.0`+ to avoid the intermediate string allocation when the value is parsed directly to a typed value. On `netstandard2.0`, the existing `GetString(byte[])` is retained.
- For the message body: `fromQueue.Body` is already `ReadOnlyMemory<byte>` from the AMQP client. With the `MessageBody` change above, this now flows through without `.ToArray()`.

#### Kafka — `KafkaDefaultMessageHeaderBuilder` / `KafkaMessageCreator`

**Responsibilities** — These are *interfacers* translating between Confluent's header format and Brighter's `Message`.

- Replace per-header `StringTools.ToByteArray()` / `FromByteArray()` calls with a cached/pooled approach. Header names are a fixed set of constants — their `byte[]` representations can be computed once at static initialisation and reused.
- Fix the encoding mismatch: standardise on UTF-8 for both produce and consume.

#### Azure Service Bus — `BrokeredMessageWrapper` / `AzureServiceBusMesssageCreator`

**Responsibilities** — These are *interfacers* translating between the Azure SDK's `ServiceBusReceivedMessage` and Brighter's `Message`.

- `BrokeredMessageWrapper` will use `BinaryData.ToMemory()` internally for zero-copy access. `IBrokeredMessageWrapper` is a public interface (implemented by test doubles such as `FakeServiceBusReceiverWrapper` and `BrokeredMessage`), so we cannot change the existing `byte[]? MessageBodyValue` property without breaking implementors (NFR-1). Instead, add a new `ReadOnlyMemory<byte> MessageBodyMemory { get; }` property to the interface with a default implementation that calls `MessageBodyValue.AsMemory()`. `BrokeredMessageWrapper` overrides this to return `BinaryData.ToMemory()` directly. Internal callers (`AzureServiceBusMesssageCreator`) migrate to `MessageBodyMemory`, avoiding the `byte[]` allocation.
- Fix the `Encoding.Default` bug in `AzureServiceBusMesssageCreator`: use `Encoding.UTF8` explicitly.
- With `MessageBody` accepting `ReadOnlyMemory<byte>` without copying, the body flows through from the Azure SDK without allocation.

#### SQS/SNS and Redis

These transports are string-native on the wire. No byte-encoding changes needed. They may benefit from the `MessageBody.Value` caching, but no transport-level changes are required.

### 5. `ClaimCheckTransformer` / `InMemoryStorageProvider` — reduce StreamWriter/StreamReader allocations

**Responsibilities** — `ClaimCheckTransformer` is a *service provider* that *does* offloading of large message bodies to external storage. `InMemoryStorageProvider` is an *information holder* that *knows* stored claim-check payloads.

Both classes allocate `StreamWriter`/`StreamReader` instances on every store/retrieve operation to convert between `string` and `Stream`. With `MessageBody` now exposing `ReadOnlyMemory<byte>` via the `Memory` property, these conversions can be simplified:

- **Store path**: Instead of `new MemoryStream()` → `new StreamWriter()` → `writer.Write(body.Value)` → `writer.Flush()` → pass stream to storage, pass `body.Memory` directly as the payload. Storage providers that accept `Stream` can use the `ReadOnlyMemoryStream` adapter introduced for `CompressPayloadTransformer`.
- **Retrieve path**: Instead of `new StreamReader()` → `reader.ReadToEnd()` → `new MessageBody(string)`, read directly into a `byte[]` or `Memory<byte>` from the storage stream and construct `MessageBody(ReadOnlyMemory<byte>)`.
- **`InMemoryStorageProvider`**: Store `ReadOnlyMemory<byte>` in the internal `ConcurrentDictionary` instead of `string`, avoiding the string conversion entirely.

This addresses FR-7.

### 6. `CharacterEncodingExtensions` (FR-8)

The `ToCharacterEncoding(this string name)` method calls `name.ToLowerInvariant()` which allocates a new string. Replace with `string.Equals(name, "utf-8", StringComparison.OrdinalIgnoreCase)` chains on all targets — `StringComparison.OrdinalIgnoreCase` is available on both `netstandard2.0` and `net8.0`+.

FR-8 asks for `Span<T>`-based overloads. In practice, all callers of `ToCharacterEncoding` pass a `string` (typically from `ContentType.CharSet` or header values), and there are no call sites that have a `ReadOnlySpan<char>` without an owning string. Adding a `ReadOnlySpan<char>` overload would add API surface with no current callers. The `OrdinalIgnoreCase` change already eliminates the `ToLowerInvariant()` allocation, which is the actual performance win. If a `ReadOnlySpan<char>` call site appears in future transport work, a span overload can be added then.

### Architecture overview

```
                        ┌─────────────────────────────────────┐
                        │          Message Pipeline           │
                        └─────────────────────────────────────┘
                                         │
              ┌──────────────────────────┼──────────────────────────┐
              ▼                          ▼                          ▼
     ┌─────────────────┐     ┌────────────────────┐    ┌───────────────────┐
     │  MessageMapper   │     │    Transformer     │    │    Transport      │
     │ (Json/CloudEvent)│     │(Compress/ClaimCheck)│    │  (RMQ/Kafka/ASB)  │
     │  Mapper)         │     │                    │    │                   │
     └────────┬────────┘     └────────┬───────────┘    └────────┬──────────┘
              │                       │                         │
              ▼                       ▼                         ▼
     ┌──────────────────────────────────────────────────────────────────────┐
     │                         MessageBody                                 │
     │  _memory: ReadOnlyMemory<byte>  ←── zero-copy from transport        │
     │  _cachedValue: string?          ←── lazy, computed once             │
     │  Bytes: byte[]                  ←── compat (copies from _memory)    │
     │  Memory: ReadOnlyMemory<byte>   ←── new zero-copy accessor         │
     └──────────────────────────────────────────────────────────────────────┘
```

### Implementation approach

The work is ordered so that each phase builds on the previous:

1. **Add `System.Memory` package reference** to `netstandard2.0` projects that need it.
2. **`MessageBody` refactor** — internal `ReadOnlyMemory<byte>` storage, cached `Value`, new `Memory` property, span-based `Equals`.
3. **`JsonMessageMapper` / `CloudEventJsonMessageMapper`** — `SerializeToUtf8Bytes`, deserialise from bytes via `Utf8JsonReader`.
4. **`CompressPayloadTransformer`** — migrate to `Memory` property, pooled buffers, `ReadOnlyMemoryStream` adapter.
5. **`ClaimCheckTransformer` / `InMemoryStorageProvider`** — eliminate `StreamWriter`/`StreamReader`, store `ReadOnlyMemory<byte>`.
6. **`CharacterEncodingExtensions`** — case-insensitive comparison without `ToLowerInvariant()`.
7. **Transport: RMQ** — flow `ReadOnlyMemory<byte>` body through; span-based header decoding on `net8.0`+.
8. **Transport: Kafka** — cache header name byte arrays; fix encoding mismatch to UTF-8.
9. **Transport: Azure Service Bus** — `BinaryData.ToMemory()` via internal interface change; fix `Encoding.Default` bug.
10. **Benchmarks** — BenchmarkDotNet project measuring allocation counts on the full message round-trip.

## Consequences

### Positive

- Fewer heap allocations on every message processed — reduced GC pressure in high-throughput scenarios.
- `MessageBody.Value` caching eliminates redundant string conversions when accessed multiple times.
- Transport bodies flow through without copying when using the new `Memory` property.
- Fixes the Azure Service Bus `Encoding.Default` bug and the Kafka ASCII/UTF-8 encoding mismatch.
- No breaking changes to the public API surface — existing callers compile unchanged.

### Behavioral Changes (release notes)

- **`ClaimCheckTransformer` threshold semantics**: The threshold comparison changed from `Encoding.Unicode.GetByteCount(message.Body.Value)` (UTF-16 byte count of the decoded string) to `message.Body.Memory.Length` (actual stored byte count). The new behavior is more correct (compares like-for-like bytes), but callers who tuned `ThresholdInBytes` based on the old UTF-16 semantics may see different claim-check behavior. For ASCII content the UTF-16 count was ~2x the UTF-8 count, so bodies that previously exceeded the threshold may now fall below it. Adjust threshold values if needed.
- **Kafka `StringTools` encoding**: Changed from `Encoding.ASCII` to `Encoding.UTF8`. For ASCII-only header values (the common case) the wire format is identical. Non-ASCII characters in Kafka headers will now be encoded correctly as UTF-8 instead of being replaced with `?`. During a rolling deployment, ensure all producers and consumers are updated together if non-ASCII header values are in use.
- **`IBrokeredMessageWrapper.MessageBodyMemory`**: Added as a new abstract property without a default interface implementation. Default interface members are not supported on `netstandard2.0`, which is a target for the ASB package. External implementors of `IBrokeredMessageWrapper` will need to add the property when upgrading. This is an accepted semver-breaking change for this release.
- `netstandard2.0` support is maintained via the `System.Memory` polyfill.

### Design Trade-offs (accepted)

- **`TryGetBuffer` vs `ToArray` in transformers**: `MemoryStream.TryGetBuffer` returns an `ArraySegment` that correctly covers only the written bytes via `Offset` and `Count`. The underlying array may be over-allocated due to `MemoryStream`'s doubling growth policy, but the `ReadOnlyMemory<byte>` slice is precise. The trade-off is a potentially larger backing array kept alive vs an additional copy via `ToArray()`. For typical message sizes this is negligible; the zero-copy path is preferred.
- **`ReadOnlyMemoryStream` is `internal` with no direct tests**: This class is an implementation detail exercised indirectly through `CompressPayloadTransformer` and `ClaimCheckTransformer` tests. Per the project's testing policy, internal classes should not have direct unit tests — they are covered by the behavior that led to their creation. Exploratory tests were used during development and deleted once the class was made internal.

### Negative

- `MessageBody` becomes slightly more complex with dual storage (`_memory` + `_cachedValue`) and the backward-compatible `Bytes` property that copies.
- `MessageBody.Bytes` is now marked `[Obsolete]`. All internal callers (~30 across transports, outboxes, and transformers) have been migrated to use `Memory`, `Memory.ToArray()`, or `Memory.Span` as appropriate. Third-party consumers will receive a compile warning guiding them to use `Memory` instead. The property will be removed in a future major version.
- `#if NETSTANDARD2_0` conditional compilation increases in some files for the `Span<T>`-based overloads that are not available on `netstandard2.0`.
- The `System.Memory` polyfill adds a transitive dependency for `netstandard2.0` consumers (though most modern projects already depend on it).

### Risks and mitigations

| Risk | Mitigation |
|---|---|
| `MessageBody.Bytes` callers assume they own the array (mutate it) | `Bytes` property returns a copy via `.ToArray()`, so mutation is safe. Document that `Memory` returns a read-only view. |
| `_cachedValue` retains a large string in memory even when only bytes are needed | The string is eligible for GC when the `MessageBody` is collected. For streaming scenarios, callers should use `Memory` directly. |
| `System.Memory` polyfill performance on `netstandard2.0` | The polyfill avoids allocations even if span operations are slower. Benchmark both targets to validate. |
| Kafka encoding mismatch fix could break existing consumers | Standardise on UTF-8 for new messages; the consumer already handles UTF-8 for bag entries. Document the change in release notes. |

## Alternatives Considered

### 1. `System.IO.Pipelines` / `ReadOnlySequence<T>`

A more comprehensive approach using `PipeReader`/`PipeWriter` for streaming message bodies. Rejected because it would require significant public API changes (e.g., `IAmAMessageMapper<T>` would need to accept pipes) and is a much larger effort. Can be revisited in a future ADR.

### 2. `RecyclableMemoryStream` (Microsoft.IO.RecyclableMemoryStream)

Using `RecyclableMemoryStream` instead of `ArrayPool<byte>` for compression buffers. This is a viable alternative and may be adopted if benchmarks show it outperforms the `ArrayPool` approach. The two are not mutually exclusive.

### 3. Make `MessageBody` a `readonly struct`

Converting `MessageBody` from a `class` to a `readonly struct` to avoid the `MessageBody` object allocation itself. Rejected because `MessageBody` is used as a reference type throughout the codebase (nullable checks, equality, assignment to `Message.Body`) and the change would be a breaking API change.

### 4. Do nothing for transports

Focusing only on the core library and ignoring transports. Rejected because RMQ and Kafka have significant per-header allocations (~15–20 per message) that are straightforward to optimise, and the Azure Service Bus transport has an `Encoding.Default` bug that should be fixed regardless.

## References

- Requirements: [specs/0027-span-based-performance/requirements.md](../../specs/0027-span-based-performance/requirements.md)
- [Andrew Lock: Removing byte[] allocations using ReadOnlySpan\<T\>](https://andrewlock.net/removingbyte-array-allocations-in-dotnet-framework-using-readonlyspan-t/)
- [Microsoft: Memory and Span usage guidelines](https://learn.microsoft.com/en-us/dotnet/standard/memory-and-spans/)
- [System.Memory NuGet package](https://www.nuget.org/packages/System.Memory/)
- GitHub Issue: #4112
