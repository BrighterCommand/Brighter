# Tasks: Replay Matching Outbox Events When Inbox Has Already Seen

**Spec**: 0027-replay-matching-outbox-events-when-inbox-has-already-seen
**ADR**: [0057 — Replay Outbox Messages on Inbox Duplicate Detection](../../docs/adr/0057-replay-outbox-on-inbox-duplicate.md)
**Branch**: `replay_on_seen`

## Prerequisites

- [x] Requirements approved
- [x] ADR 0057 accepted

---

## Task 1: Structural — UseInboxHandler uses pipeline's `this.Context` instead of `InitRequestContext()`, expose `InstrumentationOptions`

This is a **tidy-first structural change** with two parts:

**Part A**: Fix `UseInboxHandler` and `UseInboxHandlerAsync` to use the pipeline's `IRequestContext` (inherited `Context` property) instead of creating a private `RequestContext` via `InitRequestContext()`. This is a prerequisite for CausationId propagation via `RequestContext.Bag`.

**Part B**: Expose `instrumentationOptions` from `RequestHandler<T>` as a protected property so that subclasses (including `UseInboxHandler`) can gate telemetry events on `InstrumentationOptions.Brighter`. Currently `instrumentationOptions` is a primary constructor parameter on `RequestHandler<T>` (private field, inaccessible to derived classes).

- **Files**:
  - `src/Paramore.Brighter/RequestHandler.cs` — add `protected InstrumentationOptions InstrumentationOptions => instrumentationOptions;`
  - `src/Paramore.Brighter/RequestHandlerAsync.cs` — same protected property
  - `src/Paramore.Brighter/Inbox/Handlers/UseInboxHandler.cs`
  - `src/Paramore.Brighter/Inbox/Handlers/UseInboxHandlerAsync.cs`
- Changes (Part A):
  - Remove the private `InitRequestContext()` method from both UseInbox files
  - Replace all `var requestContext = InitRequestContext()` calls with `this.Context`
  - Pass `this.Context` to `_inbox.Exists()`, `_inbox.ExistsAsync()`, `_inbox.Add()`, `_inbox.AddAsync()`
- Changes (Part B):
  - Add `protected InstrumentationOptions InstrumentationOptions => instrumentationOptions;` to `RequestHandler<TRequest>`
  - Add same to `RequestHandlerAsync<TRequest>`
- **Verification**: Run existing OnceOnly tests — all must pass:
  - `tests/Paramore.Brighter.Core.Tests/OnceOnly/` (all 8 test files)
- Commit separately as a structural (tidy) change

---

## Task 2: Structural — Enrich `PipelineStepDescription` with `Attribute` property

This is a **tidy-first structural change** — adding a non-positional `Attribute` property to `PipelineStepDescription` so validation rules can inspect attribute properties like `OnceOnlyAction`.

- **Files**:
  - `src/Paramore.Brighter/Validation/PipelineStepDescription.cs` — add `public RequestHandlerAttribute? Attribute { get; init; }` (non-positional property)
  - `src/Paramore.Brighter/PipelineBuilder.cs` (or wherever `Describe()` projects attributes) — change `.Select(a => new PipelineStepDescription(...))` to include `{ Attribute = a }`
- **Verification**: Run existing validation tests — all must pass:
  - `tests/Paramore.Brighter.Core.Tests/Validation/` (all test files)
- Commit separately as a structural (tidy) change

---

## Task 3: Structural — `Describe()` includes global inbox attributes + `InboxConfiguration` passed to validation path

This is a **tidy-first structural change** — ensuring `PipelineBuilder.Describe()` includes global inbox attributes (matching what `Build()` does) and that `ValidatePipelines()` passes `InboxConfiguration` through.

**Depends on**: Task 2 (`PipelineStepDescription.Attribute` property must exist for global inbox attributes to be fully useful)

- **Files**:
  - `src/Paramore.Brighter.Extensions.DependencyInjection/BrighterPipelineValidationExtensions.cs` — resolve `InboxConfiguration` from DI, pass to `PipelineBuilder`
  - `src/Paramore.Brighter/PipelineBuilder.cs` — in `Describe()`, inject global inbox attribute using same guards as `AddGlobalInboxAttributes()` (`HasNoInboxAttributesInPipeline()`, `HasExistingUseInboxAttributesInPipeline()`)
