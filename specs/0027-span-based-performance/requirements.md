# Requirements

> **Note**: This document captures user requirements and needs. Technical design decisions and implementation details should be documented in an Architecture Decision Record (ADR) in `docs/adr/`.

**Linked Issue**: #4112

## Problem Statement

As a developer using Brighter, I would like message transformation and translation to use fewer heap allocations, so that throughput is higher and GC pressure is lower in high-volume messaging scenarios.

Brighter's hot paths — message body construction, JSON serialization/deserialization, compression, and claim-check storage — repeatedly allocate `byte[]` arrays, intermediate `string` values, and `MemoryStream` instances when zero-copy or stack-based alternatives exist.

We previously avoided `Span<T>` because the project targets `netstandard2.0`. However, the [System.Memory](https://www.nuget.org/packages/System.Memory/) NuGet package provides a polyfill for `netstandard2.0`, removing this constraint entirely.

## Proposed Solution

Adopt `Span<T>`, `ReadOnlySpan<T>`, `Memory<T>`, and `ReadOnlyMemory<T>` across Brighter's message pipeline to eliminate unnecessary heap allocations. Callers interact with the same public API surface; the changes are internal optimisations and new overloads.

## Requirements

### Functional Requirements

- **FR-1**: Add `System.Memory` package reference to `netstandard2.0` target projects that do not already reference it.
- **FR-2**: `MessageBody` should avoid allocating a new `byte[]` when constructed from `ReadOnlyMemory<byte>` (eliminate the `.ToArray()` call).
- **FR-3**: `MessageBody.Value` should cache its string representation to avoid repeated `Encoding.GetString()` calls on every access.
- **FR-4**: JSON message mappers (`JsonMessageMapper`, `CloudEventJsonMessageMapper`) should serialise directly to UTF-8 bytes using `JsonSerializer.SerializeToUtf8Bytes()` instead of serialising to `string` and then encoding.
- **FR-5**: JSON deserialization in message mappers should operate on `ReadOnlySpan<byte>` or `byte[]` directly rather than converting to `string` first via `MessageBody.Value`.
- **FR-6**: `CompressPayloadTransformer` should reduce `MemoryStream` allocations by using pooled buffers or `RecyclableMemoryStream` where beneficial.
- **FR-7**: `ClaimCheckTransformer` and `InMemoryStorageProvider` should reduce `StreamWriter`/`StreamReader` allocations.
- **FR-8**: Character encoding extensions should eliminate the `ToLowerInvariant()` allocation in `ToCharacterEncoding()` by using case-insensitive comparison. `ReadOnlySpan<char>` overloads may be added later if call sites arise that have spans without owning strings.
- **FR-9**: Transport-specific message producers and consumers (RMQ, Kafka, SQS, Azure Service Bus, Redis, etc.) should be reviewed for unnecessary `byte[]`/`string` allocations in their serialization and deserialization paths and updated to use `Span<T>`/`Memory<T>` where beneficial.

### Non-functional Requirements

- **NFR-1**: No breaking changes to the public API. Existing callers must continue to work without modification.
- **NFR-2**: All existing tests must continue to pass.
- **NFR-3**: Measurable reduction in allocations on the message mapping and transformation paths (validated via benchmarks or allocation profiling).
- **NFR-4**: Changes must work on both `netstandard2.0` (via System.Memory polyfill) and `net8.0`+/`net9.0` targets.

### Constraints and Assumptions

- `Span<T>` cannot be used in `async` methods, fields, or as generic type arguments on `netstandard2.0`. Where `async` is involved, `Memory<T>` / `ReadOnlyMemory<T>` must be used instead.
- `stackalloc` should only be used for small, bounded buffers (≤256 bytes) to avoid stack overflow.
- The `System.Memory` polyfill on `netstandard2.0` is slower than native `Span<T>` on `net8.0`+, but still avoids heap allocations.
- Transport-specific code (RMQ, Kafka, SQS, Azure Service Bus, Redis, etc.) is in scope, though core library changes should land first as transports depend on them.

### Out of Scope

- `ReadOnlySequence<T>` or `System.IO.Pipelines` adoption (a larger, separate effort).
- Changes to the public `IAmAMessageMapper<T>` interface signature.
- Outbox/Inbox schema changes or migration of stored data formats.

## Acceptance Criteria

- **AC-1**: `MessageBody` constructed from `ReadOnlyMemory<byte>` does not call `.ToArray()`.
- **AC-2**: `JsonMessageMapper.MapToMessage()` does not produce an intermediate `string` between serialisation and `MessageBody` construction.
- **AC-3**: `JsonMessageMapper.MapToRequest()` deserialises from `byte[]`/`ReadOnlySpan<byte>` without calling `MessageBody.Value`.
- **AC-4**: `CompressPayloadTransformer` wrap/unwrap paths allocate fewer `MemoryStream` instances (validated by allocation count in tests or benchmarks).
- **AC-5**: All existing unit and integration tests pass without modification.
- **AC-6**: A benchmark (BenchmarkDotNet or similar) demonstrates measurable allocation reduction on the message round-trip path (map → transform → compress → decompress → untransform → unmap).

## Additional Context

- Reference article: [Removing byte[] allocations in .NET Framework using ReadOnlySpan<T>](https://andrewlock.net/removingbyte-array-allocations-in-dotnet-framework-using-readonlyspan-t/)
- Microsoft guidance: [Memory and Span usage guidelines](https://learn.microsoft.com/en-us/dotnet/standard/memory-and-spans/)
- System.Memory NuGet: https://www.nuget.org/packages/System.Memory/
