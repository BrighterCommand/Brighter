# Requirements

> **Note**: This document captures user requirements and needs. Technical design decisions and implementation details are documented in [ADR 0040](../../docs/adr/0040-asyncapi-document-generation.md).

## Problem Statement

Brighter services expose async messaging contracts (topics they publish to, topics they subscribe from,
and the message types on each), but there is no machine-readable way to document these contracts. Teams
must maintain messaging documentation manually, which becomes stale and diverges from the actual
configuration.

**User Story**: As a developer configuring a Brighter-based service, I want to auto-generate an AsyncAPI
3.0 JSON document from my service's runtime configuration so I can produce accurate API documentation as
part of my CI/CD pipeline without extra manual work.

## Proposed Solution

Add a new `Paramore.Brighter.AsyncAPI` NuGet package that generates an AsyncAPI 3.0 JSON document from
Brighter's runtime configuration and writes it to a file.

## Requirements

### R1: Document Generation

- R1.1: The package must generate a valid AsyncAPI 3.0 JSON document.
- R1.2: The document must include channels, operations, and message schemas.
- R1.3: Generation is triggered by calling `IHost.GenerateAsyncApiDocumentAsync(path)` after building the host.
- R1.4: The method must return the `AsyncApiDocument` object for caller inspection.

### R2: Data Sources

- R2.1: Subscriptions from `IAmConsumerOptions.Subscriptions` must produce `receive` operations.
- R2.2: Publications from `IAmAProducerRegistry.Producers` must produce `send` operations.
- R2.3: Assembly scanning for `IRequest` types with `[PublicationTopic]` must produce `send` operations.
- R2.4: DI-registered sources take priority during deduplication (assembly scan does not duplicate).
- R2.5: Send-only and receive-only applications must be supported (no exceptions when one source is absent).

### R3: Schema Generation

- R3.1: JSON Schema payloads must be generated for each `IRequest` type.
- R3.2: Schema generation must be pluggable via `IAmASchemaGenerator` interface.
- R3.3: The default implementation must use NJsonSchema.
- R3.4: When a type is null or schema generation fails, an empty object schema `{}` must be produced.

### R4: Configuration

- R4.1: Users must be able to configure title, version, and description for the document info block.
- R4.2: Users must be able to specify assemblies to scan or disable scanning entirely.
- R4.3: DI registration must be a single call: `IBrighterBuilder.UseAsyncApi(options)`.

### R5: Package Structure

- R5.1: Package must target `net8.0`, `net9.0`, and `net10.0`.
- R5.2: Package must follow the same structure as `Paramore.Brighter.Extensions.OpenTelemetry`.

### R6: Sample

- R6.1: A sample project using RabbitMQ must demonstrate end-to-end usage.

### R7: Generator Correctness (Post-Review)

- R7.1: The document generator must be stateless across invocations — calling `GenerateAsync()` multiple times must produce identical documents. Internal state (channels, operations, messages) must be reset on each call.
- R7.2: Assembly scanning deduplication must match on channel address + type, not on operation ID string matching, to correctly handle all DI-registered publications regardless of unique-ified operation IDs.
- R7.3: Dead code branches (e.g. `#if NET8_0_OR_GREATER` where the else path is unreachable given target frameworks) should be removed or documented.

### R8: Defensive API Design (Post-Review)

- R8.1: The `AsyncApiDocument.Servers` dictionary must be defensively copied from options to prevent mutation of the document via the options reference after generation.
- R8.2: Channel ID sanitization regex should be compiled/cached to avoid per-call overhead (use `RegexOptions.Compiled` or `[GeneratedRegex]`).
- R8.3: The coupling between `IAmConsumerOptions` (a consumer configuration interface) and subscription discovery should be documented explicitly, noting that send-only apps without Service Activator registration will not have this service in DI.

## Non-Goals

- No YAML output (JSON only).
- No HTTP endpoint (file generation only).
- No built-in AsyncAPI Studio UI.
- No MSBuild task or dotnet global tool.
- No message filtering.
- No custom channel metadata (bindings, tags, externalDocs).
