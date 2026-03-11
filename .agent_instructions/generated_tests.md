# Generated Tests

Brighter uses a test generation tool to produce consistent test suites across all provider implementations for both **outbox** (MSSQL, PostgreSQL, MySQL, SQLite, DynamoDB, MongoDB, GCP Spanner/Firestore) and **messaging gateway** (RabbitMQ, Kafka, AWS SNS/SQS, etc.) tests. See [ADR 0035](../docs/adr/0035-generated-test.md) for the design rationale.

## Key Principle

**Never edit generated test files directly.** Always edit the Liquid templates, then regenerate. Generated files are overwritten each time the generator runs. Any hand-edits will be lost.

## Architecture

```
tools/Paramore.Brighter.Test.Generator/
├── Templates/
│   ├── Outbox/
│   │   ├── Sync/       ← Liquid templates for sync outbox tests
│   │   └── Async/      ← Liquid templates for async outbox tests
│   ├── MessagingGateway/
│   │   ├── Reactor/    ← Liquid templates for sync messaging gateway tests
│   │   └── Proactor/   ← Liquid templates for async messaging gateway tests
│   ├── MessageFactory.cs.liquid
│   └── DefaultMessageFactory.cs.liquid
├── Generators/
│   ├── BaseGenerator.cs
│   ├── OutboxGenerator.cs
│   ├── MessagingGatewayGenerator.cs
│   └── SharedGenerator.cs
├── Configuration/
│   ├── TestConfiguration.cs
│   ├── OutboxConfiguration.cs
│   └── MessagingGatewayConfiguration.cs
└── Program.cs

tests/Paramore.Brighter.*.Tests/
├── test-configuration.json      ← Per-provider configuration
├── Outbox/
│   └── [Prefix]/Generated/      ← Output directory (do not hand-edit)
│       ├── Sync/*.cs
│       └── Async/*.cs
└── MessagingGateway/
    └── [Prefix]/Generated/      ← Output directory (do not hand-edit)
        ├── Reactor/*.cs
        └── Proactor/*.cs
```

## Templates

