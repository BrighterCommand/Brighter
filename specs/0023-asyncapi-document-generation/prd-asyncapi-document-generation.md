# PRD: AsyncAPI 3.0 Document Generation

## Introduction

Add a new `Paramore.Brighter.AsyncAPI` NuGet package that automatically generates an
[AsyncAPI 3.0](https://www.asyncapi.com/docs/reference/specification/v3.0.0) JSON document from a
Brighter service's runtime configuration.

The problem this solves: Brighter services expose async messaging contracts (topics they publish to,
topics they subscribe from, and the message types on each), but there is no machine-readable way to
document these contracts. Teams must maintain messaging documentation manually, which becomes stale.

The target user is a developer configuring a Brighter-based service who wants to generate accurate
API documentation as part of their CI/CD pipeline — without any extra manual work.

---

## Goals

- Allow any Brighter service to produce a valid AsyncAPI 3.0 JSON document by calling a single method
  after building the host.
- Collect messaging contracts from all three Brighter sources: subscriptions (consumer side),
  publications (producer side), and `[PublicationTopic]`-decorated `IRequest` types found by assembly
  scanning.
- Generate JSON Schema payloads for each `IRequest` type so consumers know the message shape.
- Make schema generation pluggable via an interface so advanced users can substitute their own
  schema generation logic.
- Provide a working sample project demonstrating end-to-end usage with a real transport.
- The generated document must pass validation against the AsyncAPI 3.0 specification.
- *(Added per review)* Support output in both JSON and YAML formats, since YAML is more
  human-readable and the two are readily convertible (see Amendment 2).
- *(Added per review)* Use the official [AsyncAPI .NET SDK](https://github.com/asyncapi/net-sdk)
  as the document model layer instead of custom POCOs, gaining built-in support for the full
  specification including bindings, tags, and protocol-specific metadata (see Amendment 1).
- *(Added per review)* Provide a Kafka sample in addition to the RabbitMQ sample, as Kafka is the
  major production use case (see Amendment 3).

---

## User Stories

### US-001: Project file and solution setup
**Description:** As a developer, I need the package to exist as a buildable project in the solution
so I can reference it from my services.

**Acceptance Criteria:**
- [ ] `src/Paramore.Brighter.AsyncAPI/Paramore.Brighter.AsyncAPI.csproj` exists and targets
  `net8.0;net9.0;net10.0` (`$(BrighterCoreTargetFrameworks)`)
- [ ] `tests/Paramore.Brighter.AsyncAPI.Tests/Paramore.Brighter.AsyncAPI.Tests.csproj` exists
- [ ] Both projects are added to `Brighter.slnx`
- [ ] `dotnet build Brighter.slnx` succeeds with no errors

---

### US-002: AsyncAPI 3.0 document model
**Description:** As a developer working on the library, I need a set of C# POCOs that represent the
AsyncAPI 3.0 document structure so the generator has a typed model to populate and serialize.

**Acceptance Criteria:**
- [ ] `AsyncApiDocument` POCO exists with properties: `AsyncApi` ("3.0.0"), `Info`, `Channels`,
  `Operations`, `Components`
- [ ] `AsyncApiInfo` POCO exists with properties: `Title`, `Version`, `Description?`
- [ ] `AsyncApiChannel` POCO exists with properties: `Address`, `Messages` (dict of message refs)
- [ ] `AsyncApiOperation` POCO exists with properties: `Action` ("send"/"receive"), `Channel` ($ref),
  `Messages` (list of $refs)
- [ ] `AsyncApiComponents` POCO exists with property: `Messages` (dict of message definitions)
- [ ] `AsyncApiMessage` POCO exists with properties: `Name`, `ContentType`
  ("application/json"), `Payload` (nullable `JsonElement`), `Description?`
- [ ] `AsyncApiRef` POCO exists with property: `Ref` (serialised as `"$ref"`)
- [ ] All POCOs serialise correctly to JSON using `System.Text.Json` with `JsonPropertyName`
  attributes and `JsonIgnoreCondition.WhenWritingNull`
- [ ] Unit tests verify serialization output matches expected JSON shape

---

### US-003: Configuration options
**Description:** As a service developer, I need to configure the title, version, and description of
my AsyncAPI document so that the output identifies my service correctly.

**Acceptance Criteria:**
- [ ] `AsyncApiOptions` class exists with settable properties: `Title` (default: "Brighter
  Application"), `Version` (default: "1.0.0"), `Description?`
- [ ] `AsyncApiOptions.AssembliesToScan` property exists (`IEnumerable<Assembly>?`, default: null
  which means `AppDomain.CurrentDomain.GetAssemblies()`)
- [ ] `AsyncApiOptions.DisableAssemblyScanning` property exists (`bool`, default: `false`)
- [ ] Unit test verifies default values

---

### US-004: Schema generation via pluggable interface
**Description:** As an advanced user, I want to be able to substitute the default JSON Schema
generator so I can control the schema output (e.g. use a different library or add custom attributes).

**Acceptance Criteria:**
- [ ] `IAmASchemaGenerator` interface exists with method:
  `Task<JsonElement?> GenerateAsync(Type? requestType, CancellationToken ct = default)`
- [ ] `NJsonSchemaGenerator` class implements `IAmASchemaGenerator` using `NJsonSchema.JsonSchema.FromType()`
- [ ] When `requestType` is `null`, returns `JsonElement?` representing `{}` (empty object schema)
- [ ] When `NJsonSchema` throws, returns `JsonElement?` representing `{}` (graceful degradation,
  does not propagate exception)
- [ ] `NJsonSchemaGenerator` is registered as the default `IAmASchemaGenerator` in DI
- [ ] Unit tests cover null type, successful generation, and exception degradation

---

### US-005: Document generator — subscriptions (receive operations)
**Description:** As a service developer, I want subscriptions registered with the Service Activator
to appear as `receive` operations in the AsyncAPI document so consumers know what topics the service
listens on.

**Acceptance Criteria:**
- [ ] `IAmAnAsyncApiDocumentGenerator` interface exists with method:
  `Task<AsyncApiDocument> GenerateAsync(CancellationToken ct = default)`
- [ ] `AsyncApiDocumentGenerator` implements `IAmAnAsyncApiDocumentGenerator`
- [ ] For each subscription in `IAmConsumerOptions.Subscriptions`:
  - Subscription with a non-empty `RoutingKey` produces one `AsyncApiChannel` (address = routing key value)
  - One `AsyncApiOperation` with `action: "receive"` is produced, referencing the channel
  - If `RequestType` is non-null, a `AsyncApiMessage` component is produced with `Name` = type name
    and a `Payload` from the schema generator
  - If `RequestType` is null, a placeholder message named `{channelId}Message` is produced with
    `Payload` = `{}` (empty object schema)
- [ ] Subscription with an empty/null `RoutingKey` is skipped
- [ ] When `IAmConsumerOptions` is not registered in DI, no receive operations are produced (no exception)
- [ ] Unit tests cover: single subscription with type, subscription without type, empty routing key,
  missing consumer options

---

### US-006: Document generator — publications (send operations)
**Description:** As a service developer, I want publications registered in the producer registry
to appear as `send` operations in the AsyncAPI document so consumers know what topics the service
publishes to.

**Acceptance Criteria:**
- [ ] For each `IAmAMessageProducer` in `IAmAProducerRegistry.Producers`:
  - Producer whose `Publication.Topic` is non-empty produces one `AsyncApiChannel` and one
    `AsyncApiOperation` with `action: "send"`
  - `AsyncApiMessage` component is produced as per US-005 rules
  - Producer with empty/null `Publication.Topic` is skipped
- [ ] When `IAmAProducerRegistry` is not registered in DI, no send operations are produced (no exception)
- [ ] Unit tests cover: single producer with topic and type, producer without topic, missing registry

---

### US-007: Document generator — assembly scanning
**Description:** As a service developer, I want `IRequest` types decorated with `[PublicationTopic]`
to be included in the document even if they aren't wired into DI, so the document reflects the
full messaging surface area.

**Acceptance Criteria:**
- [ ] `AsyncApiDocumentGenerator` scans `AsyncApiOptions.AssembliesToScan` (falling back to
  `AppDomain.CurrentDomain.GetAssemblies()`) for concrete, non-abstract types that implement
  `IRequest` and are decorated with `[PublicationTopic]`
- [ ] Each found type produces a `send` operation using `attr.Destination.RoutingKey.Value` as the
  channel address
- [ ] When `DisableAssemblyScanning = true`, no scanning is performed
- [ ] DI sources win: if a routing key+action pair is already covered by a subscription or
  publication, the assembly-scanned entry is skipped (no duplicate channels/operations)
- [ ] `ReflectionTypeLoadException` is caught; types that did load are still processed
- [ ] Unit tests cover: type with attribute produces channel, disabled scanning produces no entry,
  DI source wins deduplication, partial assembly load is handled

---

### US-008: Deduplication and channel merging
**Description:** As a service developer, I want the document to be correct when the same routing
key appears in multiple sources so there are no duplicate channels or messages.

**Acceptance Criteria:**
- [ ] Two subscriptions on the same routing key produce one channel and two receive operations
  (different operation IDs, e.g. using subscription name)
- [ ] A subscription and a publication on the same routing key produce one channel, one receive
  operation, and one send operation
- [ ] The same `IRequest` type appearing in both a subscription and a publication produces only one
  `AsyncApiMessage` component (keyed by type name)
- [ ] Channel IDs and operation IDs containing special characters (`.`, `/`, spaces) are sanitized
  to alphanumeric + hyphens + underscores
- [ ] Unit tests cover all three deduplication scenarios above

---

### US-009: DI registration via `UseAsyncApi()`
**Description:** As a service developer, I want to register the AsyncAPI generator with a single
call on the Brighter builder so I don't need to wire up services manually.

**Acceptance Criteria:**
- [ ] `IBrighterBuilder.UseAsyncApi(Action<AsyncApiOptions>?)` extension method exists in
  `AsyncApiBrighterBuilderExtensions`
- [ ] Calling `UseAsyncApi()` registers `AsyncApiOptions` as a singleton
- [ ] Calling `UseAsyncApi()` registers `IAmAnAsyncApiDocumentGenerator` as a singleton
  (resolving `IAmConsumerOptions` and `IAmAProducerRegistry` via `GetService`, not `GetRequiredService`)
- [ ] Calling `UseAsyncApi()` registers `IAmASchemaGenerator` as `NJsonSchemaGenerator` singleton
  (unless already registered by the user)
- [ ] `IBrighterBuilder` is returned for chaining
- [ ] Unit tests verify all three services are registered after calling `UseAsyncApi()`

---

### US-010: File generation via `GenerateAsyncApiDocumentAsync()`
**Description:** As a service developer, I want to call a single method on the built host to write
the AsyncAPI document to a JSON file, so I can integrate this into a startup argument check or CI step.

**Acceptance Criteria:**
- [ ] `IHost.GenerateAsyncApiDocumentAsync(string outputPath, CancellationToken ct = default)`
  extension method exists and returns `Task<AsyncApiDocument>`
- [ ] The method resolves `IAmAnAsyncApiDocumentGenerator` and calls `GenerateAsync()`
- [ ] Output is serialized with `WriteIndented = true` and `WhenWritingNull` ignore condition
- [ ] The parent directory of `outputPath` is created if it does not exist
- [ ] The generated `AsyncApiDocument` is returned to the caller for inspection or logging
- [ ] `IHost.GenerateAsyncApiDocumentAsync()` throws `InvalidOperationException` with a clear message
  if `UseAsyncApi()` was not called
- [ ] A synchronous overload `GenerateAsyncApiDocument(string outputPath)` exists and returns
  `AsyncApiDocument`
- [ ] Unit tests cover: file is written, directory is created, returned document matches file content,
  exception when not configured, file content is valid JSON

---

### US-011: Sample project demonstrating end-to-end usage
**Description:** As a developer evaluating Brighter, I want a sample project that demonstrates
AsyncAPI generation with a real transport so I can understand the intended usage pattern.

**Acceptance Criteria:**
- [ ] A sample project exists at `samples/AsyncAPI/RMQAsyncAPI/`
- [ ] The sample configures a Brighter service with at least one subscription and one publication
  using the RabbitMQ transport
- [ ] The sample's `Program.cs` includes a `--generate-asyncapi` argument check that calls
  `GenerateAsyncApiDocumentAsync("asyncapi.json")` and exits
- [ ] Running `dotnet run -- --generate-asyncapi` produces a valid `asyncapi.json` file
- [ ] The sample project is added to `Brighter.slnx`
- [ ] `dotnet build` succeeds for the sample

---

## Functional Requirements

- **FR-1**: The `Paramore.Brighter.AsyncAPI` package must target `net8.0`, `net9.0`, and `net10.0`.
- **FR-2**: The package must expose `IBrighterBuilder.UseAsyncApi(Action<AsyncApiOptions>?)` to
  register generator services in DI.
- **FR-3**: The generator must collect channels and operations from `IAmConsumerOptions.Subscriptions`
  (receive), `IAmAProducerRegistry.Producers` (send), and assembly scanning for `[PublicationTopic]`
  (send), in that deduplication priority order.
- **FR-4**: Both `IAmConsumerOptions` and `IAmAProducerRegistry` must be resolved with `GetService`
  (nullable) so the generator works in send-only and receive-only applications.
- **FR-5**: Channel IDs and operation IDs must be sanitized: all characters outside `[a-zA-Z0-9_\-]`
  replaced with `_`.
- **FR-6**: When a `RequestType` is null or `[PublicationTopic]` is absent, the message payload must
  be an empty object schema `{}`, not null/absent.
- **FR-7**: `IAmASchemaGenerator` must be an injectable interface so users can substitute schema
  generation. The default implementation uses NJsonSchema 11.5.2.
- **FR-8**: `IHost.GenerateAsyncApiDocumentAsync(path)` must create any missing parent directories
  before writing the file.
- **FR-9**: The generated JSON must pass validation against the
  [AsyncAPI 3.0 specification](https://www.asyncapi.com/docs/reference/specification/v3.0.0).
- **FR-10**: Assembly scanning must catch `ReflectionTypeLoadException` and continue processing
  types that did load successfully.
- **FR-11**: A sample project must exist demonstrating end-to-end usage with at least one
  subscription and one publication against a real transport.

---

## Non-Goals

The following are explicitly out of scope for this iteration:

- **No YAML output** — JSON only. YamlDotNet is not in the project's package management.
- **No HTTP endpoint** — `GenerateAsyncApiDocumentAsync` writes to a file. An
  `AspNetCore` companion package can be added later.
- **No built-in AsyncAPI Studio UI** — doc rendering is delegated to external tools
  (AsyncAPI Studio, Microcks, etc.).
- **No MSBuild task or `dotnet` global tool** — generation is triggered at runtime after `Build()`.
- **No message filtering** — all subscriptions and publications are included.
- **No custom channel metadata** — only the fields required by AsyncAPI 3.0 core (address, messages,
  action). Extensions (bindings, tags, externalDocs) are deferred.
- **No automatic CI integration** — the user is responsible for calling `GenerateAsyncApiDocumentAsync`
  in their pipeline.

---

## Technical Considerations

- **NJsonSchema** (v11.5.2) is already in `Directory.Packages.props` as a transitive dependency of
  `Paramore.Brighter`. Reference it directly in the new `.csproj` (no version needed).
- **`Microsoft.Extensions.Hosting.Abstractions`** is already in `Directory.Packages.props`.
  Reference it for `IHost` without pulling in the full hosting stack.
- **`RoutingKey.IsNullOrEmpty(routingKey)`** — static method on `RoutingKey`; use this to check for
  empty routing keys rather than comparing `.Value` directly.
- **`[PublicationTopic]` attribute access** — `attr.Destination.RoutingKey.Value` gives the topic string.
- **DI sources for subscriptions** — `IAmConsumerOptions` (registered as both `IBrighterOptions` and
  `IAmConsumerOptions` by `AddConsumers()`). Resolve via `IAmConsumerOptions`.
- **DI sources for publications** — `IAmAProducerRegistry` (registered as singleton). Each
  `IAmAMessageProducer.Publication` has `Topic` (RoutingKey?) and `RequestType` (Type?).
- **`System.Text.Json`** — use `JsonElement` for the schema payload so NJsonSchema JSON output is
  embedded inline (not double-serialized as a string).
- **`Brighter.slnx` format** — add the source project after line 205
  (`src/Paramore.Brighter.Extensions.OpenTelemetry/...`); add the test project inside the
  `<Folder Name="/tests/">` block alphabetically.
- **Target framework property** — use `$(BrighterCoreTargetFrameworks)` in the `.csproj`, not a
  hardcoded list.

---

## Success Metrics

- `dotnet build Brighter.slnx` succeeds with the new projects included.
- All unit tests in `tests/Paramore.Brighter.AsyncAPI.Tests/` pass.
- The generated `asyncapi.json` from the sample project validates successfully using the
  [AsyncAPI CLI](https://www.asyncapi.com/tools/cli): `asyncapi validate asyncapi.json`.
- The package can be referenced from a new project and generate a document with fewer than 10 lines
  of setup code.

---

## Resolved Questions

- **Which transport for the sample?** RabbitMQ. It is the most commonly used in existing samples and
  can be run locally via Docker without cloud credentials.
- **Should `GenerateAsyncApiDocumentAsync` return the document?** Yes. The method returns
  `Task<AsyncApiDocument>` so callers can inspect, log, or further process the document without
  re-reading and deserializing the file. Callers who only care about the file can discard the return value.

---

## PR Review Feedback (PR #4034)

The following amendments incorporate feedback from the maintainer review on PR #4034.

### Amendment 1: Use the AsyncAPI .NET SDK instead of custom POCOs

**Reviewer:** iancooper
**Impact:** US-002 (Document Model), FR-7, Technical Considerations

There is an official [AsyncAPI .NET SDK](https://github.com/asyncapi/net-sdk) (`LEGO.AsyncAPI`)
that defines the abstractions of the specification and the various protocols. Using this instead of
custom POCOs means:
- We don't need to define the model primitives ourselves
- We get built-in support for bindings, tags, and protocol-specific metadata
- Community-maintained spec compliance — we don't need to track spec changes

**Action:** Evaluate replacing the custom `Model/*.cs` POCOs with `LEGO.AsyncAPI` types. If adopted,
US-002 acceptance criteria should change from verifying custom POCO serialization to verifying correct
population of the SDK's `AsyncApiDocument`. The `IAmASchemaGenerator` interface (US-004) and generator
logic (US-005 through US-008) remain the same — only the model layer changes.

### Amendment 2: Support YAML output alongside JSON

**Reviewer:** iancooper
**Impact:** Non-Goals, US-010, FR-9

The original PRD listed YAML as a non-goal. The reviewer notes that YAML is more human-readable and
is readily convertible from JSON, so we should support both formats without requiring two passes.

**Action:** Move "No YAML output" out of Non-Goals. Update US-010 to support writing both
`asyncapi.json` and `asyncapi.yaml` (or accept a format parameter). If adopting the AsyncAPI .NET SDK
(Amendment 1), YAML serialization comes built-in.

### Amendment 3: Add a Kafka sample project

**Reviewer:** iancooper
**Impact:** US-011, FR-11

Kafka is the major Brighter use case at JET. A Kafka sample would be valuable in addition to the
RabbitMQ sample.

**Action:** Add a second sample at `samples/AsyncAPI/KafkaAsyncAPI/` demonstrating end-to-end usage
with the Kafka transport. This can be a follow-up PR after the RabbitMQ sample lands.

### Amendment 4: Support for custom schema attributes

**Reviewer:** iancooper
**Impact:** US-004 (Schema Generation)

Brighter has custom attributes used for schema generation, and it would be useful to support these
in the schema output.

**Action:** Ensure `IAmASchemaGenerator` is flexible enough for implementations to inspect custom
attributes on `IRequest` types. The default NJsonSchema-based generator should honour standard
`System.ComponentModel.DataAnnotations` and `JsonProperty` attributes. Support for Brighter-specific
custom attributes can be added as a separate `IAmASchemaGenerator` implementation or via
NJsonSchema's extensibility points.

### Amendment 5: Bindings and tags support (future PR)

**Reviewer:** iancooper
**Impact:** Non-Goals

The reviewer wants bindings and tags support eventually, likely via attributes on message types or
configuration. This was listed as a non-goal for this iteration.

**Action:** Keep as a non-goal for the initial PR but add as planned follow-up work. If the AsyncAPI
.NET SDK (Amendment 1) is adopted, the model already supports bindings and tags — only the attribute
discovery and population logic would be needed.

### Amendment 6: Sample project conventions

**Reviewer:** iancooper
**Impact:** US-011

- **Copyright headers:** Contributors keep IP and license it to the project. Copyright headers in
  sample files should use the contributor's name, not a generic placeholder.
- **Default mappers:** The sample should use Brighter's default message mapper rather than explicit
  custom `MessageMapper` implementations, unless there is a specific reason for custom mappers.

### Amendment 7: Code quality issues (CodeScene / Copilot)

**Impact:** US-005, US-006, US-007

Automated reviewers flagged several code health issues in `AsyncApiDocumentGenerator.cs`:
- **Code duplication** between `AddPublicationsAsync` and `AddSubscriptionsAsync` — extract shared logic
- **Complex method** `GetPublicationTopicTypes` (cyclomatic complexity 12, threshold 9) — simplify
- **Bumpy road** in `AddFromAssemblyScanningAsync` and `RewriteRefs` — reduce nesting
- **Excess function arguments** in `ProcessSourceAsync` (5 args, max 4) — consider a parameter object
- **Primitive obsession / string-heavy arguments** (43.2% primitive, threshold 30%) — partially
  addressed by earlier value-type refactoring, further work needed
- **Bug:** Assembly-scanned operations use hardcoded `sendOpId` instead of `GetUniqueOperationId`,
  which can silently overwrite if two scanned types share the same topic

### Amendment 8: TFM and solution file fixes

**Reviewer:** Copilot
**Impact:** US-001, FR-1

- Both `Paramore.Brighter.AsyncAPI` and `Paramore.Brighter.AsyncAPI.NJsonSchema` hardcode
  `net9.0;net10.0` instead of `$(BrighterCoreTargetFrameworks)` (`net8.0;net9.0;net10.0`).
  Net8.0 consumers cannot use these packages.
- Source projects (`Paramore.Brighter.AsyncAPI`, `Paramore.Brighter.AsyncAPI.NJsonSchema`) and the
  test project (`Paramore.Brighter.AsyncAPI.Tests`) are not added to `Brighter.slnx`. Only the
  sample was added.

**Action:** These are bugs in the current implementation that should be fixed immediately.