- **Verification**: Run existing validation + pipeline tests:
  - `tests/Paramore.Brighter.Core.Tests/Validation/`
  - `tests/Paramore.Brighter.Core.Tests/CommandProcessors/Pipeline/`
- Commit separately as a structural (tidy) change

---

## Task 4: Structural — Expose `Outbox` from `IAmAnOutboxProducerMediator`

This is a **tidy-first structural change** — adding a read-only `IAmAnOutbox? Outbox` property so pipeline validation can access the outbox instance.

- **Files**:
  - `src/Paramore.Brighter/OutboxProducerMediator.cs` — add `IAmAnOutbox? Outbox` property to interface and implementation
- Changes:
  - Add `IAmAnOutbox? Outbox { get; }` to `IAmAnOutboxProducerMediator` interface
  - Implement in `OutboxProducerMediator<TMessage, TTransaction>`: `public IAmAnOutbox? Outbox => (IAmAnOutbox?)_outBox ?? _asyncOutbox;`
- **Verification**: Project builds without errors
- Commit separately as a structural (tidy) change

---

## Task 5: Structural — New types: `OnceOnlyAction.Replay`, CausationId constants, role interfaces

This is a **structural change** adding the new types needed before behavioral work. No behavior yet — just type definitions.

- **Files**:
  - `src/Paramore.Brighter/Inbox/OnceOnlyAction.cs` — add `Replay` enum value
  - `src/Paramore.Brighter/RequestContextBagNames.cs` — add `public const string CausationId = "Brighter-CausationId";`
  - `src/Paramore.Brighter/Observability/BrighterSemanticConventions.cs` — add `public const string CausationId = "paramore.brighter.causation_id";`
  - `src/Paramore.Brighter/Inbox/IAmACausationTrackingInbox.cs` — new file with `SupportsCausationTracking()`, `SupportsCausationTrackingAsync()`, `GetCausationId()`, `GetCausationIdAsync()` methods
  - `src/Paramore.Brighter/IAmACausationTrackingOutbox.cs` — new file with `SupportsCausationTracking()`, `SupportsCausationTrackingAsync()`, `ReplayCausation()`, `ReplayCausationAsync()` methods
- **Verification**: Project builds without errors
- Commit separately

---

## Task 6: TEST + IMPLEMENT — InMemoryInbox stores CausationId and retrieves it

- [ ] **TEST + IMPLEMENT: InMemoryInbox reads CausationId from RequestContext.Bag on Add and returns it via GetCausationId**
  - **USE COMMAND**: `/test-first when adding to inbox with CausationId in context bag should store and retrieve it`
  - Test location: `tests/Paramore.Brighter.Core.Tests/OnceOnly/`
  - Test file: `When_adding_to_inbox_with_causation_id_should_store_and_retrieve.cs`
  - Test should verify:
    - Add a command with `CausationId` in `RequestContext.Bag`
    - `GetCausationId()` returns the stored CausationId for that command
    - `GetCausationIdAsync()` returns the same value
    - When no CausationId in Bag, `GetCausationId()` returns null
  - **⛔ STOP HERE — WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation:
    - `InMemoryInbox` implements `IAmACausationTrackingInbox`
    - `InboxItem` gains `string? CausationId` property
    - `Add()`/`AddAsync()` reads CausationId from `requestContext?.Bag` using `RequestContextBagNames.CausationId` key
    - `GetCausationId()`/`GetCausationIdAsync()` looks up the inbox entry and returns its CausationId
    - `SupportsCausationTracking()` returns `true`

---

## Task 7: TEST + IMPLEMENT — InMemoryOutbox stores CausationId and replays by clearing dispatch state