Templates use [Liquid syntax](https://shopify.github.io/liquid/) (via the Fluid library).

### Outbox Template Variables

| Variable | Source | Description |
|---|---|---|
| `{{ Namespace }}` | `test-configuration.json` | Test project namespace |
| `{{ OutboxProvider }}` | `test-configuration.json` | Provider class name (e.g. `MSSQLTextOutboxProvider`) |
| `{{ MessageFactory }}` | `test-configuration.json` | Message factory class (defaults to `DefaultMessageFactory`) |
| `{{ Transaction }}` | `test-configuration.json` | Transaction type for the provider |
| `{{ Prefix }}` | Derived from outbox key | Namespace suffix (e.g. `.Text`, `.Binary`) |
| `{{ Category }}` | `test-configuration.json` | Optional xUnit `[Trait("Category", ...)]` value |

Templates that contain `Transaction` in the filename are skipped when `SupportsTransactions` is `false`.

### Messaging Gateway Template Variables

| Variable | Source | Description |
|---|---|---|
| `{{ Namespace }}` | `test-configuration.json` | Test project namespace |
| `{{ Prefix }}` | Derived from gateway key | Namespace suffix (e.g. `.Classic`, `.Quorum`) |
| `{{ Publication }}` | `test-configuration.json` | Publication type (e.g. `RmqPublication`) |
| `{{ Subscription }}` | `test-configuration.json` | Subscription type (e.g. `RmqSubscription`) |
| `{{ MessageGatewayProvider }}` | `test-configuration.json` | Provider implementation class |
| `{{ MessageBuilder }}` | `test-configuration.json` | Message builder class (defaults to `DefaultMessageBuilder`) |
| `{{ MessageAssertion }}` | `test-configuration.json` | Message assertion class (defaults to `DefaultMessageAssertion`) |
| `{{ Category }}` | `test-configuration.json` | Optional xUnit `[Trait("Category", ...)]` value |
| `{{ DelayBetweenReceiveMessageInMilliseconds }}` | `test-configuration.json` | Optional delay before reading messages |

Templates are skipped based on feature support flags (see [Feature Flags](#messaging-gateway-feature-flags) below).

## Configuration

Each test project has a `test-configuration.json`. Two forms are supported:

**Single outbox** (e.g. DynamoDB, MongoDB):

```json
{
  "Namespace": "Paramore.Brighter.DynamoDB.Tests",
  "Outbox": {
    "Transaction": "Amazon.DynamoDBv2.Model.TransactWriteItemsRequest",
    "OutboxProvider": "DynamoDBOutboxProvider"
  }
}
```

**Multiple outboxes** (e.g. MSSQL with Text/Binary, GCP with Firestore/Spanner):

```json
{
  "Namespace": "Paramore.Brighter.MSSQL.Tests",
  "Outboxes": {
    "Text": {
      "Transaction": "System.Data.Common.DbTransaction",
      "OutboxProvider": "MSSQLTextOutboxProvider"
    },
    "Binary": {
      "Transaction": "System.Data.Common.DbTransaction",
      "OutboxProvider": "MSSQLBinaryOutboxProvider"
    }
  }
}
```

### Messaging Gateway Configuration

**Single messaging gateway** (one queue type or transport):

```json
{
  "Namespace": "Paramore.Brighter.RMQ.Async.Tests",
  "MessageAssertion": "RmqMessageAssertion",
  "MessagingGateway": {
    "Publication": "Paramore.Brighter.MessagingGateway.RMQ.Async.RmqPublication",
    "Subscription": "Paramore.Brighter.MessagingGateway.RMQ.Async.RmqSubscription",
    "MessageGatewayProvider": "Paramore.Brighter.RMQ.Async.Tests.MessagingGateway.RmqMessageGatewayProvider",
    "Category": "RMQ",
    "DelayBetweenReceiveMessageInMilliseconds": 5000,
    "HasSupportToPublishConfirmation": true,
    "HasSupportToDeadLetterQueue": true,
    "HasSupportToDelayedMessages": true,
    "HasSupportToValidateBrokerExistence": true
  }
}
```

This generates tests into:

- `MessagingGateway/Generated/Reactor/*.cs`
- `MessagingGateway/Generated/Proactor/*.cs`

**Multiple messaging gateways** (e.g. RabbitMQ Classic vs Quorum queues):

```json
{
  "Namespace": "Paramore.Brighter.RMQ.Async.Tests",
  "MessageAssertion": "RmqMessageAssertion",
  "MessagingGateways": {
    "Classic": {
      "Publication": "Paramore.Brighter.MessagingGateway.RMQ.Async.RmqPublication",
      "Subscription": "Paramore.Brighter.MessagingGateway.RMQ.Async.RmqSubscription",
      "MessageGatewayProvider": "Paramore.Brighter.RMQ.Async.Tests.MessagingGateway.RmqClassicMessageGatewayProvider",
      "Category": "RMQ",
      "DelayBetweenReceiveMessageInMilliseconds": 5000,
      "HasSupportToPublishConfirmation": true,
      "HasSupportToDeadLetterQueue": true,
      "HasSupportToDelayedMessages": false,
      "HasSupportToValidateBrokerExistence": true
    },
    "Quorum": {
      "Publication": "Paramore.Brighter.MessagingGateway.RMQ.Async.RmqPublication",
      "Subscription": "Paramore.Brighter.MessagingGateway.RMQ.Async.RmqSubscription",
      "MessageGatewayProvider": "Paramore.Brighter.RMQ.Async.Tests.MessagingGateway.RmqQuorumMessageGatewayProvider",
      "Category": "RMQ",
      "DelayBetweenReceiveMessageInMilliseconds": 5000,
      "HasSupportToPublishConfirmation": true,
      "HasSupportToDeadLetterQueue": true,
      "HasSupportToDelayedMessages": false,
      "HasSupportToValidateBrokerExistence": true
    }
  }
}
```

Each key in `MessagingGateways` becomes a subfolder and namespace segment. This generates:

- `MessagingGateway/Classic/Generated/Reactor/*.cs` (namespace `...MessagingGateway.Classic.Reactor`)
- `MessagingGateway/Classic/Generated/Proactor/*.cs` (namespace `...MessagingGateway.Classic.Proactor`)
- `MessagingGateway/Quorum/Generated/Reactor/*.cs` (namespace `...MessagingGateway.Quorum.Reactor`)
- `MessagingGateway/Quorum/Generated/Proactor/*.cs` (namespace `...MessagingGateway.Quorum.Proactor`)

Each variant gets its own `IAmAMessageGatewayReactorProvider` and `IAmAMessageGatewayProactorProvider` interfaces in its namespace. The `MessageGatewayProvider` must implement both interfaces for its variant.

### Messaging Gateway Feature Flags

Feature flags control which test templates are generated. When a flag is `false`, templates matching specific filename patterns are skipped:

| Flag | Skips templates containing | Use case |
|---|---|---|
| `HasSupportToPublishConfirmation` | `confirming_posting` | Transport doesn't support publisher confirms |
| `HasSupportToDelayedMessages` | `delayed_message`, `with_delay` | No delayed/scheduled message support (e.g. quorum queues) |
| `HasSupportToPartitionKey` | `partition_key` | Transport doesn't support partition keys |
| `HasSupportToDeadLetterQueue` | `dead_letter_queue` | No dead letter queue support |
| `HasSupportToValidateBrokerExistence` | `no_broker_created` | Transport can't validate broker existence |

### Messaging Gateway Provider Pattern

Each variant needs a provider class that implements the generated interfaces. The provider encapsulates transport-specific setup:

```csharp
public class RmqClassicMessageGatewayProvider
    : Classic.Proactor.IAmAMessageGatewayProactorProvider,
      Classic.Reactor.IAmAMessageGatewayReactorProvider
{
    // Creates Publication, Subscription, Producer, Channel
    // Handles cleanup and dead letter queue access
}
```

Key provider responsibilities:

- **`CreateSubscription()`** — configure transport-specific options (e.g. `queueType: QueueType.Quorum`, `isDurable: true`)
- **`CreateProducer()` / `CreateProducerAsync()`** — create message producers, with special handling for validate-mode tests
- **`CreateChannel()` / `CreateChannelAsync()`** — create channels, optionally wrapping with requeue tracking for DLQ tests
- **`GetMessageFromDeadLetterQueue()`** — read from DLQ for retry limit tests

## When to Create or Update Generated Tests

Templates should be **created or updated** when a change affects behavior shared across all providers. Common scenarios include:

- **Adding a new test scenario** that all providers must satisfy (e.g. a new outbox operation, a new messaging gateway pattern)
- **Fixing a bug** in shared test logic that applies to every provider
- **Changing a shared interface or base class** that generated tests depend on
- **Adding a new provider** that needs the standard test suite — create a `test-configuration.json` and run the generator

After creating or updating templates, you **must regenerate** the tests. Use the convenience scripts at the repo root to regenerate all test projects at once:

```bash
# macOS/Linux
./generate-test.sh

# Windows
.\generate-test.ps1
```

Or follow the manual steps below to regenerate specific projects.

## How to Modify Generated Tests

### Step 1: Edit the Liquid templates

Templates are in:

- **Outbox:** `tools/Paramore.Brighter.Test.Generator/Templates/Outbox/Sync/` and `.../Async/`
- **Messaging Gateway:** `tools/Paramore.Brighter.Test.Generator/Templates/MessagingGateway/Reactor/` and `.../Proactor/`

Find the template that corresponds to the test you want to change.

### Step 2: Build the generator

```bash
dotnet build tools/Paramore.Brighter.Test.Generator
```

The build copies templates to the `bin/` output directory. Without rebuilding, the generator uses stale cached templates.

### Step 3: Regenerate from each test project directory

The generator uses the current working directory as the output root. You **must** run it from the test project directory:

```bash
# Correct - generates into the test project
cd tests/Paramore.Brighter.MSSQL.Tests
dotnet run --no-build --project ../../tools/Paramore.Brighter.Test.Generator

# Wrong - generates into the repo root
dotnet run --project tools/Paramore.Brighter.Test.Generator -- --file tests/Paramore.Brighter.MSSQL.Tests/test-configuration.json
```

### Step 4: Regenerate ALL test projects

Template changes affect all providers. Regenerate every project that has a `test-configuration.json`:

```bash
dotnet build tools/Paramore.Brighter.Test.Generator

for testdir in \
  tests/Paramore.Brighter.DynamoDB.Tests \
  tests/Paramore.Brighter.DynamoDB.V4.Tests \
  tests/Paramore.Brighter.Gcp.Tests \
  tests/Paramore.Brighter.MSSQL.Tests \
  tests/Paramore.Brighter.MongoDb.Tests \
  tests/Paramore.Brighter.MySQL.Tests \
  tests/Paramore.Brighter.PostgresSQL.Tests \
  tests/Paramore.Brighter.Sqlite.Tests \
  tests/Paramore.Brighter.RMQ.Async.Tests \
  tests/Paramore.Brighter.RMQ.Sync.Tests; do
  (cd "$testdir" && dotnet run --no-build --project ../../tools/Paramore.Brighter.Test.Generator)
done
```

### Step 5: Also check the base test classes

Some test logic lives in non-generated base classes in `tests/Paramore.Brighter.Base.Test/Outbox/`:

- `OutboxTest.cs` - base class for sync outbox tests
- `OutboxAsyncTest.cs` - base class for async outbox tests

If you change a template pattern that also exists in these base classes, update them too.

### Important: Generator Does Not Delete Stale Files

The generator only creates or overwrites files — it **never deletes** existing generated files. If you change a feature flag from `true` to `false` (e.g. disabling `HasSupportToDelayedMessages`), you must **manually delete** the previously-generated test files that are no longer wanted. Otherwise stale tests will remain and may fail.

## CI Flakiness Guidelines

When writing or modifying test templates, avoid patterns that cause CI flakiness:

- **Timestamp comparisons**: Use `Assert.Equal(expected, actual, TimeSpan.FromSeconds(1))` tolerance instead of string formatting. Database datetime precision varies across providers.
- **Age filter tests**: Use explicit past timestamps (e.g. `DateTime.UtcNow.AddSeconds(-30)`) instead of relying on `DateTime.UtcNow` for recently-dispatched messages. This avoids races between .NET and database clocks.
- **Kafka retry counts**: Use `maxTries <= 10` (not `<= 3`) for consumer read loops. Consumer group rebalancing in CI can take 5-10+ seconds.
- **SQLite concurrent access**: The SQLite tests use WAL journal mode to handle parallel test execution against a shared `test.db` file.
