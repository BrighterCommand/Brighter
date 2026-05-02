# Tasks: 0027 — Span-Based Performance

**ADR**: [0057-span-based-performance](../../docs/adr/0057-span-based-performance.md)
**Requirements**: [requirements.md](requirements.md)
**Issue**: #4112

## Phase 1: Core — MessageBody Refactor (FR-1, FR-2, FR-3)

> Dependencies: None. This is the foundation — all subsequent tasks depend on it.
> Regression check: After each TEST+IMPLEMENT task in this phase, run `dotnet test tests/Paramore.Brighter.Core.Tests/` to confirm no regressions.

- [x] **TIDY: Add System.Memory package reference to netstandard2.0 projects**
  - Add `<PackageReference Include="System.Memory" />` to `Directory.Packages.props` and to `Paramore.Brighter.csproj` conditionally for `netstandard2.0`
  - Verify the solution still builds for all target frameworks
  - No behavioral change — structural only

- [x] **TEST + IMPLEMENT: MessageBody constructed from ReadOnlyMemory\<byte\> does not copy**
  - **USE COMMAND**: `/test-first when constructing MessageBody from ReadOnlyMemory should not allocate a new byte array`
  - Test location: `tests/Paramore.Brighter.Core.Tests/MessageBodyTests` (new directory — create it)
  - Test file: `When_Constructing_MessageBody_From_ReadOnlyMemory_Should_Not_Copy.cs`
  - Test should verify:
    - Constructing `MessageBody(new ReadOnlyMemory<byte>(bytes))` stores the memory without copying
    - The new `Memory` property returns the same `ReadOnlyMemory<byte>` that was passed in
    - The `Bytes` property still returns a valid `byte[]` (backward compat — may copy)
    - The `Value` property still returns the correct string
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - Change `MessageBody` internal storage from `byte[] Bytes` field to `ReadOnlyMemory<byte> _memory` field
    - Add `public ReadOnlyMemory<byte> Memory => _memory;` property
    - Change `Bytes` to a property that returns `_memory.ToArray()`
    - Update the `ReadOnlyMemory<byte>` constructor to store memory directly (remove `.ToArray()` at line 165)
    - Update `byte[]` and `string` constructors to wrap their data in `ReadOnlyMemory<byte>`

- [x] **TEST + IMPLEMENT: MessageBody.Value caches its string representation**
  - **USE COMMAND**: `/test-first when accessing MessageBody Value multiple times should return cached string`
  - Test location: `tests/Paramore.Brighter.Core.Tests/MessageBodyTests`
  - Test file: `When_Accessing_MessageBody_Value_Multiple_Times_Should_Cache.cs`
  - Test should verify:
    - First access to `Value` returns the correct string for UTF8, ASCII, and Base64 encodings
    - Subsequent accesses return the same string reference (`Object.ReferenceEquals`)
    - Cached value is correct after construction from both `string` and `byte[]`
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - Add `private string? _cachedValue` field to `MessageBody`
    - In `Value` getter, check `Volatile.Read(ref _cachedValue)` first; if non-null, return it
    - Otherwise compute, store via `Volatile.Write(ref _cachedValue, result)`, and return

- [x] **TEST + IMPLEMENT: MessageBody.Equals compares without allocating byte arrays**
  - **USE COMMAND**: `/test-first when comparing two MessageBody instances for equality should not allocate byte arrays`
  - Test location: `tests/Paramore.Brighter.Core.Tests/MessageBodyTests`
  - Test file: `When_Comparing_MessageBody_Equality_Should_Use_Span.cs`
  - Test should verify:
    - Two `MessageBody` instances with identical bytes are equal
    - Two `MessageBody` instances with different bytes are not equal
    - Null comparisons work correctly
    - `==` and `!=` operators work correctly
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - Change `Equals(MessageBody?)` to use `_memory.Span.SequenceEqual(other._memory.Span)`
    - Update `GetHashCode()` to hash from `_memory.Span`

## Phase 2: Core — JSON Message Mappers (FR-4, FR-5)

> Dependencies: Phase 1 (MessageBody refactor must be complete).
> Regression check: After each task, run `dotnet test tests/Paramore.Brighter.Core.Tests/` to confirm no regressions.