- [ ] **TEST + IMPLEMENT: InMemoryOutbox reads CausationId from RequestContext.Bag on Add and ReplayCausation clears TimeFlushed**
  - **USE COMMAND**: `/test-first when replaying causation on outbox should clear dispatch state for matching messages`
  - Test location: `tests/Paramore.Brighter.Core.Tests/OnceOnly/`
  - Test file: `When_replaying_causation_on_outbox_should_clear_dispatch_state.cs`
  - Test should verify:
    - Add multiple messages to outbox with same CausationId in `RequestContext.Bag`
    - Mark them as dispatched (set `TimeFlushed`)
    - Call `ReplayCausation(causationId)` 
    - Verify `TimeFlushed` is reset to `DateTimeOffset.MinValue` for matching messages
    - Verify messages with different CausationId are not affected
    - Verify `ReplayCausationAsync()` produces the same result
    - Verify `SupportsCausationTracking()` returns `true`
  - **⛔ STOP HERE — WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation:
    - `InMemoryOutbox` implements `IAmACausationTrackingOutbox`
    - `OutboxEntry` gains `string? CausationId` property
    - `Add()`/`AddAsync()` reads CausationId from `requestContext?.Bag` using `RequestContextBagNames.CausationId` key
    - `ReplayCausation()` finds all entries with matching CausationId and sets `TimeFlushed = DateTimeOffset.MinValue`
    - `SupportsCausationTracking()` returns `true`

---

## Task 8: TEST + IMPLEMENT — Sync UseInboxHandler generates CausationId in Bag on first handling

- [ ] **TEST + IMPLEMENT: Sync UseInboxHandler sets CausationId in RequestContext.Bag when handling a new command**
  - **USE COMMAND**: `/test-first when sync inbox handler handles new command should set causation id in context bag`
  - Test location: `tests/Paramore.Brighter.Core.Tests/OnceOnly/`
  - Test file: `When_handling_new_command_should_set_causation_id_in_context_bag.cs`
  - Test doubles needed:
    - Reuse or extend existing test double handler from `tests/Paramore.Brighter.Core.Tests/OnceOnly/TestDoubles/`
  - Test should verify:
    - After Handle(), `RequestContext.Bag` contains `RequestContextBagNames.CausationId` key
    - The CausationId value defaults to the command's `Id`
    - The inbox entry has the same CausationId stored
  - **⛔ STOP HERE — WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation:
    - In `UseInboxHandler.Handle()`, before calling `base.Handle()`:
      - Generate CausationId (default to `request.Id`)
      - Store in `Context.Bag[RequestContextBagNames.CausationId] = causationId`
    - The inbox `Add()` already reads from the Bag (Task 6)

---

## Task 9: TEST + IMPLEMENT — Async UseInboxHandlerAsync generates CausationId in Bag on first handling

- [ ] **TEST + IMPLEMENT: Async UseInboxHandlerAsync sets CausationId in RequestContext.Bag when handling a new command**
  - **USE COMMAND**: `/test-first when async inbox handler handles new command should set causation id in context bag`
  - Test location: `tests/Paramore.Brighter.Core.Tests/OnceOnly/`
  - Test file: `When_handling_new_command_async_should_set_causation_id_in_context_bag.cs`
  - Test doubles needed:
    - Reuse or extend existing async test double handler from `tests/Paramore.Brighter.Core.Tests/OnceOnly/TestDoubles/`
  - Test should verify:
    - After HandleAsync(), `RequestContext.Bag` contains `RequestContextBagNames.CausationId` key
    - The CausationId value defaults to the command's `Id`
    - The inbox entry has the same CausationId stored
  - **⛔ STOP HERE — WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation:
    - In `UseInboxHandlerAsync.HandleAsync()`, same logic as sync: generate CausationId, store in `Context.Bag`

---

## Task 10: TEST + IMPLEMENT — Sync UseInboxHandler replays outbox on duplicate when Replay configured

