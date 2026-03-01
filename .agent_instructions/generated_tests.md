# Generated Tests

Brighter uses a test generation tool to produce consistent outbox test suites across all provider implementations (MSSQL, PostgreSQL, MySQL, SQLite, DynamoDB, MongoDB, GCP Spanner/Firestore). See [ADR 0035](../docs/adr/0035-generated-test.md) for the design rationale.

## Key Principle

**Never edit generated test files directly.** Always edit the Liquid templates, then regenerate. Generated files are overwritten each time the generator runs. Any hand-edits will be lost.

## Architecture

```
tools/Paramore.Brighter.Test.Generator/
├── Templates/
│   ├── Outbox/
│   │   ├── Sync/       ← Liquid templates for sync outbox tests
│   │   └── Async/      ← Liquid templates for async outbox tests
│   ├── MessageFactory.cs.liquid
│   └── DefaultMessageFactory.cs.liquid
├── Generators/
│   ├── BaseGenerator.cs
│   ├── OutboxGenerator.cs
│   └── SharedGenerator.cs
├── Configuration/
│   ├── TestConfiguration.cs
│   └── OutboxConfiguration.cs
└── Program.cs

tests/Paramore.Brighter.*.Tests/
├── test-configuration.json      ← Per-provider configuration
└── Outbox/
    └── [Prefix]/Generated/      ← Output directory (do not hand-edit)
        ├── Sync/*.cs
        └── Async/*.cs
```

## Templates

Templates use [Liquid syntax](https://shopify.github.io/liquid/) (via the Fluid library) with these variables:

| Variable | Source | Description |
|---|---|---|
| `{{ Namespace }}` | `test-configuration.json` | Test project namespace |
| `{{ OutboxProvider }}` | `test-configuration.json` | Provider class name (e.g. `MSSQLTextOutboxProvider`) |
| `{{ MessageFactory }}` | `test-configuration.json` | Message factory class (defaults to `DefaultMessageFactory`) |
| `{{ Transaction }}` | `test-configuration.json` | Transaction type for the provider |
| `{{ Prefix }}` | Derived from outbox key | Namespace suffix (e.g. `.Text`, `.Binary`) |
| `{{ Category }}` | `test-configuration.json` | Optional xUnit `[Trait("Category", ...)]` value |

Templates that contain `Transaction` in the filename are skipped when `SupportsTransactions` is `false`.

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

## How to Modify Generated Tests

### Step 1: Edit the Liquid templates

Templates are in `tools/Paramore.Brighter.Test.Generator/Templates/Outbox/Sync/` and `.../Async/`. Find the template that corresponds to the test you want to change.

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
  tests/Paramore.Brighter.Sqlite.Tests; do
  (cd "$testdir" && dotnet run --no-build --project ../../tools/Paramore.Brighter.Test.Generator)
done
```

### Step 5: Also check the base test classes

Some test logic lives in non-generated base classes in `tests/Paramore.Brighter.Base.Test/Outbox/`:
- `OutboxTest.cs` - base class for sync outbox tests
- `OutboxAsyncTest.cs` - base class for async outbox tests

If you change a template pattern that also exists in these base classes, update them too.

## CI Flakiness Guidelines

When writing or modifying outbox test templates, avoid patterns that cause CI flakiness:

- **Timestamp comparisons**: Use `Assert.Equal(expected, actual, TimeSpan.FromSeconds(1))` tolerance instead of string formatting. Database datetime precision varies across providers.
- **Age filter tests**: Use explicit past timestamps (e.g. `DateTime.UtcNow.AddSeconds(-30)`) instead of relying on `DateTime.UtcNow` for recently-dispatched messages. This avoids races between .NET and database clocks.
- **Kafka retry counts**: Use `maxTries <= 10` (not `<= 3`) for consumer read loops. Consumer group rebalancing in CI can take 5-10+ seconds.
- **SQLite concurrent access**: The SQLite tests use WAL journal mode to handle parallel test execution against a shared `test.db` file.
