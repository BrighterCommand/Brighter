# Session Resume — Replay Outbox on Inbox Duplicate

## Branch
`replay_on_seen`

## Issue
#2541 — Force Replay of Outbox on seeing duplicate Processed Inbox Message

## Spec
`specs/0027-replay-matching-outbox-events-when-inbox-has-already-seen/`

## Status
- [x] Requirements — approved (2026-04-16), updated 2026-04-17 (ConversationId → CausationId rename, AC-8 updated to all persistent stores)
- [x] Design — ADR 0057 accepted, 5 rounds of review (all findings addressed)
- [x] Tasks — approved (2026-04-17), 5 rounds of review (all findings addressed), 23 tasks
- [ ] Implementation — not started

## Current position
Tasks approved after 5 adversarial review rounds. Ready to begin implementation with `/spec:implement`, starting at Task 1.

### Task review history
- Round 1: 3 findings ≥60 (75: missing async terminal step, 72: monolithic persistent store tasks, 62: telemetry covers too many behaviors) — all addressed
- Round 2: 1 finding ≥60 (65: missing InstrumentationOptions.Brighter gating) — addressed
- Round 3: 1 finding ≥60 (75: InstrumentationOptions inaccessible from UseInboxHandler) — addressed by adding Part B to Task 1
- Round 4: 1 finding ≥60 (75: Spanner misclassified as NoSQL) — addressed by moving to relational tasks
- Round 5: PASS — 0 findings ≥60

## Task overview (23 tasks)

### Structural prerequisites (Tasks 1-5)
1. UseInboxHandler uses pipeline's `this.Context` + expose `InstrumentationOptions` as protected property on `RequestHandler<T>`
2. Enrich `PipelineStepDescription` with `Attribute` property
3. `Describe()` includes global inbox attributes + `InboxConfiguration` in validation path (depends on 2)
4. Expose `Outbox` from `IAmAnOutboxProducerMediator`
5. New types: `OnceOnlyAction.Replay`, `RequestContextBagNames.CausationId`, `BrighterSemanticConventions.CausationId`, `IAmACausationTrackingInbox`, `IAmACausationTrackingOutbox`

### Core behavior (Tasks 6-13, test-first)
6. InMemoryInbox stores and retrieves CausationId
7. InMemoryOutbox stores CausationId + `ReplayCausation`
8. Sync UseInboxHandler generates CausationId on first handling
9. Async UseInboxHandlerAsync generates CausationId on first handling
10. Sync UseInboxHandler replays outbox on duplicate (Replay action)
11. Async UseInboxHandlerAsync replays outbox on duplicate (Replay action)
12. Sync UseInboxHandler handles Replay with no outbox (terminal step)
13. Async UseInboxHandlerAsync handles Replay with no outbox (terminal step)

### Infrastructure (Tasks 14-18, test-first)
14. Pipeline validation: Replay requires causation-tracking support
15. UseInboxHandler Replay telemetry event on pipeline span
16. UseInboxHandler Throw/Warn/Add telemetry events (tidy improvement)
17. DI registration of `IAmACausationTrackingOutbox`
18. Base test classes for persistent store causation tracking

### Persistent stores (Tasks 19-22, test-first)
19. Relational inbox stores: MsSql, MySql, Postgres, Sqlite, Spanner
20. NoSQL inbox stores: DynamoDB, DynamoDB.V4, Firestore, MongoDb
21. Relational outbox stores: MsSql, MySql, PostgreSql, Sqlite, Spanner
22. NoSQL outbox stores: DynamoDB, DynamoDB.V4, Firestore, MongoDb

### Verification (Task 23)
23. Build + run all core tests

## Key design decisions (ADR 0057)