- [ ] **TEST + IMPLEMENT: Sync UseInboxHandler replays outbox messages when duplicate detected and OnceOnlyAction is Replay**
  - **USE COMMAND**: `/test-first when sync inbox handler detects duplicate with replay configured should clear outbox dispatch state`
  - Test location: `tests/Paramore.Brighter.Core.Tests/OnceOnly/`
  - Test file: `When_handling_duplicate_command_with_replay_should_clear_outbox_dispatch.cs`
  - Test doubles needed:
    - Handler decorated with `[UseInbox(step: 1, contextKey: "test", onceOnly: true, onceOnlyAction: OnceOnlyAction.Replay)]`
    - InMemoryInbox pre-populated with existing entry (with CausationId)
    - InMemoryOutbox with dispatched messages sharing that CausationId
  - Test should verify:
    - Handler is NOT re-executed (request returned without calling `base.Handle()`)
    - Outbox messages with matching CausationId have their dispatch state cleared
    - Outbox messages with different CausationId are not affected
    - The CausationId is retrieved from the inbox entry via `IAmACausationTrackingInbox.GetCausationId()`
  - **⛔ STOP HERE — WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation:
    - `UseInboxHandler` constructor gains optional `IAmACausationTrackingOutbox? outbox = null` parameter
    - In `Handle()`, add new branch: `if (exists && _onceOnlyAction is OnceOnlyAction.Replay)` — retrieve CausationId from inbox, call `_outbox.ReplayCausation()`, return request
    - Log replay action via source-generated `Log.CommandHasAlreadyBeenSeenReplayingOutbox`

---

## Task 11: TEST + IMPLEMENT — Async UseInboxHandlerAsync replays outbox on duplicate when Replay configured

- [ ] **TEST + IMPLEMENT: Async UseInboxHandlerAsync replays outbox messages when duplicate detected and OnceOnlyAction is Replay**
  - **USE COMMAND**: `/test-first when async inbox handler detects duplicate with replay configured should clear outbox dispatch state`
  - Test location: `tests/Paramore.Brighter.Core.Tests/OnceOnly/`
  - Test file: `When_handling_duplicate_command_async_with_replay_should_clear_outbox_dispatch.cs`
  - Test doubles needed:
    - Async handler decorated with `[UseInboxAsync(step: 1, contextKey: "test", onceOnly: true, onceOnlyAction: OnceOnlyAction.Replay)]`
    - InMemoryInbox pre-populated with existing entry (with CausationId)
    - InMemoryOutbox with dispatched messages sharing that CausationId
  - Test should verify:
    - Handler is NOT re-executed
    - Outbox messages with matching CausationId have their dispatch state cleared
    - CausationId retrieved via `IAmACausationTrackingInbox.GetCausationIdAsync()`
  - **⛔ STOP HERE — WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation:
    - `UseInboxHandlerAsync` constructor gains optional `IAmACausationTrackingOutbox? outbox = null` parameter
    - In `HandleAsync()`, add Replay branch matching sync implementation using async methods

---

## Task 12: TEST + IMPLEMENT — Sync UseInboxHandler handles Replay gracefully when no outbox configured (terminal step)

- [ ] **TEST + IMPLEMENT: Sync UseInboxHandler with Replay configured but no outbox returns without error**
  - **USE COMMAND**: `/test-first when sync inbox handler detects duplicate with replay but no outbox should return without error`
  - Test location: `tests/Paramore.Brighter.Core.Tests/OnceOnly/`
  - Test file: `When_handling_duplicate_with_replay_and_no_outbox_should_return_without_error.cs`
  - Test should verify:
    - When `_outbox` is null, the handler returns the request without throwing
    - Handler is NOT re-executed
  - **⛔ STOP HERE — WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation:
    - The null check on `_outbox` in the Replay branch already handles this (from Task 10)
    - This task validates the no-outbox terminal step scenario

---

## Task 13: TEST + IMPLEMENT — Async UseInboxHandlerAsync handles Replay gracefully when no outbox configured (terminal step)

- [ ] **TEST + IMPLEMENT: Async UseInboxHandlerAsync with Replay configured but no outbox returns without error**
  - **USE COMMAND**: `/test-first when async inbox handler detects duplicate with replay but no outbox should return without error`
  - Test location: `tests/Paramore.Brighter.Core.Tests/OnceOnly/`
  - Test file: `When_handling_duplicate_async_with_replay_and_no_outbox_should_return_without_error.cs`
  - Test should verify:
    - When `_outbox` is null, the async handler returns the request without throwing
    - Handler is NOT re-executed
  - **⛔ STOP HERE — WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation:
    - The null check on `_outbox` in the Replay branch already handles this (from Task 11)
    - This task validates the no-outbox terminal step scenario for the async path

---

## Task 14: TEST + IMPLEMENT — Pipeline validation rejects Replay without causation-tracking support

