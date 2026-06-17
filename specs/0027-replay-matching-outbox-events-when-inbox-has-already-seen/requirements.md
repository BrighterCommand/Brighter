# Requirements

> **Note**: This document captures user requirements and needs. Technical design decisions and implementation details should be documented in an Architecture Decision Record (ADR) in `docs/adr/`.

**Linked Issue**: #2541

## Problem Statement

As a developer building multi-step workflows with Brighter, I would like to be able to trigger automatic replay of downstream outbox messages when a duplicate command is received in the inbox, so that I can re-trigger a workflow by re-sending a message to a Brigter consumer, have it skip already-completed steps, but send outgoing messages that force te re-execution of any steps subsequent steps eventually triggering those that we re never actioned.

Today, when the inbox detects a duplicate message (via `OnceOnly`), it either throws (`OnceOnlyAction.Throw`) or warns and skips (`OnceOnlyAction.Warn`). In neither case does it cause downstream messages that were originally produced to be resent. This means that if a workflow fails partway through — the handler completed but some downstream consumers never received or processed their messages — there is no mechanism to retry the workflow; the inbox will skip processing and no messages will be raised to trigger downstream consumers.

We don't want to reprocess the message, if we were idempotent there would be no need for the Inbox. Instead, we want to resend the **same** outgoing messages that we sent the last time the handler was executed. This avoids the problem that the inbox does not create true idempotency because the downstream messages raised are not the same as the last time we executed the handler. The outgoing messages are not side-effects, but direct effects.

In  some cases there may be an Inbox, but no Outbox. This a terminal step, which doesn't raise further messages. It is important to test for the presence of an Outbox before attempting to add a causation id or clear the associated Outbox messages.

## Proposed Solution

Introduce a **Causation Id** that links an incoming inbox entry to the outgoing outbox messages produced during that handler's execution. The causation id captures the causal relationship: this incoming request *caused* these outgoing messages. When the inbox detects a duplicate message and a "replay downstream on duplicate" option is enabled, it clears the `DispatchedAt` timestamp on all outbox messages sharing that Causation Id. This causes the outbox sweeper to re-dispatch those messages, effectively replaying the downstream workflow steps.

From a user perspective:
- A new `CausationId` is propagated through the request context during handler execution and stored in both the inbox and outbox.
- The inbox can be configured (via attribute or `InboxConfiguration`) with a flag to replay downstream messages on duplicate detection instead..
- When replay is triggered, the outbox sweeper picks up the "un-dispatched" messages and resends them naturally — no new dispatch mechanism is needed.

## Requirements

### Functional Requirements

1. **Causation Id propagation**: A `CausationId` must be added to the request context when a request enters a handler pipeline (if it does not already exist) and propagated to all outbox messages produced during that handler's execution.
2. **Causation Id storage**: Both the inbox and outbox must store the `CausationId`, allowing correlation between an incoming command and its downstream messages.
3. **Replay on duplicate**: When the inbox detects a duplicate (message already exists) and the replay flag is enabled, it must clear the `DispatchedAt` field on all outbox messages with the matching `CausationId`.
4. **Sweeper re-dispatch**: Outbox messages with a cleared `DispatchedAt` must be picked up by the existing outbox sweeper and re-dispatched — no new dispatch path is required. This is existing functionality.
5. **Causation Id is distinct from Job Id**: The `CausationId` represents a single handler invocation's downstream messages, not an entire job. Re-running a workflow should only replay the messages for the specific step being re-triggered, not all messages for that job.
6. **New OnceOnly action**: A new `OnceOnlyAction` value (e.g., `ReplayOutbox`) or a separate flag should indicate that duplicate detection should trigger downstream replay rather than throwing or warning.

### Non-functional Requirements

- **Non-breaking change**: Existing inbox/outbox implementations that do not support `CausationId` must continue to work without modification. The new behavior is opt-in.
- **Pipeline validation**: At startup, if replay-on-duplicate is configured, pipeline validation must verify that both the inbox and outbox implementations support `CausationId`. Unsupported implementations should produce a clear validation error.
- **Performance**: Clearing `DispatchedAt` on outbox messages by `CausationId` should be efficient. The outbox store indexes `CausationId` (where the store supports a secondary index) so replay queries do not table-scan; this index is delivered as part of the schema evolution in AC9.
- **Observability**: Replay events should be traceable — when messages are replayed, this should be visible in logs and telemetry.

