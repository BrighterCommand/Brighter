# Tasks: AsyncAPI 3.0 Document Generation

**Spec**: 0004-asyncapi-document-generation
**Design**: [ADR 0040](../../docs/adr/0040-asyncapi-document-generation.md)
**PRD**: [tasks/prd-asyncapi-document-generation.md](../../tasks/prd-asyncapi-document-generation.md)
**Branch**: `feature/3828-AsyncAPI-Generation`

## Overview

Add a new `Paramore.Brighter.AsyncAPI` NuGet package that generates AsyncAPI 3.0 JSON documents from
Brighter's runtime configuration (subscriptions, publications, assembly-scanned IRequest types).

### Scope Summary

| Change | Details |
|--------|---------|
| New source project | `src/Paramore.Brighter.AsyncAPI/` |
| New test project | `tests/Paramore.Brighter.AsyncAPI.Tests/` |
| New sample project | `samples/AsyncAPI/RMQAsyncAPI/` |
| Solution file | Add 3 projects to `Brighter.slnx` |
| ADR | `docs/adr/0040-asyncapi-document-generation.md` |

---

## Phase 1: Project Setup

### Task 1.1: Create project files and add to solution
- Create `src/Paramore.Brighter.AsyncAPI/Paramore.Brighter.AsyncAPI.csproj` targeting `$(BrighterCoreTargetFrameworks)`
- Add project references to `Paramore.Brighter` and `Paramore.Brighter.Extensions.DependencyInjection`
- Add package references to `NJsonSchema`, `Microsoft.Extensions.Hosting.Abstractions`, `System.Text.Json` (no versions)
- Create `tests/Paramore.Brighter.AsyncAPI.Tests/Paramore.Brighter.AsyncAPI.Tests.csproj` with xUnit and FakeItEasy
- Add both projects to `Brighter.slnx`
- Verify: `dotnet build Brighter.slnx` succeeds

---

## Phase 2: Document Model and Configuration

### Task 2.1: Create AsyncAPI 3.0 document model POCOs
- Create `Model/AsyncApiDocument.cs` — root document with `AsyncApi`, `Info`, `Channels`, `Operations`, `Components`
- Create `Model/AsyncApiInfo.cs` — `Title`, `Version`, `Description?`
- Create `Model/AsyncApiChannel.cs` — `Address`, `Messages` (dict of refs)
- Create `Model/AsyncApiOperation.cs` — `Action`, `Channel` (ref), `Messages` (list of refs)
- Create `Model/AsyncApiComponents.cs` — `Messages` (dict of message definitions)
- Create `Model/AsyncApiMessage.cs` — `Name`, `ContentType`, `Payload` (JsonElement?), `Description?`
- Create `Model/AsyncApiRef.cs` — `Ref` (serialised as `$ref`)
- All POCOs use `System.Text.Json` `[JsonPropertyName]` and `JsonIgnoreCondition.WhenWritingNull`
- Write unit tests verifying JSON serialization shape

### Task 2.2: Create AsyncApiOptions configuration class
- Create `AsyncApiOptions.cs` with `Title`, `Version`, `Description?`, `AssembliesToScan`, `DisableAssemblyScanning`
- Write unit test verifying defaults

---

## Phase 3: Schema Generation

### Task 3.1: Create schema generator interface and NJsonSchema implementation
- Create `IAmASchemaGenerator.cs` with `Task<JsonElement?> GenerateAsync(Type?, CancellationToken)`
- Create `NJsonSchemaGenerator.cs` implementing `IAmASchemaGenerator` using `NJsonSchema.JsonSchema.FromType()`
- Null type returns empty object schema `{}`
- Exceptions return empty object schema (graceful degradation)
- Write unit tests for null type, valid type, and exception paths

---

## Phase 4: Document Generator

### Task 4.1: Create generator with subscription (receive) support
- Create `IAmAnAsyncApiDocumentGenerator.cs` with `Task<AsyncApiDocument> GenerateAsync(CancellationToken)`
- Create `AsyncApiDocumentGenerator.cs` taking `AsyncApiOptions`, `IAmASchemaGenerator`, `IAmConsumerOptions?`, `IAmAProducerRegistry?`
- Iterate `IAmConsumerOptions.Subscriptions` to produce channels and `receive` operations
- Handle null RequestType with placeholder messages
- Skip empty/null RoutingKey (use `RoutingKey.IsNullOrEmpty`)
- Handle null `IAmConsumerOptions` gracefully
- Sanitize channel IDs and operation IDs
- Create test doubles: `FakeConsumerOptions`, `TestRequests`
- Write unit tests

### Task 4.2: Add publication (send) support to generator
- Iterate `IAmAProducerRegistry.Producers` to produce channels and `send` operations
- Skip empty/null `Publication.Topic`
- Handle null `IAmAProducerRegistry` gracefully
- Create test doubles: `FakeProducerRegistry`, `FakeMessageProducer`
- Write unit tests