- [ ] **TEST + IMPLEMENT: Pipeline validation detects OnceOnlyAction.Replay without causation-tracking inbox or outbox**
  - **USE COMMAND**: `/test-first when pipeline has replay configured without causation tracking should report validation error`
  - Test location: `tests/Paramore.Brighter.Core.Tests/Validation/`
  - Test file: `When_replay_configured_without_causation_tracking_should_report_error.cs`
  - Test should verify:
    - Replay with inbox that does not implement `IAmACausationTrackingInbox` → Error
    - Replay with inbox that implements `IAmACausationTrackingInbox` but `SupportsCausationTracking()` returns false → Warning
    - Replay with no outbox configured → Warning (terminal step)
    - Replay with outbox that does not implement `IAmACausationTrackingOutbox` → Error
    - Replay with outbox that implements `IAmACausationTrackingOutbox` but `SupportsCausationTracking()` returns false → Warning
    - Replay with both inbox and outbox supporting causation tracking → no findings
    - Non-Replay (Throw/Warn) pipelines → no findings regardless of causation tracking support
  - **⛔ STOP HERE — WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation:
    - Add `ReplayRequiresCausationTracking(IAmAnInbox?, IAmAnOutbox?)` to `HandlerPipelineValidationRules`
    - Wire into `PipelineValidator.ValidateHandlerPipelines()` specs array
    - In `BrighterPipelineValidationExtensions.ValidatePipelines()`: resolve outbox via `IAmAnOutboxProducerMediator.Outbox`, pass to `PipelineValidator`

---

## Task 15: TEST + IMPLEMENT — UseInboxHandler adds Replay telemetry event to pipeline span

- [ ] **TEST + IMPLEMENT: UseInboxHandler writes ActivityEvent to the pipeline span when Replay is triggered**
  - **USE COMMAND**: `/test-first when inbox handler replays duplicate should add replay telemetry event to span`
  - Test location: `tests/Paramore.Brighter.Core.Tests/OnceOnly/`
  - Test file: `When_replaying_duplicate_should_add_replay_telemetry_event_to_span.cs`
  - Test should verify:
    - When Replay is triggered, an ActivityEvent named `"UseInboxHandler Duplicate Replay"` is added to `Context.Span`
    - Event tags include `request.id` and `causation_id` (using `BrighterSemanticConventions` constants)
    - Events are only added when `Context?.Span != null` and `InstrumentationOptions` includes `InstrumentationOptions.Brighter` (no event when either condition is false)
  - **⛔ STOP HERE — WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation:
    - In `UseInboxHandler.Handle()` and `UseInboxHandlerAsync.HandleAsync()`: add `ActivityEvent` to `Context.Span` in the Replay branch
    - Guard with `Context?.Span != null` and gate on `InstrumentationOptions.HasFlag(InstrumentationOptions.Brighter)` (using the protected property exposed in Task 1 Part B)
    - Use `BrighterSemanticConventions.CausationId` for the causation tag

---

## Task 16: TEST + IMPLEMENT — UseInboxHandler adds telemetry events for Throw, Warn, and Add paths (tidy improvement)

- [ ] **TEST + IMPLEMENT: UseInboxHandler writes ActivityEvents for existing Throw, Warn, and Add paths**
  - **USE COMMAND**: `/test-first when inbox handler handles command should add telemetry events for all paths`
  - Test location: `tests/Paramore.Brighter.Core.Tests/OnceOnly/`
  - Test file: `When_inbox_handler_handles_command_should_add_telemetry_events.cs`
  - Test should verify:
    - When Throw is triggered, an ActivityEvent named `"UseInboxHandler Duplicate Throw"` is added to `Context.Span`
    - When Warn is triggered, an ActivityEvent named `"UseInboxHandler Duplicate Warn"` is added
    - When first handling (Add), an ActivityEvent named `"UseInboxHandler Add"` is added
    - Events are only added when `Context?.Span != null` and `InstrumentationOptions` includes `InstrumentationOptions.Brighter` (no event when either condition is false)
  - **⛔ STOP HERE — WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation:
    - In `UseInboxHandler.Handle()` and `UseInboxHandlerAsync.HandleAsync()`: add `ActivityEvent` to `Context.Span` in the Throw, Warn, and Add branches
    - Guard with `Context?.Span != null` and gate on `InstrumentationOptions.HasFlag(InstrumentationOptions.Brighter)` (using the protected property exposed in Task 1 Part B)
  - **Note**: This is a tidy improvement to existing paths, not directly required by the Replay feature. Commit separately