- [x] **TEST + IMPLEMENT: JsonMessageMapper serialises directly to UTF-8 bytes**
  - **USE COMMAND**: `/test-first when mapping a request to message should serialize directly to UTF8 bytes without intermediate string`
  - Test location: `tests/Paramore.Brighter.Core.Tests/JsonMapper`
  - Test file: `When_Mapping_Request_To_Message_Should_Serialize_To_Utf8_Bytes.cs`
  - Test should verify:
    - `MapToMessage` produces a valid `Message` with correct body content
    - The body bytes are valid UTF-8 JSON that deserialises back to the original request
    - Body `CharacterEncoding` is `UTF8`
    - Round-trip: `MapToMessage` → `MapToRequest` returns equivalent object
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - In `JsonMessageMapper.MapToMessage()`: replace `JsonSerializer.Serialize()` with `JsonSerializer.SerializeToUtf8Bytes()` and construct `MessageBody(byte[])` directly
    - In `JsonMessageMapper.MapToRequest()`: replace `message.Body.Value` with `JsonSerializer.Deserialize<T>(message.Body.Memory.Span)`

- [x] **TEST + IMPLEMENT: CloudEventJsonMessageMapper serialises directly to UTF-8 bytes**
  - **USE COMMAND**: `/test-first when mapping a cloud event request to message should serialize directly to UTF8 bytes`
  - Test location: `tests/Paramore.Brighter.Core.Tests/CloudEvents`
  - Test file: `When_Mapping_CloudEvent_To_Message_Should_Serialize_To_Utf8_Bytes.cs`
  - Test should verify:
    - `MapToMessage` produces a valid cloud event `Message` with correct body and headers
    - Round-trip: `MapToMessage` → `MapToRequest` returns equivalent object
    - Cloud event envelope fields (source, type, subject, dataSchema) are preserved
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - Apply the same `SerializeToUtf8Bytes` / `Deserialize<T>(ReadOnlySpan<byte>)` pattern as `JsonMessageMapper`

## Phase 3: Core — CompressPayloadTransformer (FR-6)

> Dependencies: Phase 1 (MessageBody `Memory` property must exist).
> Regression check: After each task, run `dotnet test tests/Paramore.Brighter.Core.Tests/` to confirm no regressions.

- [x] **TEST + IMPLEMENT: ReadOnlyMemoryStream adapter wraps ReadOnlyMemory\<byte\> as a readable stream**
  - **USE COMMAND**: `/test-first when reading from ReadOnlyMemoryStream should return data from the underlying ReadOnlyMemory`
  - Test location: `tests/Paramore.Brighter.Core.Tests/MessageBodyTests` (new directory — create if not yet done)
  - Test file: `When_Reading_From_ReadOnlyMemoryStream_Should_Return_Memory_Data.cs`
  - Test should verify:
    - `Read()` returns the correct bytes from the underlying `ReadOnlyMemory<byte>`
    - `Length` matches the memory length
    - `Position` advances correctly after reads
    - Seeking resets the position and subsequent reads return correct data
    - `CanRead` is true, `CanWrite` is false
    - `Write` throws `NotSupportedException`
    - Reading past the end returns 0 bytes
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - Create `src/Paramore.Brighter/ReadOnlyMemoryStream.cs` — a `Stream` subclass wrapping `ReadOnlyMemory<byte>`
    - Override `Read`, `ReadAsync`, `Length`, `Position`, `CanRead` (true), `CanSeek` (true), `CanWrite` (false)
    - Throw `NotSupportedException` from `Write`/`SetLength`

- [x] **TEST + IMPLEMENT: CompressPayloadTransformer uses Memory property instead of Bytes**
  - **USE COMMAND**: `/test-first when compressing a message should use Memory property to avoid byte array copies`
  - Test location: `tests/Paramore.Brighter.Core.Tests/Compression`
  - Test file: `When_Compressing_A_Message_Should_Use_Memory_Property.cs`
  - Test should verify:
    - Compression of a large payload produces correct compressed output (same as before)
    - Decompression recovers the original payload
    - Round-trip: compress → decompress returns original body
    - `IsCompressed` correctly identifies compressed messages
    - Both sync (`Wrap`/`Unwrap`) and async (`WrapAsync`/`UnwrapAsync`) paths work
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - Replace all `message.Body.Bytes` accesses with `message.Body.Memory`
    - Use `ReadOnlyMemoryStream` for input instead of `new MemoryStream(bytes)`
    - Use `MemoryStream.TryGetBuffer(out ArraySegment<byte>)` to get the output buffer without `.ToArray()` copy (aligns with ADR section 3 — avoids the final allocation)
    - In `IsCompressed`, use `message.Body.Memory.Span` for lead-byte checks

## Phase 4: Core — ClaimCheck and CharacterEncoding (FR-7, FR-8)

> Dependencies: Phase 1 (MessageBody `Memory` property), Phase 3 (ReadOnlyMemoryStream adapter).
> Regression check: After each task, run `dotnet test tests/Paramore.Brighter.Core.Tests/` to confirm no regressions.

