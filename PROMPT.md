# Session Resume — Replay Outbox on Inbox Duplicate

## Branch
`replay_on_seen`

## Issue
#2541 — Force Replay of Outbox on seeing duplicate Processed Inbox Message

## Spec
`specs/0027-replay-matching-outbox-events-when-inbox-has-already-seen/`

## Status
- [x] Requirements — approved (2026-04-16), updated 2026-04-17 (ConversationId → CausationId rename, AC-8 updated to all persistent stores)
- [ ] Design — ADR 0057 proposed, 4 rounds of review completed, round 4 had 2 above-threshold findings (both addressed)
- [ ] Tasks — not started
- [ ] Implementation — not started

## Current position
ADR 0057 has been through 4 adversarial reviews. Rounds 1-3 findings were all addressed. Round 4 had 2 above-threshold findings:
1. **ConversationId naming collision (82)** — resolved by renaming the concept to `CausationId` throughout (distinct from existing `BrighterSemanticConventions.ConversationId` which carries `CorrelationId`)
2. **Pipeline validation only checked outbox, not inbox (75)** — resolved by adding inbox check to `ReplayRequiresCausationTracking`

### Remaining below-threshold items from round 4 (not blocking)
3. **ADR uses `Replay` enum name but requirements suggest `ReplayOutbox` (55)** — requirements use "e.g." so not mandated; consider adding a brief rationale note
4. **Describe() changes affect all existing validation consumers (55)** — adding global inbox attributes to `Describe()` means existing rules now see those steps; should be tested for regressions in tidy-first change
5. **`TimeFlushed` vs `DispatchedAt` terminology mixed (50)** — ADR uses both; in-memory uses `TimeFlushed`, persistent stores use `DispatchedAt`; consider standardising prose

### Next step
Either:
- `/spec:review design` to confirm round 4 fixes pass
- `/spec:approve design` if satisfied with changes
- Then `/spec:tasks` to create the implementation task list

## Key design decisions (ADR 0057)

### Core feature
- **Causation Id**: Links an inbox entry to the outbox messages produced during that handler invocation. Captures the causal relationship: this incoming request *caused* these outgoing messages. Propagated via `RequestContext.Bag` (key: `Brighter-CausationId`). Distinct from CorrelationId (request-reply), JobId, WorkflowId.
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
1. **PipelineStepDescription**: Add non-positional `RequestHandlerAttribute? Attribute { get; init; }` property (existing positional params unchanged — non-breaking)
2. **Describe() global inbox**: Pass `InboxConfiguration` into `ValidatePipelines()` → `PipelineBuilder`. `Describe()` injects global inbox attributes using same `MethodInfo` guard checks as `Build()`. Shared infrastructure change — needs focused regression tests.
3. **UseInboxHandler RequestContext**: Switch from private `InitRequestContext()` to pipeline's `this.Context` so Bag data is shared across pipeline
4. **DI registration**: Register outbox as `IAmACausationTrackingOutbox` alongside primary interface when it implements it

### Pipeline validation
- `HandlerPipelineValidationRules.ReplayRequiresCausationTracking(IAmAnInbox? inbox, IAmAnOutbox? outbox)` — collapsed `Specification<HandlerPipelineDescription>` (same pattern as `BackstopAttributeOrdering`)
- Both inbox and outbox captured via closure; 4 checks: inbox implements tracking, outbox present, outbox implements tracking, outbox schema supports it
- Rule added to existing specs array in `ValidateHandlerPipelines()`

### Test strategy
- In-memory stores first (base tests in `Paramore.Brighter.Base.Test`)
- Outbox persistent store tests generated via Liquid templates
- Inbox persistent store tests derived manually from base classes

### Out of scope
- Schema migrations for persistent stores (separate PR, lands first)
- Saga/workflow orchestration
- Immediate send replay (sweeper only)
- Migration tooling for existing data (columns nullable, existing rows have null CausationId)

## Review history
- Round 1: 5 findings above threshold (90, 85, 75, 70, 65) — all addressed
- Round 2: 3 findings above threshold (75, 72, 65) — all addressed
- Round 3: 5 findings above threshold (72, 70, 68, 65, 62) — all addressed
- Round 4: 2 findings above threshold (82, 75) — both addressed

## Design notes for implementation
- Brighter uses `DateTimeOffset` over `DateTime` in APIs
- `BrighterSemanticConventions.ConversationId` (`messaging.message.conversation_id`) already exists and carries `CorrelationId` — do NOT reuse for CausationId

## Files modified
- `docs/adr/0057-replay-outbox-on-inbox-duplicate.md` — the ADR
- `specs/0027-replay-matching-outbox-events-when-inbox-has-already-seen/` — requirements.md, README.md, .issue-number, .adr-list, .requirements-approved, review-design.md