---

## Task 17: TEST + IMPLEMENT — DI registration of `IAmACausationTrackingOutbox`

- [ ] **TEST + IMPLEMENT: ServiceCollection registers IAmACausationTrackingOutbox when outbox supports it**
  - **USE COMMAND**: `/test-first when registering outbox that supports causation tracking should register under role interface`
  - Test location: `tests/Paramore.Brighter.Core.Tests/OnceOnly/`
  - Test file: `When_registering_outbox_with_causation_tracking_should_register_role_interface.cs`
  - Test should verify:
    - When outbox implements `IAmACausationTrackingOutbox`, it is resolvable from DI as `IAmACausationTrackingOutbox`
    - When outbox does not implement `IAmACausationTrackingOutbox`, resolving returns null
    - Same outbox instance is returned for both primary interface and `IAmACausationTrackingOutbox`
  - **⛔ STOP HERE — WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation:
    - In `ServiceCollectionExtensions` (or equivalent DI setup): after registering outbox, check `if (outbox is IAmACausationTrackingOutbox)` and register under that interface
    - Note: `IAmACausationTrackingInbox` does NOT need separate DI registration — `UseInboxHandler` already has the inbox instance and pattern-matches it at runtime (`if (_inbox is IAmACausationTrackingInbox trackingInbox)`)

---

## Task 18: Base test classes for causation tracking in Paramore.Brighter.Base.Test

- [ ] **TEST + IMPLEMENT: Base test classes define causation tracking scenarios for persistent store tests**
  - **USE COMMAND**: `/test-first when persistent inbox stores causation id should match base test expectations`
  - Test location: `tests/Paramore.Brighter.Base.Test/`
  - Test should define base test classes:
    - `CausationTrackingInboxBaseTests` — abstract tests for `IAmACausationTrackingInbox`: Add with CausationId, GetCausationId retrieval, SupportsCausationTracking
    - `CausationTrackingOutboxBaseTests` — abstract tests for `IAmACausationTrackingOutbox`: Add with CausationId, ReplayCausation clearing dispatch state, SupportsCausationTracking
  - These are validated against InMemoryInbox/InMemoryOutbox first
  - **⛔ STOP HERE — WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation:
    - Create abstract base test classes with virtual setup methods for store-specific initialization
    - Persistent store test projects will inherit these and provide their store implementations

---

## Task 19: TEST + IMPLEMENT — Relational inbox stores implement `IAmACausationTrackingInbox` (MsSql, MySql, Postgres, Sqlite, Spanner)

- [ ] **TEST + IMPLEMENT: Relational inbox stores add CausationId column and implement IAmACausationTrackingInbox**
  - **USE COMMAND**: `/test-first when relational inbox stores causation id should store and retrieve via base tests`
  - Stores: MsSql, MySql, Postgres, Sqlite, Spanner
  - Each store:
    - Adds nullable `CausationId` column to schema (existing rows have null — no data migration)
    - Reads `CausationId` from `RequestContext.Bag` in `Add()`/`AddAsync()` and stores it
    - Implements `IAmACausationTrackingInbox` (`SupportsCausationTracking()`, `GetCausationId()`, `GetCausationIdAsync()`)
    - `SupportsCausationTracking()` performs runtime schema check (column exists?)
  - Tests: Each store's test project inherits from `CausationTrackingInboxBaseTests` (Task 18)
  - **⛔ STOP HERE — WAIT FOR USER APPROVAL in IDE before implementing**
  - Depends on: Task 18

---

## Task 20: TEST + IMPLEMENT — NoSQL inbox stores implement `IAmACausationTrackingInbox` (DynamoDB, DynamoDB.V4, Firestore, MongoDb)

