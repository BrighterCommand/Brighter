# 37. Add Messaging Gateway Generated Tests

Date: 2026-01-29

## Status

Accepted

## Context

The Brighter project uses a test generator tool (`Paramore.Brighter.Test.Generator`) to create standardized tests for different components like Outbox implementations. This reduces code duplication and ensures consistent test coverage across different messaging gateway implementations (RabbitMQ, AWS SNS/SQS, Azure Service Bus, etc.).

Previously, the test generator only supported Outbox tests. Each messaging gateway implementation had to manually write similar test scenarios repeatedly, leading to:

- Code duplication across different gateway implementations
- Inconsistent test coverage
- Increased maintenance burden when patterns change
- Difficulty ensuring all gateway implementations are tested consistently

## Decision

We have extended the test generator tool to support generating messaging gateway tests based on existing test patterns from `Paramore.Brighter.RMQ.Async.Tests`. This includes:

### 1. New Configuration Support

Added `MessagingGatewayConfiguration` class to configure gateway test generation:

- `Prefix`: Test class name prefix
- `Namespace`: Target namespace for generated tests
- `MessageFactory`: Factory for creating test messages
- `MessageAssertion`: Assertion helpers for validating messages
- `MessageGatewayProvider`: Provider implementation for the specific gateway
- `Category`: Test category for filtering
- `Publication`/`Subscription`: Gateway-specific configuration
- `DelayBetweenReceiveMessageInMilliseconds`: Timing configuration for async operations

Added configuration support in `TestConfiguration`:

- `MessageAssertion`: Default assertion helper (defaults to `DefaultMessageAssertion`)
- `MessagingGateway`: Single gateway configuration
- `MessagingGatewaies`: Dictionary of named gateway configurations

### 2. New Generator Implementation

Created `MessagingGatewayGenerator` that:

- Generates test provider interfaces and implementations
- Supports both Reactor (sync) and Proactor (async) test patterns
- Generates tests from Liquid templates
- Follows the same pattern as `OutboxGenerator` for consistency

### 3. Updated Message Factories and Assertions

Refactored message creation pattern:

- Replaced timestamp-based factory with configuration-based factory
- Added `MessageConfiguration` to provide complete control over test message properties
- Created `DefaultMessageAssertion` for validating message properties
- Added `IAmAMessageAssertion` interface for custom assertions
- Factory now tracks created messages via `CreatedMessages` property

### 4. Template Structure

Added templates for messaging gateway tests:

- `MessagingGateway/Proactor/`: Async (Proactor pattern) test templates
- `MessagingGateway/Reactor/`: Sync (Reactor pattern) test templates
- `IAmAMessageGatewayProactorProvider.cs.liquid`: Provider interface for async tests
- `DefaultMessageAssertion.cs.liquid`: Default assertion implementation
- `IAmAMessageAssertion.cs.liquid`: Assertion interface

### 5. Integration into Build Process

Updated `Program.cs` to call `MessageGatewayGenerator` alongside existing generators:

```csharp
await new MessageGatewayGenerator(factory.CreateLogger<MessageGatewayGenerator>()).GenerateAsync(configuration);
```

### 6. Example Generated Test Structure

For RabbitMQ:

```
tests/Paramore.Brighter.RMQ.Async.Tests/
├── MessagingGateway/
│   ├── RmqMessageGatewayProvider.cs (manually written)
│   ├── Proactor/ (existing manual tests)
│   ├── Reactor/ (existing manual tests)
│   └── Generated/
│       └── Proactor/
│           ├── IAmAMessageGatewayProactorProvider.cs (generated)
│           └── When_a_message_consumer_reads_multiple_messages_should_receive_all_messages.cs (generated)
├── DefaultMessageFactory.cs (generated, shared)
├── DefaultMessageAssertion.cs (generated, shared)
└── test-configuration.json (configuration file)
```

### 7. Additional Changes

- Updated target framework from .NET 9 to .NET 10 for tests and tools
- Added `.csproj` entries to copy new template folders to output directory

## Consequences

### Positive

- **Reduced Duplication**: Gateway implementers write one provider class instead of dozens of test classes
- **Consistency**: All gateway implementations follow the same test patterns
- **Maintainability**: Changes to test patterns only need to be made in templates
- **Coverage**: Easy to ensure all gateways have the same test coverage
- **Scalability**: Adding new test scenarios only requires updating templates
- **Flexibility**: Supports both single and multiple gateway configurations

### Negative

- **Learning Curve**: Developers need to understand the generator and template system
- **Debugging**: Generated test failures require understanding both template and generated code
- **Template Complexity**: Liquid templates can be harder to write than direct C# code
- **Build Dependency**: Test generation must run before tests can execute

### Neutral

- Generated tests are marked with `<auto-generated>` comments
- Manual tests continue to exist alongside generated tests as examples and edge cases
- The generator follows established patterns from Outbox generation

## Implementation Notes

The generator creates tests based on configuration in `test-configuration.json`:

```json
{
  "namespace": "Paramore.Brighter.RMQ.Async.Tests",
  "destinationFolder": "tests/Paramore.Brighter.RMQ.Async.Tests",
  "messageFactory": "DefaultMessageFactory",
  "messageAssertion": "DefaultMessageAssertion",
  "messagingGateway": {
    "prefix": "Rmq",
    "messageGatewayProvider": "Paramore.Brighter.RMQ.Async.Tests.MessagingGateway.RmqMessageGatewayProvider",
    "category": "RabbitMQ",
    "publication": "Paramore.Brighter.MessagingGateway.RMQ.Async.RmqPublication",
    "subscription": "Paramore.Brighter.MessagingGateway.RMQ.Async.RmqSubscription",
    "delayBetweenReceiveMessageInMilliseconds": 1000
  }
}
```

Gateway implementers only need to:

1. Create a `test-configuration.json` file
2. Implement a provider class (e.g., `RmqMessageGatewayProvider`)
3. Run the generator tool
4. Generated tests automatically cover common scenarios

## References

- Existing Pattern: `OutboxGenerator` and Outbox test templates
- Source Tests: `tests/Paramore.Brighter.RMQ.Async.Tests/MessagingGateway/`
- Generator Tool: `tools/Paramore.Brighter.Test.Generator/`