- [x] **TEST + IMPLEMENT: ClaimCheckTransformer stores and retrieves without StreamWriter/StreamReader**
  - **USE COMMAND**: `/test-first when wrapping a large message for claim check should store body without StreamWriter allocation`
  - Test location: `tests/Paramore.Brighter.Core.Tests/Claims`
  - Test file: `When_Wrapping_A_Large_Message_Should_Store_Without_StreamWriter.cs`
  - Test should verify:
    - A large message body is stored to the storage provider correctly
    - Retrieval returns the original body content
    - Round-trip: wrap → unwrap returns original message body
    - Both sync and async paths work
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - In `ClaimCheckTransformer`, use `ReadOnlyMemoryStream` over `body.Memory` instead of `MemoryStream` + `StreamWriter`
    - On retrieve, read the stream into a `byte[]` and construct `MessageBody(byte[])` instead of `StreamReader.ReadToEnd()` → `MessageBody(string)`
    - In `InMemoryStorageProvider`, store `ReadOnlyMemory<byte>` internally instead of `string`

- [x] **TEST + IMPLEMENT: CharacterEncoding lookup uses case-insensitive comparison without allocation**
  - **USE COMMAND**: `/test-first when converting string to CharacterEncoding should not allocate via ToLowerInvariant`
  - Test location: `tests/Paramore.Brighter.Core.Tests/MessageBodyTests`
  - Test file: `When_Converting_String_To_CharacterEncoding_Should_Be_Case_Insensitive.cs`
  - Test should verify:
    - `"utf-8"`, `"UTF-8"`, `"Utf-8"` all return `CharacterEncoding.UTF8`
    - `"us-ascii"`, `"US-ASCII"` all return `CharacterEncoding.ASCII`
    - `"base64"`, `"BASE64"` all return `CharacterEncoding.Base64`
    - Unknown strings return `CharacterEncoding.Raw`
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - Replace `name.ToLowerInvariant()` switch with `string.Equals(name, "utf-8", StringComparison.OrdinalIgnoreCase)` chains in `CharacterEncodingExtensions.ToCharacterEncoding()`

## Phase 5: Transport — RMQ (FR-9)

> Dependencies: Phase 1 (MessageBody `ReadOnlyMemory<byte>` constructor no longer copies).
> Note: There are two `RmqMessageCreator` implementations — one in `RMQ.Async` and one in `RMQ.Sync`. Both must be updated.