### Core feature
- **Causation Id**: Links an inbox entry to the outbox messages produced during that handler invocation. Propagated via `RequestContext.Bag` (key: `Brighter-CausationId`). Distinct from CorrelationId (request-reply), JobId, WorkflowId.
- **OnceOnlyAction.Replay**: New enum value. When inbox detects duplicate and action is Replay, it clears dispatch state on matching outbox messages so the sweeper resends them.
- **Two new role interfaces** (opt-in, non-breaking):
  - `IAmACausationTrackingInbox` — knows the CausationId for an inbox entry
  - `IAmACausationTrackingOutbox` — replays a causation's outbox messages; knows if schema supports it (`SupportsCausationTracking()`)
- **UseInboxHandler** gains optional `IAmACausationTrackingOutbox?` constructor param (resolved via standard MSDI `ActivatorUtilities`)

### Observability
- `UseInboxHandler` currently has no telemetry — adding span events for all paths (Add, Throw, Warn, Replay)
- Events written to `Context.Span` (pipeline's Activity), gated on `InstrumentationOptions.Brighter`
- New `BrighterSemanticConventions.CausationId` constant (`"paramore.brighter.causation_id"`) — distinct from existing `ConversationId` which carries `CorrelationId`
- No new child spans; OutboxSweeper already creates its own trace on sweep

### Persistent store strategy
- All 18 Brighter-maintained stores (9 inbox, 9 outbox) get CausationId support
- Schema migration is opt-in — users only need to migrate if they use Replay
- Separate migration PR lands on master first, merges into this branch
- `SupportsCausationTracking()` is a permanent runtime schema check (not transitional)

### Structural prerequisites (tidy-first)
1. **RequestHandler<T>**: Expose `instrumentationOptions` as `protected InstrumentationOptions InstrumentationOptions => instrumentationOptions;` (same for async)
2. **UseInboxHandler**: Switch from private `InitRequestContext()` to pipeline's `this.Context` so Bag data is shared across pipeline
3. **PipelineStepDescription**: Add non-positional `RequestHandlerAttribute? Attribute { get; init; }` property
4. **Describe() global inbox**: Pass `InboxConfiguration` into `ValidatePipelines()` → `PipelineBuilder`. `Describe()` injects global inbox attributes using same `MethodInfo` guard checks as `Build()`
5. **IAmAnOutboxProducerMediator**: Add `IAmAnOutbox? Outbox` read-only property
6. **DI registration**: Register outbox as `IAmACausationTrackingOutbox` alongside primary interface when it implements it

### Pipeline validation
- `HandlerPipelineValidationRules.ReplayRequiresCausationTracking(IAmAnInbox? inbox, IAmAnOutbox? outbox)` — collapsed `Specification<HandlerPipelineDescription>`
- Both inbox and outbox captured via closure; checks: inbox implements tracking, inbox schema supports it, outbox present, outbox implements tracking, outbox schema supports it

### Test strategy
- In-memory stores first (base tests in `Paramore.Brighter.Base.Test`)
- Outbox persistent store tests generated via Liquid templates
- Inbox persistent store tests derived manually from base classes

### Out of scope
- Schema migrations for persistent stores (separate PR, lands first)
- Saga/workflow orchestration
- Immediate send replay (sweeper only)
- Migration tooling for existing data (columns nullable, existing rows have null CausationId)

## Design notes for implementation
- Brighter uses `DateTimeOffset` over `DateTime` in APIs
- `BrighterSemanticConventions.ConversationId` (`messaging.message.conversation_id`) already exists and carries `CorrelationId` — do NOT reuse for CausationId
- `UseInboxHandlerAsync.cs` has a duplicate `base.InitializeFromAttributeParams()` call — fix in Task 1 tidy-first pass
- Spanner is relational (implements `IRelationalDatabaseInboxQueries`), NOT NoSQL

## Files modified
- `docs/adr/0057-replay-outbox-on-inbox-duplicate.md` — the ADR
- `specs/0027-replay-matching-outbox-events-when-inbox-has-already-seen/` — requirements.md, README.md, tasks.md, review-tasks.md, .issue-number, .adr-list, .requirements-approved, .design-approved, .tasks-approved

## Next step
Begin implementation with `/spec:implement`, starting at Task 1 (structural: UseInboxHandler uses pipeline Context + expose InstrumentationOptions).