### Task 4.3: Add assembly scanning for PublicationTopic-decorated IRequest types
- Scan assemblies for concrete non-abstract `IRequest` types with `[PublicationTopic]`
- Use `attr.Destination.RoutingKey.Value` as channel address
- DI sources win deduplication
- Handle `DisableAssemblyScanning` option
- Catch `ReflectionTypeLoadException`
- Write unit tests

### Task 4.4: Add deduplication and channel merging
- Same routing key from subscription + publication = one channel, two operations
- Same IRequest type across sources = one message component
- Write unit tests for all dedup scenarios

---

## Phase 5: DI and Host Integration

### Task 5.1: Create DI registration via UseAsyncApi()
- Create `AsyncApiBrighterBuilderExtensions.cs` with `IBrighterBuilder.UseAsyncApi(Action<AsyncApiOptions>?)`
- Register `AsyncApiOptions`, `IAmAnAsyncApiDocumentGenerator`, `IAmASchemaGenerator` as singletons
- Resolve `IAmConsumerOptions` and `IAmAProducerRegistry` via `GetService` (nullable)
- Write unit tests verifying DI registrations

### Task 5.2: Create file generation via IHost extension
- Create `AsyncApiHostExtensions.cs` with `IHost.GenerateAsyncApiDocumentAsync(string, CancellationToken)` returning `Task<AsyncApiDocument>`
- Serialize with `WriteIndented = true`, `WhenWritingNull` ignore condition
- Create parent directories
- Throw `InvalidOperationException` if `UseAsyncApi()` not called
- Synchronous overload
- Write unit tests

---

## Phase 6: Sample Project

### Task 6.1: Create RabbitMQ sample project
- Create `samples/AsyncAPI/RMQAsyncAPI/` with RabbitMQ transport
- Configure at least one subscription and one publication
- Add `--generate-asyncapi` argument check calling `GenerateAsyncApiDocumentAsync`
- Add to `Brighter.slnx`
- Verify `dotnet build` succeeds

---

## Phase 7: Post-Review Fixes (P0 - Must Fix)

### Task 7.1: Fix stateful singleton bug in AsyncApiDocumentGenerator
- **Bug**: `AsyncApiDocumentGenerator` accumulates state in `_channels`, `_operations`, `_messages` dictionaries across calls. Registered as singleton, second call to `GenerateAsync()` produces corrupt document with duplicate `_2` suffixed operations.
- **Fix**: Clear all three dictionaries at the start of `GenerateAsync()` before processing
- `/test-first when generating async api document twice the second call produces identical output`
- Verify existing tests still pass

### Task 7.2: Fix incomplete assembly scanning deduplication
- **Bug**: Assembly scanning dedup checks `_operations.ContainsKey($"send_{channelId}")` which misses DI publications that were assigned unique-ified IDs (e.g. `send_order_created_2`).
- **Fix**: Track deduplicated channel+action pairs separately (e.g. a `HashSet<(string channelId, string action)>`) rather than relying on operation ID string matching
- `/test-first when assembly scanning skips types already registered via DI with non-default operation ids`
- Verify existing deduplication tests still pass

### Task 7.3: Remove dead `#else` branch in AsyncApiHostExtensions
- **Issue**: `#if NET8_0_OR_GREATER` / `#else` in `AsyncApiHostExtensions.cs` — the else branch is unreachable since TFMs are `net8.0;net9.0;net10.0`
- Remove the conditional compilation and keep only the async `File.WriteAllTextAsync` path

---

## Phase 8: Post-Review Fixes (P1 - Should Fix)

### Task 8.1: Defensive copy of Servers dictionary
- **Issue**: `AsyncApiDocumentGenerator.GenerateAsync()` assigns `_options.Servers` directly to the document, sharing the mutable reference
- **Fix**: Shallow-copy the dictionary when assigning to the document: `Servers = _options.Servers != null ? new Dictionary<string, AsyncApiServer>(_options.Servers) : null`

### Task 8.2: Compile/cache channel ID sanitization regex
- **Issue**: `Regex.Replace(value, "[^a-zA-Z0-9]", "_")` creates regex overhead per call
- **Fix**: Use a `private static readonly Regex` with `RegexOptions.Compiled`, or `[GeneratedRegex]` source generator for net8.0+

### Task 8.3: Document IAmConsumerOptions coupling
- **Issue**: AsyncAPI generation resolves `IAmConsumerOptions` for subscriptions, but this interface is the consumer configuration contract (channel factory, inbox, etc.), tightly coupling to Service Activator registration
- **Fix**: Add XML doc comment on `UseAsyncApi()` explaining that subscriptions are sourced from `IAmConsumerOptions` which requires `AddConsumers()` registration, and that send-only apps without consumers will correctly produce documents with no receive operations