- [ ] **TEST + IMPLEMENT: NoSQL inbox stores add CausationId attribute and implement IAmACausationTrackingInbox**
  - **USE COMMAND**: `/test-first when nosql inbox stores causation id should store and retrieve via base tests`
  - Stores: DynamoDB, DynamoDB.V4, Firestore, MongoDb
  - Each store:
    - Adds nullable `CausationId` attribute/field to schema (existing entries have null — no data migration)
    - Reads `CausationId` from `RequestContext.Bag` in `Add()`/`AddAsync()` and stores it
    - Implements `IAmACausationTrackingInbox` (`SupportsCausationTracking()`, `GetCausationId()`, `GetCausationIdAsync()`)
    - `SupportsCausationTracking()` performs runtime schema check (attribute exists?)
  - Tests: Each store's test project inherits from `CausationTrackingInboxBaseTests` (Task 18)
  - **⛔ STOP HERE — WAIT FOR USER APPROVAL in IDE before implementing**
  - Depends on: Task 18

---

## Task 21: TEST + IMPLEMENT — Relational outbox stores implement `IAmACausationTrackingOutbox` (MsSql, MySql, PostgreSql, Sqlite, Spanner)

- [ ] **TEST + IMPLEMENT: Relational outbox stores add CausationId column and implement IAmACausationTrackingOutbox**
  - **USE COMMAND**: `/test-first when relational outbox stores causation id should store and replay via base tests`
  - Stores: MsSql, MySql, PostgreSql, Sqlite, Spanner
  - Each store:
    - Adds nullable `CausationId` column to schema, indexed for efficient replay queries
    - Reads `CausationId` from `RequestContext.Bag` in `Add()`/`AddAsync()` and stores it
    - Implements `IAmACausationTrackingOutbox` (`SupportsCausationTracking()`, `ReplayCausation()`, `ReplayCausationAsync()`)
    - `SupportsCausationTracking()` performs runtime schema check (column exists?)
  - Tests: Update Liquid templates in `tools/Paramore.Brighter.Test.Generator/` to generate causation tracking test cases from `CausationTrackingOutboxBaseTests`
  - **⛔ STOP HERE — WAIT FOR USER APPROVAL in IDE before implementing**
  - Depends on: Task 18

---

## Task 22: TEST + IMPLEMENT — NoSQL outbox stores implement `IAmACausationTrackingOutbox` (DynamoDB, DynamoDB.V4, Firestore, MongoDb)

- [ ] **TEST + IMPLEMENT: NoSQL outbox stores add CausationId attribute and implement IAmACausationTrackingOutbox**
  - **USE COMMAND**: `/test-first when nosql outbox stores causation id should store and replay via base tests`
  - Stores: DynamoDB, DynamoDB.V4, Firestore, MongoDb
  - Each store:
    - Adds nullable `CausationId` attribute/field to schema, indexed for efficient replay queries
    - Reads `CausationId` from `RequestContext.Bag` in `Add()`/`AddAsync()` and stores it
    - Implements `IAmACausationTrackingOutbox` (`SupportsCausationTracking()`, `ReplayCausation()`, `ReplayCausationAsync()`)
    - `SupportsCausationTracking()` performs runtime schema check (attribute exists?)
  - Tests: Each store's test project inherits from `CausationTrackingOutboxBaseTests`
  - **⛔ STOP HERE — WAIT FOR USER APPROVAL in IDE before implementing**
  - Depends on: Task 18

---

## Task 23: Build verification

- [ ] **Build and run all core tests**
  - Run `dotnet build src/Paramore.Brighter/Paramore.Brighter.csproj` — must compile
  - Run `dotnet test tests/Paramore.Brighter.Core.Tests/Paramore.Brighter.Core.Tests.csproj` — all tests pass
  - Verify existing OnceOnly tests pass (no regressions):
    - `When_Handling_A_Command_With_A_Inbox_Enabled`
    - `When_Handling_A_Command_Once_Only_With_Throw_Enabled`
    - `When_Handling_A_Command_Once_Only_With_Warn_Enabled`
    - (and their async equivalents)
  - Verify existing Validation tests pass (no regressions)

---

## Task Summary

