# Generated Tests

Brighter uses a test generation tool to produce consistent test suites across all provider implementations for both **outbox** (MSSQL, PostgreSQL, MySQL, SQLite, DynamoDB, MongoDB, GCP Spanner/Firestore) and **messaging gateway** (RabbitMQ, Kafka, AWS SNS/SQS, NATS, Redis, RocketMQ, GCP Pub/Sub, PostgreSQL, MSSQL) tests. See [ADR 0035](../docs/adr/0035-generated-test.md) for the design rationale.

## Key Principle

**Never edit generated test files directly.** Always edit the Liquid templates, then regenerate. Generated files are overwritten each time the generator runs. Any hand-edits will be lost.

## Architecture

```
tools/Paramore.Brighter.Test.Generator/
├── Templates/
│   ├── DefaultMessageBuilder.cs.liquid     ← Default message builder implementation
│   ├── DefaultMessageAssertion.cs.liquid   ← Default message assertion implementation
│   ├── IAmAMessageBuilder.cs.liquid        ← Message builder interface
│   ├── IAmAMessageAssertion.cs.liquid      ← Message assertion interface
│   ├── Outbox/
│   │   ├── Sync/       ← 13 Liquid templates for sync outbox tests (12 tests + 1 interface)
│   │   └── Async/      ← 13 Liquid templates for async outbox tests (12 tests + 1 interface)
│   └── MessagingGateway/
│       ├── Reactor/    ← 13 Liquid templates for sync messaging gateway tests (12 tests + 1 interface)
│       └── Proactor/   ← 13 Liquid templates for async messaging gateway tests (12 tests + 1 interface)
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

### Projects Using the Generator

| Project | Outbox | Messaging Gateway | Variants |
|---|---|---|---|
| `Paramore.Brighter.MySQL.Tests` | ✅ | | Text, Binary |
| `Paramore.Brighter.PostgresSQL.Tests` | ✅ | ✅ | Text, Binary (outbox); single (gateway) |
| `Paramore.Brighter.MSSQL.Tests` | ✅ | ✅ | Text, Binary (outbox); single (gateway) |
| `Paramore.Brighter.Sqlite.Tests` | ✅ | | Text, Binary |
| `Paramore.Brighter.DynamoDB.Tests` | ✅ | | single |
| `Paramore.Brighter.DynamoDB.V4.Tests` | ✅ | | single |
| `Paramore.Brighter.MongoDb.Tests` | ✅ | | single |
| `Paramore.Brighter.Gcp.Tests` | ✅ | ✅ | Firestore, SpannerBinary, SpannerText (outbox); Pull, PullOrdering, Stream, StreamOrdering (gateway) |
| `Paramore.Brighter.RMQ.Async.Tests` | | ✅ | Classic, Quorum |
| `Paramore.Brighter.AWS.Tests` | | ✅ | SnsStandard, SnsFifo, SqsStandard, SqsFifo |
| `Paramore.Brighter.AWS.V4.Tests` | | ✅ | SnsStandard, SnsFifo, SqsStandard, SqsFifo |
| `Paramore.Brighter.Kafka.Tests` | | ✅ | Standard, PartitionKey |
| `Paramore.Brighter.NATS.Tests` | | ✅ | single |
| `Paramore.Brighter.Redis.Tests` | | ✅ | single |
| `Paramore.Brighter.RocketMQ.Tests` | | ✅ | single |

## Templates

Templates use [Liquid syntax](https://shopify.github.io/liquid/) (via the Fluid library). There are 56 Liquid templates total.

### Shared Templates (Root Level)

Generated into the test project root. These provide common helpers used by both outbox and messaging gateway tests:

- `DefaultMessageBuilder.cs.liquid` — Default `IAmAMessageBuilder` implementation that builds test messages with randomized properties
- `DefaultMessageAssertion.cs.liquid` — Default `IAmAMessageAssertion` implementation for validating received messages
- `IAmAMessageBuilder.cs.liquid` — Interface for building test messages (supports topic, partition key, correlation ID, etc.)
- `IAmAMessageAssertion.cs.liquid` — Interface for asserting message properties after receive

### Outbox Templates

**Sync** (13 templates) and **Async** (13 templates) — each contains 1 provider interface + 12 test scenarios:

| Template | Description |
|---|---|
| `IAmAnOutboxProviderSync/Async` | Provider interface for managing outbox storage |
| `When_Adding_A_Message_It_Should_Be_Stored_With_All_Properties` | Verifies all message properties survive round-trip |
| `When_Adding_A_Duplicate_Message_It_Should_Not_Throw` | Idempotent add behavior |
| `When_Adding_A_Message_Within_Transaction_It_Should_Be_Stored_After_Commit` | Transaction commit behavior |
| `When_Adding_A_Message_Within_Transaction_And_Rollback_It_Should_Not_Be_Stored` | Transaction rollback behavior |
| `When_Deleting_One_Message_It_Should_Be_Removed_From_Outbox` | Single message deletion |
| `When_Deleting_Multiple_Messages_They_Should_Be_Removed_From_Outbox` | Batch message deletion |
| `When_Retrieving_A_Message_By_Id_It_Should_Return_The_Correct_Message` | Lookup by ID |
| `When_Retrieving_Messages_By_Ids_It_Should_Return_Only_Requested_Messages` | Batch lookup by IDs |
| `When_Retrieving_A_Non_Existent_Message_It_Should_Return_Empty_Message` | Missing message handling |
| `When_Retrieving_All_Messages_They_Should_Include_Dispatched_And_Undispatched` | Full message listing |
| `When_Retrieving_Outstanding_Messages_It_Should_Filter_By_Age` | Outstanding message age filtering |
| `When_Retrieving_Dispatched_Messages_It_Should_Filter_By_Age` | Dispatched message age filtering |

Templates containing `Transaction` in the filename are skipped when `SupportsTransactions` is `false`.

#### Outbox Template Variables

| Variable | Source | Description |
|---|---|---|
| `{{ Namespace }}` | `test-configuration.json` | Test project namespace |
| `{{ OutboxProvider }}` | `test-configuration.json` | Provider class name (e.g. `MSSQLTextOutboxProvider`) |
| `{{ MessageBuilder }}` | `test-configuration.json` | Message builder class (defaults to `DefaultMessageBuilder`) |
| `{{ Transaction }}` | `test-configuration.json` | Transaction type for the provider |
| `{{ Prefix }}` | Derived from outbox key | Namespace suffix (e.g. `.Text`, `.Binary`) |
| `{{ Category }}` | `test-configuration.json` | Optional xUnit `[Trait("Category", ...)]` value |
| `{{ CollectionName }}` | `test-configuration.json` | Optional xUnit `[Collection(...)]` value |

### Messaging Gateway Templates

**Reactor** (13 templates) and **Proactor** (13 templates) — each contains 1 provider interface + 12 test scenarios:

| Template | Description |
|---|---|
| `IAmAMessageGatewayReactorProvider/ProactorProvider` | Provider interface for creating gateway components |
| `When_posting_a_message_via_the_messaging_gateway_should_be_received` | Basic send/receive round-trip |
| `When_posting_a_message_but_no_broker_created_should_throw_exception` | Missing broker error |
| `When_multiple_threads_try_to_post_a_message_at_the_same_time_should_not_throw_exception` | Thread-safety |
| `When_a_message_consumer_reads_multiple_messages_should_receive_all_messages` | Batch receive |
| `When_sending_a_message_should_propagate_activity_context` | OpenTelemetry context propagation |
| `When_confirming_posting_a_message_should_receive_publish_confirmation` | Publisher confirms |
| `When_requeuing_a_failed_message_should_receive_message_again` | Basic requeue |
| `When_requeuing_a_failed_message_with_delay_should_receive_message_again` | Delayed requeue |
| `When_requeuing_a_message_too_many_times_should_move_to_dead_letter_queue` | DLQ redrive |
| `When_infrastructure_missing_and_assume_channel_should_throw_exception` | Assume mode error |
| `When_infrastructure_missing_and_validate_channel_should_throw_exception` | Validate mode error |
| `When_reading_a_delayed_message_via_the_messaging_gateway_should_delay_delivery` | Scheduled delivery |

Templates are skipped based on feature support flags (see [Feature Flags](#messaging-gateway-feature-flags) below).

#### Messaging Gateway Template Variables

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
| `{{ CollectionName }}` | `test-configuration.json` | Optional xUnit `[Collection(...)]` value |
| `{{ ReceiveMessageTimeoutInMilliseconds }}` | `test-configuration.json` | Timeout for receive operations (default: `300`ms) |
| `{{ DelayBetweenReceiveMessageInMilliseconds }}` | `test-configuration.json` | Optional delay before reading messages |

## Configuration

Each test project has a `test-configuration.json`. Two forms are supported:

**Single outbox** (e.g. DynamoDB, MongoDB):

```json
{
  "Namespace": "Paramore.Brighter.DynamoDB.Tests",
  "Outbox": {
    "Transaction": "Amazon.DynamoDBv2.Model.TransactWriteItemsRequest",
    "OutboxProvider": "DynamoDBOutboxProvider",
    "CollectionName": "DynamoDBOutbox"
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
      "OutboxProvider": "MSSQLTextOutboxProvider",
      "Category": "MSSQL",
      "CollectionName": "MSSQLTextOutbox"
    },
    "Binary": {
      "Transaction": "System.Data.Common.DbTransaction",
      "OutboxProvider": "MSSQLBinaryOutboxProvider",
      "Category": "MSSQL",
      "CollectionName": "MSSQLBinaryOutbox"
    }
  }
}
```

To disable transaction tests for providers that don't support them (e.g. MongoDB):

```json
{
  "Outbox": {
    "SupportsTransactions": false
  }
}
```

### Messaging Gateway Configuration

**Single messaging gateway** (one transport type):

```json
{
  "Namespace": "Paramore.Brighter.Redis.Tests",
  "MessagingGateway": {
    "Prefix": "Redis",
    "Publication": "Paramore.Brighter.MessagingGateway.Redis.RedisPublication",
    "Subscription": "Paramore.Brighter.MessagingGateway.Redis.RedisSubscription",
    "MessageGatewayProvider": "Paramore.Brighter.Redis.Tests.MessagingGateway.RedisMessageGatewayProvider",
    "Category": "Redis",
    "CollectionName": "RedisMessagingGateway",
    "HasSupportToDeadLetterQueue": true,
    "HasSupportToRequeue": true,
    "HasSupportToValidateInfrastructure": false
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
      "CollectionName": "Classic",
      "ReceiveMessageTimeoutInMilliseconds": 4000,
      "HasSupportToPublishConfirmation": true,
      "HasSupportToDeadLetterQueue": true,
      "HasSupportToDelayedMessages": false,
      "HasSupportToValidateBrokerExistence": true,
      "HasSupportToRequeue": true
    },
    "Quorum": {
      "Publication": "Paramore.Brighter.MessagingGateway.RMQ.Async.RmqPublication",
      "Subscription": "Paramore.Brighter.MessagingGateway.RMQ.Async.RmqSubscription",
      "MessageGatewayProvider": "Paramore.Brighter.RMQ.Async.Tests.MessagingGateway.RmqQuorumMessageGatewayProvider",
      "Category": "RMQ",
      "CollectionName": "Quorum",
      "ReceiveMessageTimeoutInMilliseconds": 4000,
      "HasSupportToPublishConfirmation": true,
      "HasSupportToDeadLetterQueue": true,
      "HasSupportToDelayedMessages": false,
      "HasSupportToValidateBrokerExistence": true,
      "HasSupportToRequeue": true
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

Root-level `MessageBuilder` and `MessageAssertion` in the configuration serve as defaults. Each outbox or gateway entry can override them with its own values. Use custom message builders (e.g. `FifoMessageBuilder`) when the transport requires specific message properties like partition keys or ordering group IDs.

### Messaging Gateway Feature Flags

Feature flags control which test templates are generated. When a flag is `false`, templates matching specific filename patterns are skipped:

| Flag | Default | Skips templates containing | Use case |
|---|---|---|---|
| `HasSupportToPublishConfirmation` | `false` | `confirming_posting` | Transport doesn't support publisher confirms |
| `HasSupportToDelayedMessages` | `false` | `delayed_message`, `with_delay` | No delayed/scheduled message support |
| `HasSupportToDeadLetterQueue` | `false` | `dead_letter_queue` | No dead letter queue support |
| `HasSupportToValidateBrokerExistence` | `false` | `no_broker_created` | Transport can't validate broker existence |
| `HasSupportToRequeue` | `false` | `requeuing` | Transport doesn't support message requeue |
| `HasSupportToValidateInfrastructure` | **`true`** | `assume_channel`, `validate_channel` | Transport can't validate infrastructure existence |

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

- **`GetOrCreateRoutingKey()` / `GetOrCreateChannelName()`** — generate unique routing keys and channel names per test (uses `[CallerMemberName]`)
- **`CreatePublication()`** — create a publication with transport-specific options
- **`CreateSubscription()`** — configure transport-specific options (e.g. `queueType: QueueType.Quorum`, `isDurable: true`)
- **`CreateProducer()` / `CreateProducerAsync()`** — create message producers, with special handling for validate-mode tests
- **`CreateChannel()` / `CreateChannelAsync()`** — create channels, optionally wrapping with requeue tracking for DLQ tests
- **`GetMessageFromDeadLetterQueue()` / `GetMessageFromDeadLetterQueueAsync()`** — read from DLQ for retry limit tests
- **`CleanUp()` / `CleanUpAsync()`** — dispose producers, channels, and clean up sent messages

### Outbox Provider Pattern

Each outbox variant needs a provider class implementing the generated sync/async interfaces:

```csharp
public class MSSQLTextOutboxProvider
    : Outbox.Text.Generated.Sync.IAmAnOutboxProviderSync,
      Outbox.Text.Generated.Async.IAmAnOutboxProviderAsync
{
    // Creates/deletes data store, creates outbox instances, manages transactions
}
```

Key provider responsibilities:

- **`CreateStore()` / `CreateStoreAsync()`** — initialize the outbox data store (create tables, etc.)
- **`DeleteStore()` / `DeleteStoreAsync()`** — tear down the store and clean up test messages
- **`CreateOutbox()` / `CreateOutboxAsync()`** — create an outbox instance for the provider
- **`CreateTransactionProvider()`** — create a transaction provider for transaction tests
- **`GetAllMessages()` / `GetAllMessagesAsync()`** — retrieve all stored messages for assertions

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
  tests/Paramore.Brighter.AWS.Tests \
  tests/Paramore.Brighter.AWS.V4.Tests \
  tests/Paramore.Brighter.DynamoDB.Tests \
  tests/Paramore.Brighter.DynamoDB.V4.Tests \
  tests/Paramore.Brighter.Gcp.Tests \
  tests/Paramore.Brighter.Kafka.Tests \
  tests/Paramore.Brighter.MSSQL.Tests \
  tests/Paramore.Brighter.MongoDb.Tests \
  tests/Paramore.Brighter.MySQL.Tests \
  tests/Paramore.Brighter.PostgresSQL.Tests \
  tests/Paramore.Brighter.Redis.Tests \
  tests/Paramore.Brighter.RMQ.Async.Tests \
  tests/Paramore.Brighter.RocketMQ.Tests \
  tests/Paramore.Brighter.Sqlite.Tests; do
  (cd "$testdir" && dotnet run --no-build --project ../../tools/Paramore.Brighter.Test.Generator)
done
```

Or use the convenience scripts which iterate **all** subdirectories under `tests/` (projects without a `test-configuration.json` are silently skipped):

```bash
./generate-test.sh      # macOS/Linux
.\generate-test.ps1     # Windows
```

### Step 5: Also check the base test classes

Some test logic lives in non-generated base classes in `tests/Paramore.Brighter.Base.Test/Outbox/`:

- `OutboxTest.cs` — base class for sync outbox tests
- `OutboxAsyncTest.cs` — base class for async outbox tests
- `RelationDatabaseOutboxTest.cs` — base class for relational database sync outbox tests
- `RelationDatabaseOutboxAsyncTest.cs` — base class for relational database async outbox tests

If you change a template pattern that also exists in these base classes, update them too.

### Important: Generator Does Not Delete Stale Files

The generator only creates or overwrites files — it **never deletes** existing generated files. If you change a feature flag from `true` to `false` (e.g. disabling `HasSupportToDelayedMessages`), you must **manually delete** the previously-generated test files that are no longer wanted. Otherwise stale tests will remain and may fail.

Similarly, if you rename or remove a template, the old generated files remain on disk. Always check for stale files after template changes.

## CI Flakiness Guidelines

When writing or modifying test templates, avoid patterns that cause CI flakiness:

- **Timestamp comparisons**: Use `Assert.Equal(expected, actual, TimeSpan.FromSeconds(1))` tolerance instead of string formatting. Database datetime precision varies across providers.
- **Age filter tests**: Use explicit past timestamps (e.g. `DateTime.UtcNow.AddSeconds(-30)`) instead of relying on `DateTime.UtcNow` for recently-dispatched messages. This avoids races between .NET and database clocks.
- **Receive timeouts**: Set `ReceiveMessageTimeoutInMilliseconds` appropriately for the transport. Fast transports (MSSQL, PostgreSQL) can use the default 300ms; broker-based transports need higher values (RMQ: 4000ms, AWS: 4000ms, Kafka: 15000ms, GCP: 10000ms).
- **Kafka retry counts**: Use `maxTries <= 10` (not `<= 3`) for consumer read loops. Consumer group rebalancing in CI can take 5-10+ seconds.
- **SQLite concurrent access**: The SQLite tests use WAL journal mode to handle parallel test execution against a shared `test.db` file.
- **Collection attributes**: Use `CollectionName` in configuration to control xUnit test collection grouping. This prevents parallel execution conflicts for tests sharing the same transport resources.
