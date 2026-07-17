# Generating a Conformance Suite for an Outbox

This page covers generating the test suite for `Paramore.Brighter.Outbox.X`. Read the [overview](./README.md) first for how to invoke the generator and the rules about generated code.

The generator covers outboxes only; **Inbox templates do not exist yet**. Inbox generation is in progress in [PR #4230](https://github.com/BrighterCommand/Brighter/pull/4230). Until it lands, you MUST hand-write an Inbox's tests.

## The Workflow

1. Add `test-configuration.json` to `tests/Paramore.Brighter.X.Tests/`.
2. Run the generator from inside that directory.
3. Implement the generated provider interface.
4. Run the suite against real infrastructure until it is green.

## Step 1 — Write the Configuration

Prefer the **plural `Outboxes`** form, even for a single outbox. See [Why plural](#why-you-should-prefer-outboxes-even-for-one-outbox) below — the singular form has a defect.

A worked example, [`tests/Paramore.Brighter.MySQL.Tests/test-configuration.json`](../../../tests/Paramore.Brighter.MySQL.Tests/test-configuration.json), which tests two variants of the same store (text and binary body storage):

```json
{
  "Namespace": "Paramore.Brighter.MySQL.Tests",
  "Outboxes": {
    "Text": {
      "Transaction": "System.Data.Common.DbTransaction",
      "OutboxProvider": "MySQLTextOutboxProvider",
      "Category": "MySQL",
      "CollectionName": "MySQLTextOutbox"
    },
    "Binary": {
      "Transaction": "System.Data.Common.DbTransaction",
      "OutboxProvider": "MySQLBinaryOutboxProvider",
      "Category": "MySQL",
      "CollectionName": "MySQLBinaryOutbox"
    }
  }
}
```

Each key (`Text`, `Binary`) names a variant. It drives both the output folder and the namespace segment, so it MUST be a valid C# identifier.

### `OutboxConfiguration` schema

Defined in [`Configuration/OutboxConfiguration.cs`](../../../tools/Paramore.Brighter.Test.Generator/Configuration/OutboxConfiguration.cs).

| Property | Type | Default | Notes |
|---|---|---|---|
| `Transaction` | string | `""` | **Required.** The transaction type your outbox is generic over, e.g. `System.Data.Common.DbTransaction`. |
| `OutboxProvider` | string | `""` | **Required.** The type *you* write, implementing the generated provider interface. |
| `Prefix` | string | `""` | Falls back to the dictionary key. Set it only if the namespace segment must differ from the key. |
| `Namespace` | string? | inherits root | |
| `MessageBuilder` | string? | inherits root | |
| `SupportsTransactions` | bool | **`true`** | Set `false` for a store with no transaction support. |
| `Category` | string? | `null` | Renders an xUnit `[Trait("Category", ...)]`. |
| `CollectionName` | string? | `null` | Renders `[Collection(...)]`. **Async tests only — see the caveat below.** |

### `SupportsTransactions`

Leaving this at its `true` default means you MUST implement `CreateTransactionProvider()` meaningfully and pass the transactional tests. Set it to `false` for a store that cannot honour them (a document store without multi-document transactions, say), and the generator skips the two transaction tests.

Skipping works by **substring match on the file path**, from [`OutboxGenerator.cs`](../../../tools/Paramore.Brighter.Test.Generator/Generators/OutboxGenerator.cs):

```csharp
if (!outboxConfiguration.SupportsTransactions
    && fileName.Contains("Transaction", StringComparison.InvariantCultureIgnoreCase))
{
    return true;
}
```

Because it matches the whole path case-insensitively, a `SupportsTransactions: false` config whose *output path* contains "transaction" — a variant keyed `Transactional`, for instance — would skip every test. Avoid "transaction" in your keys.

## Step 2 — Generate

```bash
cd tests/Paramore.Brighter.MySQL.Tests
dotnet run --project ../../tools/Paramore.Brighter.Test.Generator
```

Output, per variant:

```
Outbox/<Key>/Generated/Sync/     # 12 tests + IAmAnOutboxProviderSync.cs
Outbox/<Key>/Generated/Async/    # 12 tests + IAmAnOutboxProviderAsync.cs
```

with namespace `<Namespace>.Outbox.<Key>.Sync` / `.Async`. The four [shared contract files](./README.md#the-shared-contracts) land in the project root.

## Step 3 — Implement the Provider

This is the only code you write. The generator emits the interface; you supply the wrapper that knows how to stand up your store.

`IAmAnOutboxProviderAsync` (the `Sync` variant mirrors it without `Task`):

```csharp
public interface IAmAnOutboxProviderAsync
{
    Task CreateStoreAsync();
    Task DeleteStoreAsync(IEnumerable<Message> messages);
    IAmAnOutboxAsync<Message, DbTransaction> CreateOutboxAsync();
    Task<IEnumerable<Message>> GetAllMessagesAsync();
    IAmABoxTransactionProvider<DbTransaction> CreateTransactionProvider();
}
```

Note what each is for:

- `CreateStoreAsync` / `DeleteStoreAsync` — per-test setup and teardown. `DeleteStoreAsync` receives the messages the test created so you can clean up precisely.
- `CreateOutboxAsync` — hand back your `IAmAnOutboxAsync` implementation. This is the thing under test.
- `GetAllMessagesAsync` — read messages back **out of band**, i.e. not via the Outbox interface. This is what lets the tests verify a message truly landed in the store rather than trusting the code under test to report on itself.
- `CreateTransactionProvider` — only meaningful when `SupportsTransactions` is `true`.

You MUST implement it once per variant, named to match `OutboxProvider` in the config, and it MUST live in the namespace the generated tests occupy (or be reachable from it). Worked examples: [`SpannerTextOutboxProvider`](../../../tests/Paramore.Brighter.Gcp.Tests/Outbox/SpannerText/SpannerTextOutboxProvider.cs) and [`FirestoreOutboxProvider`](../../../tests/Paramore.Brighter.Gcp.Tests/Outbox/Firestore/FirestoreOutboxProvider.cs).

## Step 4 — Run

```bash
docker compose -f docker-compose-mysql.yaml up -d
dotnet test tests/Paramore.Brighter.MySQL.Tests --filter "Category=MySQL"
```

## What Gets Tested

Twelve behaviours, in both Sync and Async form:

| Test | Asserts |
|---|---|
| `When_Adding_A_Message_It_Should_Be_Stored_With_All_Properties` | Round-trip fidelity of every header and the body. |
| `When_Adding_A_Duplicate_Message_It_Should_Not_Throw` | Adding the same id twice is idempotent, not an error. |
| `When_Adding_A_Message_Within_Transaction_It_Should_Be_Stored_After_Commit` | Commit makes the write visible. |
| `When_Adding_A_Message_Within_Transaction_And_Rollback_It_Should_Not_Be_Stored` | Rollback discards it. |
| `When_Deleting_One_Message_It_Should_Be_Removed_From_Outbox` | Single delete. |
| `When_Deleting_Multiple_Messages_They_Should_Be_Removed_From_Outbox` | Batch delete. |
| `When_Retrieving_A_Message_By_Id_It_Should_Return_The_Correct_Message` | Get by id. |
| `When_Retrieving_Messages_By_Ids_It_Should_Return_Only_Requested_Messages` | Get by ids returns no extras. |
| `When_Retrieving_A_Non_Existent_Message_It_Should_Return_Empty_Message` | A miss returns an empty `Message`, and MUST NOT throw. |
| `When_Retrieving_All_Messages_They_Should_Include_Dispatched_And_Undispatched` | Get-all spans both states. |
| `When_Retrieving_Outstanding_Messages_It_Should_Filter_By_Age` | Age filtering on undispatched. |
| `When_Retrieving_Dispatched_Messages_It_Should_Filter_By_Age` | Age filtering on dispatched. |

The two transaction tests are omitted when `SupportsTransactions` is `false`.

## Known Defects

These are defects in the generator as it stands, both tracked. They are documented so you are not surprised; do not work around them by editing generated files.

### Why you SHOULD prefer `Outboxes` even for one outbox

> Tracked as [#4238](https://github.com/BrighterCommand/Brighter/issues/4238).

The singular `Outbox` branch **generates only the Async suite — the Sync tests are silently never emitted** ([`OutboxGenerator.cs:49-61`](../../../tools/Paramore.Brighter.Test.Generator/Generators/OutboxGenerator.cs)):

```csharp
if (configuration.Outbox != null)
{
    await GenerateAsync(configuration, "Outbox", "Outbox", configuration.Outbox);   // no-op: Templates/Outbox has no .liquid files

    var prefix = configuration.Outbox.Prefix;
    await GenerateAsync(
        configuration,
        Path.Combine("Outbox", prefix, "Generated", "Async"),                        // Async only; no Sync call
        Path.Combine("Outbox", "Async"),
        configuration.Outbox,
        filename => SkipTest(configuration.Outbox, filename)
    );
}
```

The first call asks for template folder `Templates/Outbox`, which holds only the `Sync/` and `Async/` subdirectories and no templates of its own — the generator does not recurse, so it renders nothing. The second emits Async. There is no Sync call.

The `Outboxes` branch emits both, and also defaults `Prefix` from the key. Projects using the singular form today ([DynamoDB](../../../tests/Paramore.Brighter.DynamoDB.Tests/test-configuration.json), MongoDB) therefore have **async-only coverage**. Using `Outboxes` with a single entry gets you the full suite:

```json
{
  "Namespace": "Paramore.Brighter.MyStore.Tests",
  "Outboxes": {
    "MyStore": {
      "Transaction": "System.Data.Common.DbTransaction",
      "OutboxProvider": "MyStoreOutboxProvider",
      "Category": "MyStore",
      "CollectionName": "MyStoreOutbox"
    }
  }
}
```

### `CollectionName` has no effect on Sync outbox tests

> Tracked as [#4239](https://github.com/BrighterCommand/Brighter/issues/4239).

None of the 12 templates in `Templates/Outbox/Sync/` reference `CollectionName`; all 12 Async ones do. So an outbox config setting `CollectionName` gets `[Collection(...)]` on its async tests only, and the sync tests remain unserialised. If sync tests contend over shared infrastructure, that is the cause.