| # | Type | Description | Depends On |
|---|------|-------------|------------|
| 1 | Structural (tidy) | UseInboxHandler uses pipeline's `this.Context` | — |
| 2 | Structural (tidy) | Enrich `PipelineStepDescription` with `Attribute` property | — |
| 3 | Structural (tidy) | `Describe()` includes global inbox + `InboxConfiguration` in validation | 2 |
| 4 | Structural (tidy) | Expose `Outbox` from `IAmAnOutboxProducerMediator` | — |
| 5 | Structural | New types: `OnceOnlyAction.Replay`, CausationId constants, role interfaces | — |
| 6 | Test + Implement | InMemoryInbox stores and retrieves CausationId | 5 |
| 7 | Test + Implement | InMemoryOutbox stores CausationId + `ReplayCausation` | 5 |
| 8 | Test + Implement | Sync UseInboxHandler generates CausationId on first handling | 1, 6 |
| 9 | Test + Implement | Async UseInboxHandlerAsync generates CausationId on first handling | 1, 6 |
| 10 | Test + Implement | Sync UseInboxHandler replays outbox on duplicate (Replay) | 6, 7, 8 |
| 11 | Test + Implement | Async UseInboxHandlerAsync replays outbox on duplicate (Replay) | 6, 7, 9 |
| 12 | Test + Implement | Sync UseInboxHandler handles Replay with no outbox (terminal step) | 10 |
| 13 | Test + Implement | Async UseInboxHandlerAsync handles Replay with no outbox (terminal step) | 11 |
| 14 | Test + Implement | Pipeline validation: Replay requires causation-tracking support | 2, 3, 4, 5 |
| 15 | Test + Implement | UseInboxHandler Replay telemetry event on pipeline span | 1, 10, 11 |
| 16 | Test + Implement | UseInboxHandler Throw/Warn/Add telemetry events (tidy improvement) | 1, 15 |
| 17 | Test + Implement | DI registration of `IAmACausationTrackingOutbox` | 5, 6, 7 |
| 18 | Test + Implement | Base test classes for persistent store causation tracking | 6, 7 |
| 19 | Test + Implement | Relational inbox stores: `IAmACausationTrackingInbox` (MsSql, MySql, Postgres, Sqlite, Spanner) | 18 |
| 20 | Test + Implement | NoSQL inbox stores: `IAmACausationTrackingInbox` (DynamoDB, DynamoDB.V4, Firestore, MongoDb) | 18 |
| 21 | Test + Implement | Relational outbox stores: `IAmACausationTrackingOutbox` (MsSql, MySql, PostgreSql, Sqlite, Spanner) | 18 |
| 22 | Test + Implement | NoSQL outbox stores: `IAmACausationTrackingOutbox` (DynamoDB, DynamoDB.V4, Firestore, MongoDb) | 18 |
| 23 | Verification | Build + run all core tests | 1–22 |

## FR Coverage

| FR | Description | Task(s) |
|----|-------------|---------|
| FR1 | CausationId propagation via RequestContext.Bag | 5, 8, 9 |
| FR2 | CausationId stored in inbox and outbox | 6, 7, 19, 20, 21, 22 |
| FR3 | Replay on duplicate clears DispatchedAt | 10, 11 |
| FR4 | Sweeper re-dispatch (existing, unchanged) | — (existing behavior; outbox state assertions in Tasks 10/11 verify messages are marked for re-dispatch) |
| FR5 | CausationId distinct from JobId | 5 (separate constant/concept) |
| FR6 | New OnceOnlyAction.Replay | 5 |

## ADR Decision Coverage

| ADR Decision | Task(s) |
|-------------|---------|
| Causation Id concept + propagation via Bag | 5, 8, 9 |
| OnceOnlyAction.Replay | 5 |
| IAmACausationTrackingInbox / IAmACausationTrackingOutbox | 5 |
| InMemoryInbox/Outbox support | 6, 7 |
| UseInboxHandler Replay logic | 10, 11, 12, 13 |
| Pipeline validation: ReplayRequiresCausationTracking | 14 |
| PipelineStepDescription enrichment | 2 |
| Describe() global inbox + InboxConfiguration in validation | 3 |
| Outbox property on IAmAnOutboxProducerMediator | 4 |
| Observability (Replay telemetry) | 15 |
| Observability (Throw/Warn/Add telemetry — tidy improvement) | 16 |
| DI registration | 17 |
| Persistent store implementations | 19, 20, 21, 22 |
| UseInboxHandler uses pipeline Context (prerequisite) | 1 |