### Constraints and Assumptions

- Only the outbox sweeper is used for re-dispatch; immediate send is not supported for replay (as noted in the issue: "if you don't have a sweeper it's unlikely you also want this kind of support").
- The `CausationId` is separate from the existing `CorrelationId` (used for request-reply) and `JobId`/`WorkflowId` (reserved for future workflow orchestration). It specifically represents "the set of outbox messages caused by handling this inbox entry."
- The existing `JobId` and `WorkflowId` fields on `MessageHeader` are reserved for future use and should not be repurposed for this feature.
- **Schema evolution is delivered through BoxProvisioning.** The `CausationId` column is added to the Brighter-maintained relational inbox/outbox schemas as a new BoxProvisioning migration version (idempotent `ALTER TABLE ADD ... NULL`) for the catalog-based stores (MsSql, MySql, PostgreSql, Sqlite), and through the provisioner for Spanner. NoSQL stores (DynamoDB, DynamoDB.V4, Firestore, MongoDb) are schemaless and need no migration. This work is part of this spec, not a separate PR.


### Out of Scope

- Saga/workflow orchestration — this feature enables replay of a single step's downstream messages, not orchestration of multi-step workflows.
- Immediate send replay — only sweeper-based re-dispatch is supported.
- Automatic retry of the handler logic itself — the handler is not re-executed, only its previously-produced outbox messages are replayed.
- Data backfill for existing outbox/inbox rows — the new `CausationId` column is nullable; existing rows keep a null `CausationId` and cannot be replayed. Schema *evolution* (adding the column) is in scope via BoxProvisioning; *data* migration is not.

## Acceptance Criteria

1. When a handler produces outbox messages, all messages share the same `CausationId` as the inbox entry for the triggering command.
2. When a duplicate command arrives and replay is configured, the outbox messages for that `CausationId` have their `DispatchedAt` cleared.
3. The outbox sweeper subsequently re-dispatches those messages.
4. When replay is not configured, existing `OnceOnly` behavior (throw or warn) is unchanged.
5. When replay is configured but the inbox or outbox implementation does not support `CausationId`, pipeline validation at startup fails with a descriptive error.
6. The `CausationId` is independent of `JobId` — replaying one step does not affect other steps in the same job.
7. In-memory inbox and outbox implementations support `CausationId` for testing.
8. All persistent inbox and outbox implementations support `CausationId`. Base tests in `Paramore.Brighter.Base.Test` verify the causation tracking interfaces; persistent store tests are derived from the base tests (outbox via the Liquid template generator, inbox manually).
9. The `CausationId` column is added to the relational stores through BoxProvisioning. ("Catalog-based" stores carry versioned migration catalogs that BoxProvisioning replays; Spanner has no catalog and provisions the column directly.) For the catalog-based stores (MsSql, MySql, PostgreSql, Sqlite) the column is added as a new migration version plus the matching live-builder DDL; Spanner adds it through its provisioner and live builder. Verifiable end-state: a fresh install and a migrated upgrade both end with (a) the `CausationId` column present, (b) for the outbox, an index on `CausationId` where the store supports one, and (c) `SupportsCausationTracking()` returning `true`. The live-builder DDL and the migration chain must produce identical column sets (this parity is what the existing drift test enforces, but the acceptance is the identical-column-set outcome, not the test by name).

## Additional Context

The codebase already has infrastructure for workflow-level correlation:
- `MessageHeader.JobId` and `MessageHeader.WorkflowId` — reserved for future use, stored in DB schemas
- `CorrelationId` — used for request-reply patterns
- `RequestContext.Bag` — carries arbitrary key-value data through the pipeline
- `RequestContextBagNames` — defines well-known bag keys

The `CausationId` fills a gap between per-message correlation (`CorrelationId`) and per-workflow correlation (`JobId`/`WorkflowId`): it represents "all the outbox messages caused by handling a single inbox entry."