- [x] **TEST + IMPLEMENT: RMQ Async message body flows through from AMQP without byte array copy** (already working via Phase 1 — fromQueue.Body is ReadOnlyMemory<byte> passed directly to MessageBody)
  - **USE COMMAND**: `/test-first when creating a Brighter message from an AMQP delivery should pass ReadOnlyMemory body without copying`
  - Test location: `tests/Paramore.Brighter.RMQ.Async.Tests`
  - Test file: `When_Creating_Message_From_AMQP_Should_Pass_Memory_Without_Copy.cs`
  - Test should verify:
    - `RmqMessageCreator.CreateMessage()` produces a `Message` with body content matching the AMQP delivery body
    - The message body is constructed from `ReadOnlyMemory<byte>` (the AMQP client's native type)
    - Body content is accessible via both `Memory` and `Bytes` properties
    - Headers are correctly decoded from the AMQP delivery
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - Verify that `RmqMessageCreator` (Async) already passes `fromQueue.Body` (a `ReadOnlyMemory<byte>`) to `MessageBody` constructor — the Phase 1 change eliminates the copy automatically
    - If any intermediate `.ToArray()` calls exist in the RMQ Async path, remove them
    - On `net8.0`+, replace `Encoding.UTF8.GetString(byte[])` header calls with `Encoding.UTF8.GetString(ReadOnlySpan<byte>)` where the value is parsed immediately (timestamps, integers), using `#if` conditional compilation. On `netstandard2.0`, retain `GetString(byte[])`

- [x] **TEST + IMPLEMENT: RMQ Sync message body flows through from AMQP without byte array copy** (already working via Phase 1 — same as Async)
  - **USE COMMAND**: `/test-first when creating a Brighter message from an AMQP delivery in sync mode should pass ReadOnlyMemory body without copying`
  - Test location: `tests/Paramore.Brighter.RMQ.Sync.Tests`
  - Test file: `When_Creating_Message_From_AMQP_Should_Pass_Memory_Without_Copy.cs`
  - Test should verify:
    - Same verifications as the Async variant above, applied to the Sync `RmqMessageCreator`
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - Apply the same changes as the Async variant to `src/Paramore.Brighter.MessagingGateway.RMQ.Sync/RmqMessageCreator.cs`

## Phase 6: Transport — Kafka (FR-9)

> Dependencies: Phase 1 (MessageBody refactor).

- [x] **TEST + IMPLEMENT: Kafka header encoding uses UTF-8 consistently for produce and consume**
  - **USE COMMAND**: `/test-first when producing and consuming Kafka headers should use UTF8 encoding consistently`
  - Test location: `tests/Paramore.Brighter.Kafka.Tests`
  - Test file: `When_Producing_And_Consuming_Headers_Should_Use_Utf8_Consistently.cs`
  - Test should verify:
    - Headers produced with UTF-8 encoding can be consumed correctly
    - Non-ASCII characters (e.g. Unicode in bag values) round-trip correctly
    - Standard Brighter headers (message type, topic, timestamp, etc.) round-trip correctly
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - Change `StringTools.ToByteArray()` from `Encoding.ASCII.GetBytes()` to `Encoding.UTF8.GetBytes()`
    - Change `StringTools.FromByteArray()` from `Encoding.ASCII.GetString()` to `Encoding.UTF8.GetString()`
    - Cache the `byte[]` representations of fixed header name constants in `KafkaDefaultMessageHeaderBuilder` via static readonly fields

## Phase 7: Transport — Azure Service Bus (FR-9)

> Dependencies: Phase 1 (MessageBody refactor).

- [x] **TEST + IMPLEMENT: Azure Service Bus message body uses BinaryData.ToMemory instead of ToArray**
  - **USE COMMAND**: `/test-first when creating a Brighter message from Azure Service Bus should use ToMemory for zero-copy body`
  - Test location: `tests/Paramore.Brighter.AzureServiceBus.Tests`
  - Test file: `When_Creating_Message_From_ServiceBus_Should_Use_Memory.cs`
  - Test should verify:
    - Message body content is correctly read from a `ServiceBusReceivedMessage`
    - The body is accessible via both `Memory` and `Value` properties
    - The `IBrokeredMessageWrapper.MessageBodyValue` property still returns `byte[]` (backward compat)
    - The new `MessageBodyMemory` property returns `ReadOnlyMemory<byte>`
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - Add `ReadOnlyMemory<byte> MessageBodyMemory { get; }` to `IBrokeredMessageWrapper` with default implementation `=> (MessageBodyValue ?? Array.Empty<byte>()).AsMemory()`
    - Override in `BrokeredMessageWrapper` to return `_brokeredMessage.Body.ToMemory()`
    - Update test doubles (`FakeServiceBusReceiverWrapper`, `BrokeredMessage`) to implement new property

- [x] **TEST + IMPLEMENT: Azure Service Bus message creator uses UTF-8 encoding instead of Encoding.Default**
  - **USE COMMAND**: `/test-first when creating a Brighter message from Azure Service Bus should use UTF8 not Encoding.Default`
  - Test location: `tests/Paramore.Brighter.AzureServiceBus.Tests`
  - Test file: `When_Creating_Message_Should_Use_Utf8_Encoding.cs`
  - Test should verify:
    - A message body containing non-ASCII UTF-8 characters (e.g. emoji, accented characters) round-trips correctly
    - The decoded body string matches the original
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - In `AzureServiceBusMesssageCreator`, replace `System.Text.Encoding.Default.GetString()` with `Encoding.UTF8.GetString()` or preferably use `MessageBodyMemory` and construct `MessageBody(ReadOnlyMemory<byte>)` directly

## Phase 8: Benchmarks (NFR-3)

> Dependencies: All previous phases.

- [ ] **SETUP: Create BenchmarkDotNet project for allocation measurement**
  - Create a new console project `benchmarks/Paramore.Brighter.Benchmarks/` (BenchmarkDotNet requires a console app, not a test project)
  - Add `BenchmarkDotNet` package reference to `Directory.Packages.props`
  - Create `MessageRoundTripBenchmark.cs` with `[MemoryDiagnoser]`
  - Benchmark the full round-trip: `JsonMessageMapper.MapToMessage` → `CompressPayloadTransformer.Wrap` → `Unwrap` → `MapToRequest`
  - Verification: `dotnet run -c Release` in the benchmark project produces BenchmarkDotNet output with allocation metrics
  - The benchmark results serve as documentation of the allocation reduction achieved (AC-6)

## Phase 9: Final Regression Verification

> Dependencies: All previous phases.
> Note: Each phase runs per-task regression checks. This is the final comprehensive sweep including transport tests.

- [ ] **VERIFY: All existing tests pass across core and transports**
  - Run the full core test suite: `dotnet test tests/Paramore.Brighter.Core.Tests/`
  - Run RMQ Async tests: `dotnet test tests/Paramore.Brighter.RMQ.Async.Tests/`
  - Run RMQ Sync tests: `dotnet test tests/Paramore.Brighter.RMQ.Sync.Tests/`
  - Run Kafka tests: `dotnet test tests/Paramore.Brighter.Kafka.Tests/`
  - Run Azure Service Bus tests: `dotnet test tests/Paramore.Brighter.AzureServiceBus.Tests/`
  - This satisfies AC-5 and NFR-2
