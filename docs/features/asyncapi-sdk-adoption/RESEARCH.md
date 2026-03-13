# Feature: AsyncAPI SDK Adoption -- Research

**Researched:** 2026-03-13
**Domain:** AsyncAPI 3.0 document generation, .NET SDK adoption
**Confidence:** MEDIUM (network access unavailable during research; findings based on training data through May 2025 plus codebase analysis -- versions and API details should be verified against current NuGet/GitHub before implementation)

## Summary

The Brighter AsyncAPI 3.0 document generator currently uses 8 custom POCOs (AsyncApiDocument, AsyncApiInfo, AsyncApiChannel, AsyncApiOperation, AsyncApiMessage, AsyncApiComponents, AsyncApiRef, AsyncApiServer) with System.Text.Json serialization. The maintainer (iancooper) requested evaluation of the official AsyncAPI .NET SDK as a replacement (PR #4034, Amendment 1).

The official SDK is **LEGO.AsyncAPI** (NuGet package name: `AsyncAPI.NET`), maintained under the AsyncAPI GitHub organization at `asyncapi/asyncapi-dotnet` (formerly `LEGO/AsyncAPI.NET`). As of my training data (May 2025), versions 5.x were stable and targeted AsyncAPI 2.x, while version 6.x (initially beta, likely GA by now) added AsyncAPI 3.0 support via a separate namespace (`LEGO.AsyncAPI.V3`). The SDK provides typed document models, JSON and YAML serialization, `$ref` resolution, and protocol binding models -- all of which would replace custom code in the current implementation.

**Primary recommendation:** Adopt `AsyncAPI.NET` version 6.x (the latest stable release supporting AsyncAPI 3.0). This eliminates the custom POCO layer, adds YAML support for free, handles `$ref` semantics properly, and provides bindings models for future Kafka/AMQP/RMQ work. The `IAmASchemaGenerator` interface and generator logic remain unchanged -- only the model layer and serialization change.

## User Constraints (from FEATURE.md / PRD)

### Locked Decisions
- Use the official AsyncAPI .NET SDK (`LEGO.AsyncAPI`) per maintainer review (Amendment 1)
- Support both JSON and YAML output (Amendment 2)
- Maintain `IAmASchemaGenerator` interface for pluggable schema generation
- Target `net8.0;net9.0;net10.0` via `$(BrighterCoreTargetFrameworks)`
- NJsonSchema 11.5.2 remains the default schema generator (separate package)

### Out of Scope
- HTTP endpoint serving
- AsyncAPI Studio UI
- MSBuild task / dotnet global tool
- Message filtering
- Bindings/tags population (model support comes free with SDK; attribute discovery is deferred)

## Standard Stack

### Core
| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| AsyncAPI.NET | 6.x (latest stable) | AsyncAPI document model, serialization (JSON+YAML), $ref handling | Official AsyncAPI organization SDK; community-maintained spec compliance |
| NJsonSchema | 11.5.2 | JSON Schema generation from .NET types | Already in Directory.Packages.props; default `IAmASchemaGenerator` impl |

### Supporting
| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| Microsoft.Extensions.DependencyInjection.Abstractions | 10.0.4 | DI registration | Already in use |
| Microsoft.Extensions.Hosting.Abstractions | 10.0.4 | IHost extension methods | Already in use |
| System.Text.Json | (framework) | Schema payload interchange format | Already in use for IAmASchemaGenerator |

### Alternatives Considered
| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| AsyncAPI.NET (LEGO) | ByteBard.AsyncAPI.NET | ByteBard is a community fork, less active, unclear AsyncAPI 3.0 support. Not recommended. |
| AsyncAPI.NET (LEGO) | Custom POCOs (current) | Works but must track spec changes manually, no bindings/tags, no YAML, no $ref resolution. Maintainer explicitly requested SDK adoption. |

**Installation:**
```bash
# Add to Directory.Packages.props
<PackageVersion Include="AsyncAPI.NET" Version="6.3.1" />

# Add to Paramore.Brighter.AsyncAPI.csproj
<PackageReference Include="AsyncAPI.NET" />
```

**NOTE:** The exact latest version should be verified on NuGet before implementation. As of my training data, 6.0.0-beta existed; stable 6.x releases may now be available. Check https://www.nuget.org/packages/AsyncAPI.NET for the current version.

## Architecture Patterns

### Recommended Project Structure (post-migration)
```
src/
  Paramore.Brighter.AsyncAPI/
    AsyncApiDocumentGenerator.cs     # Uses LEGO.AsyncAPI.V3 models instead of custom POCOs
    AsyncApiOptions.cs               # Servers property type changes to SDK server type
    AsyncApiHostExtensions.cs        # Uses SDK serializer for JSON+YAML output
    AsyncApiBrighterBuilderExtensions.cs  # DI registration (unchanged)
    IAmAnAsyncApiDocumentGenerator.cs     # Return type changes to SDK document type
    IAmASchemaGenerator.cs           # UNCHANGED -- still returns JsonElement?
    Model/                           # DELETED -- replaced by SDK types
  Paramore.Brighter.AsyncAPI.NJsonSchema/
    NJsonSchemaGenerator.cs          # UNCHANGED -- still implements IAmASchemaGenerator
```

### Pattern 1: SDK Document Construction (AsyncAPI 3.0)
**What:** Build an `AsyncApiDocument` using the SDK's V3 model types
**When to use:** Everywhere the generator currently creates custom POCOs

The SDK v6 organizes AsyncAPI 3.0 types under a `V3` namespace (the exact namespace needs verification -- likely `LEGO.AsyncAPI.V3.Models` or `LEGO.AsyncAPI.Models.V3`). Key types include:

```csharp
// MEDIUM confidence -- API shape based on training data, verify against actual package
using LEGO.AsyncAPI.Models;  // or V3 sub-namespace

var document = new AsyncApiDocument
{
    AsyncApi = "3.0.0",
    Info = new AsyncApiInfo
    {
        Title = "My Service",
        Version = "1.0.0",
        Description = "Service description"
    },
    Servers = new Dictionary<string, AsyncApiServer>
    {
        ["production"] = new AsyncApiServer
        {
            Host = "broker.example.com",
            Protocol = "amqp"
        }
    },
    Channels = new Dictionary<string, AsyncApiChannel>
    {
        ["userSignedUp"] = new AsyncApiChannel
        {
            Address = "user.signed-up",
            Messages = new Dictionary<string, AsyncApiMessage>
            {
                ["UserSignedUp"] = new AsyncApiMessage
                {
                    // $ref to components
                    Reference = new AsyncApiReference
                    {
                        Type = ReferenceType.Message,
                        Id = "UserSignedUp"
                    }
                }
            }
        }
    },
    Operations = new Dictionary<string, AsyncApiOperation>
    {
        ["receiveUserSignedUp"] = new AsyncApiOperation
        {
            Action = OperationAction.Receive,
            Channel = new AsyncApiChannel
            {
                Reference = new AsyncApiReference
                {
                    Type = ReferenceType.Channel,
                    Id = "userSignedUp"
                }
            }
        }
    },
    Components = new AsyncApiComponents
    {
        Messages = new Dictionary<string, AsyncApiMessage>
        {
            ["UserSignedUp"] = new AsyncApiMessage
            {
                Name = "UserSignedUp",
                ContentType = "application/json",
                // Payload handling -- see Schema/Payload section below
            }
        }
    }
};
```

### Pattern 2: Schema/Payload Integration
**What:** Bridge between NJsonSchema output (JsonElement) and the SDK's schema model
**When to use:** When setting message payloads

**This is the critical integration point.** The SDK has its own `AsyncApiSchema` type (or uses `JsonSchema` from a related library). The key question is whether it accepts raw JSON or requires its own schema objects.

Based on training data, the SDK's schema model supports:
- Construction from `JsonNode`/`JsonElement` (needs verification)
- An `Extensions` dictionary for pass-through properties
- Or: serialize NJsonSchema output to JSON string, then parse it with the SDK's schema reader

```csharp
// Approach A: If SDK accepts JsonNode/JsonElement for schema payload
// (NEEDS VERIFICATION)
var schemaJson = await _schemaGenerator.GenerateAsync(requestType, ct);
message.Payload = AsyncApiSchemaFactory.FromJsonElement(schemaJson.Value);

// Approach B: If SDK has its own schema model, parse from JSON string
var schemaJson = await _schemaGenerator.GenerateAsync(requestType, ct);
var rawJson = schemaJson.Value.GetRawText();
message.Payload = new AsyncApiSchema();  // populate from parsed JSON

// Approach C: Use SDK's "any" type or extension data for raw schema passthrough
// Some SDK versions support AsyncApiAny or similar for untyped JSON
```

**IMPORTANT:** The exact approach depends on the SDK's `AsyncApiMessage.Payload` property type. This MUST be verified by examining the actual SDK types after package restore. The `IAmASchemaGenerator` interface returning `JsonElement?` was designed for System.Text.Json serialization; it may need adaptation for the SDK's schema model.

### Pattern 3: Serialization (JSON + YAML)
**What:** Use the SDK's built-in serializers for output
**When to use:** In `AsyncApiHostExtensions.GenerateAsyncApiDocumentAsync`

```csharp
// MEDIUM confidence -- based on training data
using LEGO.AsyncAPI;
using LEGO.AsyncAPI.Writers;

// JSON output
using var jsonWriter = new StringWriter();
document.SerializeAsJson(jsonWriter, AsyncApiVersion.AsyncApi3_0);
var json = jsonWriter.ToString();

// YAML output
using var yamlWriter = new StringWriter();
document.SerializeAsYaml(yamlWriter, AsyncApiVersion.AsyncApi3_0);
var yaml = yamlWriter.ToString();
```

The SDK uses its own serialization pipeline (not System.Text.Json). This means:
- The `JsonPropertyName` attributes on our custom POCOs become irrelevant (deleted with the POCOs)
- The static `s_serializerOptions` in `AsyncApiHostExtensions` is replaced by SDK serializer calls
- No conflict with System.Text.Json since they operate at different layers

### Pattern 4: $ref Handling
**What:** The SDK manages `$ref` references natively
**When to use:** Replaces the custom `RewriteEmbeddedSchemaRefs` / `RewriteRefs` logic

The SDK's `AsyncApiReference` type handles `$ref` creation and resolution. When you set a `Reference` property on a model object, the serializer emits the correct `$ref` string. This means:
- The custom `AsyncApiRef` POCO is deleted
- The `RewriteEmbeddedSchemaRefs` method can likely be simplified or eliminated
- **CAVEAT:** The NJsonSchema `$ref` rewriting for *internal schema refs* (e.g., `#/definitions/Foo` to `#/components/messages/X/payload/definitions/Foo`) may still be needed if the SDK does not perform this rewriting automatically for embedded schemas

### Anti-Patterns to Avoid
- **Wrapping SDK types in custom wrappers:** Use SDK types directly throughout the generator. Do not create adapter/wrapper classes around `AsyncApiDocument` etc.
- **Mixing serialization approaches:** Do not use System.Text.Json to serialize the SDK document. Use the SDK's own `SerializeAsJson`/`SerializeAsYaml` methods.
- **Depending on prerelease versions in production NuGet packages:** If only beta versions support 3.0, document the risk and pin to an exact version.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| AsyncAPI document model | Custom POCOs (current 8 files) | `AsyncAPI.NET` SDK types | Spec compliance maintained by community; bindings, tags, security schemes included |
| $ref semantics | Custom `AsyncApiRef` + string interpolation | `AsyncApiReference` from SDK | SDK handles serialization format, type-safe reference types |
| JSON serialization | `System.Text.Json` with `JsonPropertyName` | SDK's `SerializeAsJson()` | Ensures spec-compliant property names, ordering, null handling |
| YAML serialization | Manual JSON-to-YAML conversion | SDK's `SerializeAsYaml()` | Built-in; no need for YamlDotNet dependency at the Brighter level |
| Protocol bindings model | Custom binding POCOs (future) | SDK's binding types (Kafka, AMQP, etc.) | Already defined in SDK; just populate when ready |

**Key insight:** The custom POCO approach requires tracking every AsyncAPI spec change manually. The SDK is maintained by the AsyncAPI organization and provides a typed model that evolves with the spec. The SDK also provides capabilities (YAML, bindings, $ref resolution) that would be significant effort to build manually.

## Common Pitfalls

### Pitfall 1: Schema Payload Type Mismatch
**What goes wrong:** The `IAmASchemaGenerator` returns `JsonElement?` (System.Text.Json), but the SDK's `AsyncApiMessage.Payload` expects an SDK-specific type (likely `AsyncApiSchema` or `AsyncApiAny`).
**Why it happens:** The interface was designed for direct System.Text.Json serialization, not for SDK interop.
**How to avoid:** After restoring the SDK package, inspect `AsyncApiMessage.Payload`'s type. If it's not `JsonElement`, create a conversion method. The `IAmASchemaGenerator` interface signature may need to change, or a conversion adapter is needed in the generator.
**Warning signs:** Compilation errors on `message.Payload = schemaJson` assignment.

**Possible resolutions (verify which applies):**
1. If `Payload` is `AsyncApiSchema`: parse the JsonElement into SDK schema type
2. If `Payload` is `AsyncApiAny`/`IAsyncApiAny`: wrap the JsonElement in the SDK's "any" type
3. If `Payload` accepts `JsonNode`: convert `JsonElement` to `JsonNode` (straightforward)
4. Change `IAmASchemaGenerator` to return the SDK's schema type instead of `JsonElement?`

Option 4 would couple the core interface to the SDK, which may be undesirable if `IAmASchemaGenerator` is meant to be SDK-agnostic. A conversion in the generator (options 1-3) keeps the interface clean.

### Pitfall 2: NJsonSchema $ref Rewriting Still Needed
**What goes wrong:** Assuming the SDK handles all `$ref` rewriting, but NJsonSchema emits internal refs like `#/definitions/Foo` that need to be rewritten to `#/components/messages/X/payload/definitions/Foo` when embedded in an AsyncAPI document.
**Why it happens:** NJsonSchema generates standalone JSON Schema documents with `$ref` paths relative to the schema root. When this schema is embedded inside an AsyncAPI document at a different path, the refs become invalid.
**How to avoid:** Test with a complex type (one that has nested types generating `$ref`s in NJsonSchema output). If the SDK's schema model properly re-roots refs during serialization, the custom rewriting can be removed. If not, keep the `RewriteEmbeddedSchemaRefs` logic but adapt it to work with the SDK's schema representation.
**Warning signs:** Invalid `$ref` paths in the generated document; AsyncAPI CLI validation failures.

### Pitfall 3: V2 vs V3 Namespace Confusion
**What goes wrong:** Using AsyncAPI 2.x model types instead of 3.0 types.
**Why it happens:** The SDK supports both 2.x and 3.0, potentially in different namespaces or with version-specific types. In 2.x, operations had `publish`/`subscribe`; in 3.0, it's `send`/`receive` with separated channels and operations.
**How to avoid:** Explicitly use the V3 namespace/types. Verify that `AsyncApiOperation.Action` is an enum with `Send`/`Receive` values (3.0 semantics), not `Publish`/`Subscribe` (2.x semantics).
**Warning signs:** No `Action` property on `AsyncApiOperation`; finding `Publish`/`Subscribe` properties instead.

### Pitfall 4: TFM Compatibility
**What goes wrong:** SDK does not target all of `net8.0;net9.0;net10.0`.
**Why it happens:** SDK was developed before net10.0 was released; may only target `netstandard2.0` or specific TFMs.
**How to avoid:** Check the SDK package's supported frameworks. If it targets `netstandard2.0` or `netstandard2.1`, it works with all three TFMs. If it targets specific `net*` frameworks, verify compatibility.
**Warning signs:** Build errors referencing framework incompatibility.

### Pitfall 5: Serialization Output Differences
**What goes wrong:** Tests comparing exact JSON output break because the SDK serializer produces slightly different formatting/ordering than the custom System.Text.Json approach.
**Why it happens:** Different serializers have different default behaviors for property ordering, whitespace, null handling.
**How to avoid:** Update tests to validate semantic correctness (parse and compare structures) rather than exact string comparison. Or use the SDK's serializer consistently for all test assertions.
**Warning signs:** Tests that compared exact JSON strings start failing after migration.

### Pitfall 6: AsyncAPI.NET Transitive Dependency Conflicts
**What goes wrong:** The SDK brings in dependencies that conflict with existing Brighter dependencies.
**Why it happens:** The SDK may depend on YamlDotNet, Newtonsoft.Json, or Microsoft.OpenApi at specific versions.
**How to avoid:** After `dotnet restore`, run `dotnet list package --include-transitive` and check for version conflicts. Pin conflicting versions in `Directory.Packages.props` if needed.
**Warning signs:** NU1605 (downgrade) or NU1608 (version above resolved) warnings during restore.

## Code Examples

### Current vs SDK Document Construction

**Current (custom POCOs):**
```csharp
var doc = new AsyncApiDocument
{
    Info = new AsyncApiInfo { Title = "My Service", Version = "1.0.0" },
    Channels = new Dictionary<string, AsyncApiChannel>
    {
        ["orders"] = new AsyncApiChannel
        {
            Address = "orders.created",
            Messages = new Dictionary<string, AsyncApiRef>
            {
                ["OrderCreated"] = new AsyncApiRef { Ref = "#/components/messages/OrderCreated" }
            }
        }
    },
    Operations = new Dictionary<string, AsyncApiOperation>
    {
        ["send_orders"] = new AsyncApiOperation
        {
            Action = "send",
            Channel = new AsyncApiRef { Ref = "#/channels/orders" },
            Messages = new List<AsyncApiRef>
            {
                new AsyncApiRef { Ref = "#/channels/orders/messages/OrderCreated" }
            }
        }
    },
    Components = new AsyncApiComponents
    {
        Messages = new Dictionary<string, AsyncApiMessage>
        {
            ["OrderCreated"] = new AsyncApiMessage
            {
                Name = "OrderCreated",
                ContentType = "application/json",
                Payload = schemaJsonElement
            }
        }
    }
};

// Serialization
var json = JsonSerializer.Serialize(doc, serializerOptions);
```

**After migration (SDK types -- MEDIUM confidence, verify API):**
```csharp
// Namespace will be one of:
// using LEGO.AsyncAPI.Models;
// using LEGO.AsyncAPI.Models.V3;
// Verify after package restore

var doc = new AsyncApiDocument
{
    Info = new AsyncApiInfo
    {
        Title = "My Service",
        Version = "1.0.0"
    }
};

// Add channel
var channel = new AsyncApiChannel
{
    Address = "orders.created"
};

// Add message to components
var message = new AsyncApiMessage
{
    Name = "OrderCreated",
    ContentType = "application/json",
    Payload = ConvertToSdkSchema(schemaJsonElement)  // conversion needed
};

doc.Components = new AsyncApiComponents
{
    Messages = new Dictionary<string, AsyncApiMessage>
    {
        ["OrderCreated"] = message
    }
};

// Channel message refs use SDK reference type
channel.Messages = new Dictionary<string, AsyncApiMessage>
{
    ["OrderCreated"] = new AsyncApiMessage
    {
        Reference = new AsyncApiReference
        {
            Type = ReferenceType.Message,
            Id = "OrderCreated"
        }
    }
};

doc.Channels = new Dictionary<string, AsyncApiChannel>
{
    ["orders"] = channel
};

// Operation with channel ref
doc.Operations = new Dictionary<string, AsyncApiOperation>
{
    ["send_orders"] = new AsyncApiOperation
    {
        Action = OperationAction.Send,
        Channel = new AsyncApiChannel
        {
            Reference = new AsyncApiReference
            {
                Type = ReferenceType.Channel,
                Id = "orders"
            }
        }
    }
};

// Serialization -- JSON
using var jsonWriter = new StringWriter();
doc.SerializeAsJson(jsonWriter);
var json = jsonWriter.ToString();

// Serialization -- YAML
using var yamlWriter = new StringWriter();
doc.SerializeAsYaml(yamlWriter);
var yaml = yamlWriter.ToString();
```

### Schema Payload Conversion Helper (Skeleton)
```csharp
// This method bridges IAmASchemaGenerator (JsonElement?) to the SDK's schema type.
// The exact implementation depends on what AsyncApiMessage.Payload expects.
// MUST be verified after package restore.

private static AsyncApiSchema? ConvertToSdkSchema(JsonElement? jsonElement)
{
    if (jsonElement is null) return null;

    // Option A: If SDK can parse from JSON string
    var json = jsonElement.Value.GetRawText();
    return AsyncApiSchema.Parse(json);  // hypothetical -- verify API

    // Option B: If SDK has a JsonNode-based constructor
    var node = JsonNode.Parse(jsonElement.Value.GetRawText());
    return new AsyncApiSchema(node);  // hypothetical -- verify API

    // Option C: Manual property mapping (least desirable)
}
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| AsyncAPI 2.x (`publish`/`subscribe`) | AsyncAPI 3.0 (`send`/`receive`, separated channels/operations) | AsyncAPI 3.0 spec: Dec 2023 | Document model fundamentally different |
| LEGO.AsyncAPI 5.x (AsyncAPI 2.x only) | AsyncAPI.NET 6.x (AsyncAPI 3.0 support) | SDK v6 beta: ~2024 | New V3 namespace/types |
| Custom POCOs + System.Text.Json | SDK model + SDK serializer | This migration | Eliminates 8 custom files, adds YAML |

**Deprecated/outdated:**
- `ByteBard.AsyncAPI.NET`: Community fork, unclear maintenance status. The LEGO/AsyncAPI.NET package has been adopted into the official AsyncAPI organization and is the canonical .NET SDK.
- AsyncAPI 2.x `publish`/`subscribe` semantics: Replaced by 3.0 `send`/`receive` actions.

## Migration Impact Assessment

### Files to Delete (custom POCOs)
- `Model/AsyncApiDocument.cs`
- `Model/AsyncApiInfo.cs`
- `Model/AsyncApiChannel.cs`
- `Model/AsyncApiOperation.cs`
- `Model/AsyncApiMessage.cs`
- `Model/AsyncApiComponents.cs`
- `Model/AsyncApiRef.cs`
- `Model/AsyncApiServer.cs`

### Files to Modify
- `AsyncApiDocumentGenerator.cs` -- use SDK types instead of custom POCOs; potentially simplify/remove `RewriteEmbeddedSchemaRefs`
- `AsyncApiHostExtensions.cs` -- replace System.Text.Json serialization with SDK serialization; add YAML support
- `AsyncApiOptions.cs` -- `Servers` dictionary value type changes from custom `AsyncApiServer` to SDK type
- `IAmAnAsyncApiDocumentGenerator.cs` -- return type changes from custom `AsyncApiDocument` to SDK type
- `Paramore.Brighter.AsyncAPI.csproj` -- add `AsyncAPI.NET` PackageReference, remove `System.Text.Json` if no longer needed directly

### Files Unchanged
- `IAmASchemaGenerator.cs` -- interface stays `Task<JsonElement?>` (conversion happens in generator)
- `NJsonSchemaGenerator.cs` -- implementation unchanged
- `AsyncApiBrighterBuilderExtensions.cs` -- DI registration logic unchanged (type names change)

### Test Impact
- Tests asserting custom POCO serialization format must be rewritten for SDK serialization output
- Tests verifying document structure can be adapted to assert against SDK model properties
- The `RewriteRefs` tests need re-evaluation based on whether SDK handles this

## Package Selection Analysis

### LEGO/AsyncAPI.NET (Recommended)
- **NuGet:** `AsyncAPI.NET`
- **GitHub:** `asyncapi/asyncapi-dotnet` (transferred from `LEGO/AsyncAPI.NET`)
- **Maintained by:** AsyncAPI organization (LEGO originally, now community)
- **AsyncAPI 3.0:** Supported in v6.x
- **License:** MIT
- **TFMs:** Likely `netstandard2.0` + modern TFMs (verify)
- **Confidence:** MEDIUM (based on training data)

### ByteBard/AsyncAPI.NET (Not Recommended)
- **NuGet:** `ByteBard.AsyncAPI.NET`
- **Last version:** 2.0.1 (as mentioned in research question)
- **AsyncAPI 3.0:** Unknown/unlikely
- **Status:** Appears to be a fork with lower activity
- **Confidence:** LOW

### Recommendation
Use `AsyncAPI.NET` (LEGO/official). It is the only SDK endorsed by the AsyncAPI organization for .NET. The `ByteBard` variant should not be considered.

## Stability and Risk Assessment

### Version Strategy
**Recommendation:** Use the latest stable 6.x release. If only beta releases support 3.0, document this risk explicitly.

**If stable 6.x exists (likely by now):**
- Use it. Pin exact version in `Directory.Packages.props`.
- Risk: LOW. Stable releases have semver guarantees.

**If only 6.x beta exists:**
- Risk: MEDIUM. API may change before GA.
- Mitigation: Pin exact prerelease version. Wrap SDK types behind the existing `IAmAnAsyncApiDocumentGenerator` interface so changes are isolated to the generator implementation.
- The existing `IAmASchemaGenerator` abstraction already provides isolation for the schema layer.

### Breaking Changes Risk
The Brighter project's `IAmAnAsyncApiDocumentGenerator` and `IAmASchemaGenerator` interfaces provide good isolation. Even if the SDK's API changes:
- Only `AsyncApiDocumentGenerator.cs` and `AsyncApiHostExtensions.cs` need updates
- The public API surface of `Paramore.Brighter.AsyncAPI` (the interfaces, options, and extension methods) can remain stable
- Downstream consumers of the Brighter package are insulated from SDK changes

## Open Questions

1. **Exact SDK version and namespace for V3 types**
   - What we know: v6.x adds AsyncAPI 3.0 support; types are likely in a V3 sub-namespace
   - What's unclear: Exact namespace, exact latest stable version number
   - Recommendation: Run `dotnet add package AsyncAPI.NET` and inspect types via IDE after restore. Check NuGet for latest version.

2. **Message.Payload type in the SDK**
   - What we know: Custom POCOs use `JsonElement?`; SDK will have its own schema type
   - What's unclear: Whether it's `AsyncApiSchema`, `JsonNode`, `AsyncApiAny`, or something else
   - Recommendation: After package restore, check the type of `AsyncApiMessage.Payload`. This determines the conversion strategy from `IAmASchemaGenerator` output. **This is the highest-priority verification item.**

3. **$ref rewriting for embedded NJsonSchema output**
   - What we know: NJsonSchema emits `#/definitions/Foo` refs; these need path-adjustment when embedded in AsyncAPI
   - What's unclear: Whether the SDK's schema serialization handles this automatically
   - Recommendation: Create a test with a complex nested type, generate schema via NJsonSchema, embed in SDK document, serialize, and check if `$ref` paths are valid. If not, port the `RewriteEmbeddedSchemaRefs` logic.

4. **Transitive dependencies**
   - What we know: SDK likely depends on YamlDotNet (for YAML serialization) and potentially Microsoft.OpenApi
   - What's unclear: Exact dependency versions and whether they conflict with Brighter's existing deps
   - Recommendation: Run `dotnet list package --include-transitive` after adding the package. Check for conflicts in `Directory.Packages.props`.

5. **SDK serialization control (indentation, null handling)**
   - What we know: Current code uses `WriteIndented = true` and `WhenWritingNull` ignore
   - What's unclear: Whether the SDK serializer respects these settings or has its own configuration
   - Recommendation: Test serialization output and verify it meets the "human-readable" requirement. The SDK likely produces formatted output by default.

## Sources

### Primary (HIGH confidence)
- **Existing codebase** (ground truth) -- all 8 custom POCOs, generator logic, NJsonSchema integration, $ref rewriting examined directly

### Secondary (MEDIUM confidence)
- **Training data knowledge of LEGO.AsyncAPI / AsyncAPI.NET** -- package exists, v5.x stable for AsyncAPI 2.x, v6.x adds 3.0 support, maintained under AsyncAPI GitHub org. API shape is approximate.
- **AsyncAPI 3.0 specification** -- send/receive actions, separated channels/operations, components structure. Well-established in training data.
- **PRD Amendment 1** (from maintainer) -- explicitly requests SDK adoption with rationale

### Tertiary (LOW confidence)
- **Exact version numbers beyond 6.0.0-beta** -- could not verify current NuGet state; version 6.3.1 mentioned in research questions not verified
- **ByteBard.AsyncAPI.NET status** -- limited information in training data
- **SDK internal serialization details** -- property ordering, null handling, schema type specifics need runtime verification

## Metadata

**Confidence breakdown:**
- Standard stack: MEDIUM -- SDK choice is certain (maintainer-locked), but exact version and API details need NuGet verification
- Architecture: MEDIUM -- migration pattern is clear, but schema payload integration is the key unknown
- Pitfalls: HIGH -- identified from direct codebase analysis of the current $ref handling, serialization, and type compatibility concerns
- Code examples: LOW-MEDIUM -- API shapes are approximate based on training data; treat as starting points, not copy-paste solutions

**Research date:** 2026-03-13
**Valid until:** 7 days (fast-moving: SDK versions should be verified before implementation begins)

**Research limitations:** Network access (WebSearch, NuGet, GitHub API) was unavailable during this research session. All SDK-specific findings are based on training data through May 2025. Before starting implementation, the implementer MUST:
1. Verify the latest `AsyncAPI.NET` version on NuGet
2. Restore the package and inspect V3 model types in an IDE
3. Determine the exact type of `AsyncApiMessage.Payload`
4. Run `dotnet list package --include-transitive` to check for dependency conflicts
